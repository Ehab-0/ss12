using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>
///     The area a sprite's art covers when it is drawn into the billboard atlas.
/// </summary>
/// <remarks>
///     <para>
///     The engine's local sprite bounds include the offset of every layer but not the offset of the sprite itself
///     (<c>sprite.Offset</c>), which the engine applies on top when it draws. Wall lamps have <c>offset: 0, 1</c>: their art is
///     drawn one tile away from the entity so that it lands on the wall. Reserving an atlas slot from the plain local bounds
///     therefore put the art one tile outside the slot, on top of whatever sat next to it: a glowing tube appeared on a
///     character, on a floor item or on another wall fixture, and moved to another entity whenever the nearest-first order of
///     the atlas changed (that is, whenever the player walked), while the lamp's own quad stayed empty.
///     </para>
///     <para>
///     The bounds returned here are the local bounds moved by the offset exactly as the sprite is drawn, so the art is inside
///     its slot and the quad that shows the slot shows the whole sprite.
///     </para>
/// </remarks>
public static class AtlasBounds
{
    /// <summary>For a sprite drawn unrotated (upright quads and wall panels).</summary>
    public static Box2 Unrotated(Box2 local, Vector2 spriteOffset)
    {
        return local.Translated(spriteOffset);
    }

    /// <summary>For a sprite drawn with the rotation of its entity (things lying flat); the offset turns with it.</summary>
    public static Box2 Rotated(Box2 local, Vector2 spriteOffset, Angle rotation)
    {
        var turned = new Box2Rotated(local, rotation, Vector2.Zero).CalcBoundingBox();
        return turned.Translated(rotation.RotateVec(spriteOffset));
    }

    /// <summary>
    ///     The part of a sprite's bounds that lies inside its own tile (one tile around the entity origin). A door, a window or a
    ///     panel stands in the one tile it is in, so only that square of its picture belongs on the face that is drawn. The docking
    ///     airlock has a clamp layer one tile below the door: its bounds are two tiles tall, and showing all of it on a one tile
    ///     doorway squeezed the door into the top half and left the rest of the opening empty.
    /// </summary>
    public static Box2 TileCrop(Box2 bounds)
    {
        var left = Math.Max(bounds.Left, -0.5f);
        var bottom = Math.Max(bounds.Bottom, -0.5f);
        var right = Math.Min(bounds.Right, 0.5f);
        var top = Math.Min(bounds.Top, 0.5f);
        return right > left && top > bottom ? new Box2(left, bottom, right, top) : bounds;
    }

    /// <summary>
    ///     The area the engine really covers when it draws a sprite: it turns the layers by the sprite's own rotation
    ///     (<c>sprite.Rotation</c>, which a lying character has), moves them by <c>sprite.Offset</c>, and then turns all of
    ///     that by the entity rotation it was asked to draw with (<paramref name="entityRotation"/>, see
    ///     <see cref="DrawnRotation"/>). The engine's own local bounds leave out both the sprite rotation and the offset.
    /// </summary>
    public static Box2 Drawn(Box2 local, Vector2 spriteOffset, Angle spriteRotation, Angle entityRotation)
    {
        var turned = new Box2Rotated(local, entityRotation + spriteRotation, Vector2.Zero).CalcBoundingBox();
        return turned.Translated(entityRotation.RotateVec(spriteOffset));
    }

    /// <summary>
    ///     The entity rotation the engine uses when it draws a sprite at <paramref name="worldRotation"/> with the camera
    ///     turned by zero: none for a sprite that never rotates, only the part left over after snapping to a quarter turn for
    ///     a sprite that snaps to cardinal directions, else the whole of it.
    /// </summary>
    public static Angle DrawnRotation(Angle worldRotation, bool noRotation, bool snapCardinals)
    {
        if (noRotation)
            return Angle.Zero;

        if (!snapCardinals)
            return worldRotation;

        return worldRotation - worldRotation.Reduced().FlipPositive().RoundToCardinalAngle();
    }
}
