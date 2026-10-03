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
}
