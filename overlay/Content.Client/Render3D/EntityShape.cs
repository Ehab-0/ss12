using System.Numerics;

namespace Content.Client.Render3D;

/// <summary>What kind of thing an entity is, for the lean and thickness settings (each category can be switched on its own).</summary>
public enum EntityCategory : byte
{
    /// <summary>Anything that is not an item or a mob: machines, furniture, structures.</summary>
    Object = 0,

    /// <summary>Items (loose, thrown or on a table).</summary>
    Item,

    /// <summary>Mobs: players, NPCs, animals.</summary>
    Character,
}

/// <summary>
///     Pure geometry for the shape of things in the 3D view: how far a card tilts towards the camera, where its corners
///     end up, and how many layers give it thickness. Kept free of engine types so it can be unit tested.
/// </summary>
/// <remarks>
///     A thing is a "card" (one quad of its sprite). <b>Lean</b> tilts the card: a card lying on the floor rises on its
///     far side (so its art faces the camera more), a standing card falls back at the top (so it is less squashed seen
///     from above). <b>Thickness</b> adds copies of the card behind its front face along the card's normal, darker with
///     depth, so the silhouette gets sides like a small slab (sprite stacking).
/// </remarks>
public static class EntityShape
{
    /// <summary>Default thickness in tiles when no rule or component says otherwise.</summary>
    public static float DefaultThickness(EntityCategory category) => category switch
    {
        EntityCategory.Item => 0.06f,
        EntityCategory.Character => 0.16f,
        _ => 0.14f,
    };

    /// <summary>Brightness of the deepest layer (the sides of the slab).</summary>
    public const float SideShade = 0.58f;

    /// <summary>Beyond this distance (tiles) things get half the layers, and beyond <see cref="FarRange"/> none.</summary>
    public const float NearRange = 9f;

    public const float FarRange = 17f;

    /// <summary>Largest tilt of a flat card (radians, 20 degrees). More makes cards rise like ramps into the view.</summary>
    public const float MaxFlatLean = 0.35f;

    /// <summary>Largest backwards tilt of a standing card (radians, about 17 degrees).</summary>
    public const float MaxStandLean = 0.30f;

    /// <summary>How far the camera looks down, in radians: 0 = horizontal, pi/2 = straight down.</summary>
    public static float DownPitch(Vector3 forward)
        => MathF.Max(0f, MathF.Asin(Math.Clamp(-forward.Z, -1f, 1f)));

    /// <summary>Tilt of a card lying on the floor: none when looking straight down, up to 20 degrees when the camera is low.</summary>
    public static float FlatLean(float downPitch)
        => Math.Clamp(0.4f * (MathF.PI * 0.5f - downPitch), 0f, MaxFlatLean);

    /// <summary>
    ///     Limits the tilt of a card lying on a surface so its far edge rises by at most <paramref name="maxRise"/> tiles: a card
    ///     <paramref name="span"/> tiles deep that tilts by an angle rises by span x sin(angle), which for a one tile sprite at
    ///     the full 20 degrees is a third of a tile, enough to make a bedsheet hover over the table it lies on. A small card
    ///     keeps the whole tilt. A <paramref name="maxRise"/> of 0 or less means no limit.
    /// </summary>
    public static float CapFlatLean(float lean, float span, float maxRise)
    {
        if (maxRise <= 0f || span <= 0.0001f)
            return lean;

        return MathF.Min(lean, MathF.Asin(Math.Clamp(maxRise / span, 0f, 1f)));
    }

    /// <summary>Backwards tilt of a standing card: grows with how far the camera looks down.</summary>
    public static float StandLean(float downPitch)
        => Math.Clamp(0.45f * downPitch, 0f, MaxStandLean);

    /// <summary>Number of extra layers to draw behind the front face at this distance from the camera.</summary>
    /// <summary>
    ///     Whether an entity is so close to the camera that its thickness layers are left out. The entity shader fades what is
    ///     close to the camera, by the depth of each pixel, and the layers lie behind the front face, so they are further away and
    ///     fade less: the face went transparent and its layers showed as a long smeared band across the view.
    /// </summary>
    public static bool TooCloseForLayers(float distanceSquaredToCamera) => distanceSquaredToCamera < NearLayerDistance * NearLayerDistance;

    /// <summary>Distance in tiles from the camera inside which an entity gets no thickness layers.</summary>
    public const float NearLayerDistance = 1.1f;

    public static int LayerCount(int maxLayers, float distance)
    {
        if (maxLayers <= 0 || distance > FarRange)
            return 0;

        return distance <= NearRange ? maxLayers : Math.Max(1, maxLayers / 2);
    }

    /// <summary>
    ///     A corner of a floor card that is tilted by <paramref name="lean"/> about its near edge (the edge nearest the
    ///     camera stays where it is, the far edge rises), then moved so the card keeps its centre. <paramref name="offset"/>
    ///     is the corner relative to the card centre on the floor, <paramref name="forward"/> the camera's horizontal
    ///     direction, <paramref name="sMin"/> and <paramref name="sMax"/> the extent of the card along it.
    /// </summary>
    public static Vector3 LeanFlatPoint(Vector2 offset, float z, Vector2 forward, float sMin, float sMax, float lean)
    {
        var s = Vector2.Dot(offset, forward);
        var ds = s - sMin;
        var c = MathF.Cos(lean);
        var sn = MathF.Sin(lean);
        var shift = ds * (c - 1f) + (sMax - sMin) * (1f - c) * 0.5f;
        var h = offset + forward * shift;
        return new Vector3(h, z + ds * sn);
    }

    /// <summary>Direction a floor card tilted by <paramref name="lean"/> faces (unit, points up and towards the camera).</summary>
    public static Vector3 FlatNormal(Vector2 forward, float lean)
        => new(-forward.X * MathF.Sin(lean), -forward.Y * MathF.Sin(lean), MathF.Cos(lean));

    /// <summary>
    ///     A point of a standing card that falls back by <paramref name="lean"/> about its foot: a point
    ///     <paramref name="height"/> above the foot ends up further from the camera and a little lower.
    /// </summary>
    public static Vector3 LeanStandPoint(Vector2 foot, float z, float height, Vector2 forward, float lean)
    {
        var h = foot + forward * (height * MathF.Sin(lean));
        return new Vector3(h, z + height * MathF.Cos(lean));
    }

    /// <summary>Direction a standing card leaning back by <paramref name="lean"/> faces (unit, towards the camera and up).</summary>
    public static Vector3 StandNormal(Vector2 forward, float lean)
        => new(-forward.X * MathF.Cos(lean), -forward.Y * MathF.Cos(lean), MathF.Sin(lean));

    /// <summary>Brightness of layer <paramref name="index"/> of <paramref name="layers"/> (0 = the front face).</summary>
    public static float LayerShade(int index, int layers)
        => layers <= 0 ? 1f : 1f + (SideShade - 1f) * (index / (float) layers);
}
