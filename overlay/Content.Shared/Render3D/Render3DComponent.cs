using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Render3D;

/// <summary>
///     How an entity is drawn by the 3D renderer. See docs/ss12/PLAN.md section 5 (Phase 2) and section 6.
/// </summary>
public enum Render3DMode : byte
{
    /// <summary>No override; the automatic classifier decides.</summary>
    Auto = 0,

    /// <summary>Not drawn as an entity (already part of the ground capture or the tile map).</summary>
    Skip,

    /// <summary>Full-height wall block, rendered by the tile-map raymarcher rather than as a quad.</summary>
    WallBlock,

    /// <summary>Upright quad that rotates around the vertical axis to face the camera.</summary>
    Billboard,

    /// <summary>Horizontal quad on the floor, using the sprite's top-down art.</summary>
    FlatFloor,

    /// <summary>Horizontal quad hovering at the effect height.</summary>
    FlatAir,

    /// <summary>Double-sided vertical quad through the tile center (airlocks, firelocks, shutters).</summary>
    Panel,

    /// <summary>Vertical quad on the tile edge given by the entity rotation (directional windows, railings).</summary>
    EdgePanel,

    /// <summary>Four alpha-blended vertical faces around the tile (full windows, grilles).</summary>
    GlassBox,

    /// <summary>Low box whose top face is the sprite (tables).</summary>
    TableBox,

    /// <summary>Vertical quad hugging the wall face the entity is mounted on (signs, APCs, posters).</summary>
    WallDecal,
}

/// <summary>
///     Client-side rendering hints for the 3D view. Data only; the server never reads it.
///     Placed on a few abstract base prototypes so thousands of children inherit it.
/// </summary>
[RegisterComponent]
public sealed partial class Render3DComponent : Component
{
    /// <summary>Overrides the automatic classification when not <see cref="Render3DMode.Auto"/>.</summary>
    [DataField]
    public Render3DMode Mode = Render3DMode.Auto;

    /// <summary>
    ///     Optional pre-rendered 8-direction impostor sprite (Phase 8). When set and the entity is in its default
    ///     visual state the billboard uses it instead of the live sprite.
    /// </summary>
    [DataField]
    public SpriteSpecifier.Rsi? Impostor;

    /// <summary>Thickness in tiles for this entity (0 = paper thin); negative uses the default of its category or the rules.</summary>
    [DataField]
    public float Thickness = -1f;

    /// <summary>False keeps this entity from tilting towards the camera.</summary>
    [DataField]
    public bool Lean = true;
}
