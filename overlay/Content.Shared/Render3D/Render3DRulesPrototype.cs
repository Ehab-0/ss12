using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.Render3D;

/// <summary>
///     Data-driven classification for the 3D renderer: which <see cref="Render3DMode"/> an entity gets, decided by the
///     prototype it was spawned from (or any prototype it inherits from) and by components it has. Lives in its own
///     file so installing 3D into another server codebase does not require editing any of that codebase's prototypes.
///     Several <c>render3dRules</c> prototypes can exist; all of them are consulted.
/// </summary>
[Prototype("render3dRules")]
public sealed partial class Render3DRulesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<Render3DRule> Rules = new();

    /// <summary>Per-prototype overrides of the thickness and the lean of things (see <see cref="Render3DShapeRule"/>).</summary>
    [DataField]
    public List<Render3DShapeRule> Shapes = new();

    /// <summary>How windows, window doors and grilles are drawn (see <see cref="Render3DGlassRule"/>).</summary>
    [DataField]
    public List<Render3DGlassRule> Glass = new();

    /// <summary>How high the top of a surface is, for the items that lie on it (see <see cref="Render3DSurfaceRule"/>).</summary>
    [DataField]
    public List<Render3DSurfaceRule> Surfaces = new();
}

/// <summary>
///     One rule. An entity matches when its prototype is, or inherits from, one of <see cref="Parents"/>, or when it
///     has one of the <see cref="Components"/>. The nearest ancestor wins, so a child prototype listed in a rule beats
///     the base prototype it inherits from. Names are plain strings on purpose: a fork that renamed or removed a
///     prototype must not fail the YAML linter because of this file.
/// </summary>
[DataDefinition]
public sealed partial class Render3DRule
{
    [DataField(required: true)]
    public Render3DMode Mode;

    /// <summary>Entity prototype ids (matched against the entity's prototype and all its parents).</summary>
    [DataField]
    public List<string> Parents = new();

    /// <summary>Registered component names (for example <c>Door</c>).</summary>
    [DataField]
    public List<string> Components = new();
}

/// <summary>
///     Overrides how thick a thing is drawn and whether it leans towards the camera. Matched like
///     <see cref="Render3DRule"/> (prototype and ancestors, nearest wins, then components). Use it to keep flat things
///     flat (paper, cards) and give bulky ones more body (toolboxes, crates). Only has an effect where the matching
///     effects are switched on in the 3D settings.
/// </summary>
[DataDefinition]
public sealed partial class Render3DShapeRule
{
    /// <summary>Entity prototype ids (matched against the entity's prototype and all its parents).</summary>
    [DataField]
    public List<string> Parents = new();

    /// <summary>Registered component names.</summary>
    [DataField]
    public List<string> Components = new();

    /// <summary>Thickness in tiles (0 = paper thin). Negative leaves the default of the category.</summary>
    [DataField]
    public float Thickness = -1f;

    /// <summary>False keeps the thing from tilting towards the camera (it stays exactly flat or upright).</summary>
    [DataField]
    public bool Lean = true;

    /// <summary>
    ///     False makes a standing object turn to face the camera as you walk round it (a tree, a statue, anything round or without
    ///     a front); the default keeps furniture and machines fixed in the world, facing the way they point.
    /// </summary>
    [DataField]
    public bool Fixed = true;
}

/// <summary>
///     The height of the top of a surface that items lie on, in tiles above the floor. Without a rule a table is as high as the
///     <c>render3d.table_height</c> setting and a standing object (a locker, a crate) as high as its drawn picture. Matched like
///     <see cref="Render3DRule"/> by prototype and ancestors (nearest wins). Only has an effect where the item_surface effect is on.
/// </summary>
[DataDefinition]
public sealed partial class Render3DSurfaceRule
{
    /// <summary>Entity prototype ids (matched against the entity's prototype and all its parents).</summary>
    [DataField]
    public List<string> Parents = new();

    /// <summary>Height of the top of the surface above the floor, in tiles.</summary>
    [DataField]
    public float Height = 0.45f;
}

/// <summary>
///     How a kind of glass is drawn by the 3D view. The picture of a window in the game is a top-down drawing of a frame with
///     a small pane in the middle; stuck on a standing face it looks like a wall and hides everything behind it. With the
///     "glass" effect on, the 3D view draws these faces itself instead: a thin frame and a mostly transparent, tinted pane
///     (and a fine mesh for grilles), keeping the cracks the sprite shows for damage. Matched like
///     <see cref="Render3DRule"/> by prototype and ancestors (nearest wins); only prototypes drawn as glass boxes or edge
///     panels are looked up.
/// </summary>
[DataDefinition]
public sealed partial class Render3DGlassRule
{
    /// <summary>Entity prototype ids (matched against the entity's prototype and all its parents).</summary>
    [DataField]
    public List<string> Parents = new();

    /// <summary>True keeps the sprite for these (corner and diagonal windows, whose picture is not a plain pane).</summary>
    [DataField]
    public bool Disabled;

    /// <summary>Colour of the pane.</summary>
    [DataField]
    public Color Tint = Color.FromHex("#8fc4e8");

    /// <summary>How opaque the pane is, 0 (clear) to 1.</summary>
    [DataField]
    public float Alpha = 0.18f;

    /// <summary>Colour of the frame around the pane.</summary>
    [DataField]
    public Color Frame = Color.FromHex("#3a4658");

    /// <summary>Width of the frame in pixels of the 32 pixel picture of a tile (0 = none).</summary>
    [DataField]
    public int FrameWidth = 1;

    /// <summary>Distance in pixels between the lines of a fine mesh over the pane (grilles); 0 = no mesh.</summary>
    [DataField]
    public int Mesh;

    /// <summary>Colour of the mesh lines.</summary>
    [DataField]
    public Color MeshColor = Color.FromHex("#2a2e33");

    /// <summary>A faint diagonal highlight, which makes a clear pane read as glass.</summary>
    [DataField]
    public bool Shine = true;
}
