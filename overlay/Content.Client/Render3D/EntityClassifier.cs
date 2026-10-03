using System.Numerics;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Placeable;
using Content.Shared.Projectiles;
using Content.Shared.Render3D;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using DrawDepthContent = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Render3D;

/// <summary>One entity that the 3D pass draws this frame, with everything the later stages need.</summary>
public struct EntityDraw3D
{
    public EntityUid Uid;
    public SpriteComponent Sprite;
    public Render3DMode Mode;

    /// <summary>World (map space) position of the entity origin.</summary>
    public Vector2 Pos;

    public Angle WorldRot;

    /// <summary>Height of the base of the drawn quad above the floor.</summary>
    public float Z;

    /// <summary>Extent of the sprite in the space it was drawn in (tile units, y up, relative to the origin).</summary>
    public Box2 Bounds;

    public float Distance2;

    /// <summary>Where the sprite is stored in the atlas (pixels), or an empty box when not placed.</summary>
    public UIBox2i Slot;

    /// <summary>Position inside the slot where the entity origin was drawn.</summary>
    public Vector2 SlotOrigin;

    /// <summary>Direction override used when drawing the sprite (null = use the entity's real rotation).</summary>
    public Direction? DrawDirection;

    /// <summary>Draw with the entity's real world rotation (flat modes) instead of rotation zero.</summary>
    public bool DrawRotated;

    public bool Placed;

    /// <summary>The sprite has visible unshaded layers, drawn a second time into the glow atlas.</summary>
    public bool Glow;

    /// <summary>Wall decals only: false when nothing was found to hang it on (drawn on the ceiling).</summary>
    public bool OnWall;

    /// <summary>Pre-rendered impostor frame to draw instead of the live sprite (Phase 8), if any.</summary>
    public Robust.Client.Graphics.Texture? Impostor;

    /// <summary>Which lean / thickness settings apply to this entity.</summary>
    public EntityCategory Category;

    /// <summary>Thickness in tiles (0 = flat) after the category default, the rules and the component override.</summary>
    public float Thickness;

    /// <summary>False when a rule or the component keeps this entity from tilting.</summary>
    public bool CanLean;
}

/// <summary>
///     Decides how each entity is drawn by the 3D renderer (plan section 6.1). The first matching rule wins.
/// </summary>
public sealed class EntityClassifier
{
    private readonly EntityQuery<Render3DComponent> _render3d;
    private readonly Render3DCompat _compat;
    private readonly EntityQuery<MobStateComponent> _mobs;
    private readonly EntityQuery<StandingStateComponent> _standing;
    private readonly EntityQuery<ThrownItemComponent> _thrown;
    private readonly EntityQuery<ItemComponent> _items;
    private readonly EntityQuery<ProjectileComponent> _projectiles;
    private readonly EntityQuery<WallMountComponent> _wallMounts;
    private readonly EntityQuery<PointLightComponent> _lights;
    private readonly EntityQuery<DoorComponent> _doors;
    private readonly EntityQuery<PlaceableSurfaceComponent> _surfaces;

    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protos;
    private readonly EntityQuery<MetaDataComponent> _meta;

    // entity prototype id -> mode from the render3dRules prototypes (null = no rule), computed once per prototype
    private readonly Dictionary<string, Render3DMode?> _ruleCache = new();
    private Dictionary<string, Render3DMode>? _parentModes;
    private List<(Render3DMode Mode, List<Type> Components)>? _componentRules;

    // shape rules: prototype id -> (thickness, lean) from the render3dRules prototypes, plus component rules
    private readonly Dictionary<string, (float Thickness, bool Lean)?> _shapeCache = new();
    private Dictionary<string, (float Thickness, bool Lean)>? _shapeParents;
    private List<(float Thickness, bool Lean, List<Type> Components)>? _shapeComponents;

    public EntityClassifier(IEntityManager entMan, IPrototypeManager protos)
    {
        _entMan = entMan;
        _protos = protos;
        _meta = entMan.GetEntityQuery<MetaDataComponent>();
        _render3d = entMan.GetEntityQuery<Render3DComponent>();
        _compat = new Render3DCompat(entMan);
        _mobs = entMan.GetEntityQuery<MobStateComponent>();
        _standing = entMan.GetEntityQuery<StandingStateComponent>();
        _thrown = entMan.GetEntityQuery<ThrownItemComponent>();
        _items = entMan.GetEntityQuery<ItemComponent>();
        _projectiles = entMan.GetEntityQuery<ProjectileComponent>();
        _wallMounts = entMan.GetEntityQuery<WallMountComponent>();
        _lights = entMan.GetEntityQuery<PointLightComponent>();
        _doors = entMan.GetEntityQuery<DoorComponent>();
        _surfaces = entMan.GetEntityQuery<PlaceableSurfaceComponent>();
    }

    public bool IsSurface(EntityUid uid) => _surfaces.HasComp(uid);

    public Render3DMode Classify(EntityUid uid, SpriteComponent sprite, TransformComponent xform)
    {
        // 2. explicit override
        if (_render3d.TryComp(uid, out var explicitMode) && explicitMode.Mode != Render3DMode.Auto)
        {
            // WallBlock entities live in the tile map.
            return explicitMode.Mode == Render3DMode.WallBlock ? Render3DMode.Skip : explicitMode.Mode;
        }

        // 2b. rules from render3dRules prototypes
        if (RuleMode(uid) is { } ruleMode)
            return ruleMode == Render3DMode.WallBlock ? Render3DMode.Skip : ruleMode;

        // 3. walls are blocks in the tile map
        if (_compat.IsWall(uid))
            return Render3DMode.Skip;

        var depth = sprite.DrawDepth;
        var isItem = _items.HasComp(uid);

        // 4. floor-level things are already part of the ground capture. Loose items are the exception (they are
        // lifted off the ground capture); anchored items such as pipes and cables are part of the floor.
        var looseItem = isItem && !xform.Anchored;
        if (depth <= (int) DrawDepthContent.HighFloorObjects && !looseItem)
            return Render3DMode.Skip;

        // 5. mobs
        if (_mobs.TryComp(uid, out var mob))
        {
            var lying = mob.CurrentState is MobState.Dead or MobState.Critical
                || _standing.TryComp(uid, out var standing) && !standing.Standing;
            return lying ? Render3DMode.FlatFloor : Render3DMode.Billboard;
        }

        // 6. thrown items in flight
        if (_thrown.HasComp(uid))
            return Render3DMode.Billboard;

        // projectiles and effects hover flat at effect height
        if (_projectiles.HasComp(uid) || depth == (int) DrawDepthContent.Effects)
            return Render3DMode.FlatAir;

        // 7. loose items on the ground lie on the floor
        if (looseItem)
            return Render3DMode.FlatFloor;

        // 8. wall mounted things
        if (depth == (int) DrawDepthContent.WallMountedItems || _wallMounts.HasComp(uid) && depth >= (int) DrawDepthContent.WallMountedItems)
            return Render3DMode.WallDecal;

        // doors are panels unless a prototype says otherwise (windoors are marked EdgePanel in YAML)
        if (_doors.HasComp(uid))
            return Render3DMode.Panel;

        // 10. everything else
        return Render3DMode.Billboard;
    }

    /// <summary>The mode the render3dRules prototypes give this entity, if any (nearest matching ancestor wins).</summary>
    private Render3DMode? RuleMode(EntityUid uid)
    {
        if (!_meta.TryComp(uid, out var meta) || meta.EntityPrototype is not { } proto)
            return null;

        if (_ruleCache.TryGetValue(proto.ID, out var cached))
            return cached;

        BuildRules();

        Render3DMode? result = null;
        // EnumerateAllParents includes abstract prototypes (the base prototypes the rules mostly name), which are not
        // indexed at runtime and so are skipped by EnumerateParents.
        foreach (var (ancestorId, _) in _protos.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
        {
            if (_parentModes!.TryGetValue(ancestorId, out var mode))
            {
                result = mode;
                break;
            }
        }

        // component rules only apply when no prototype rule matched; they are checked per entity, so don't cache them
        if (result == null && _componentRules!.Count > 0)
        {
            foreach (var (mode, types) in _componentRules)
            {
                foreach (var type in types)
                {
                    if (_entMan.HasComponent(uid, type))
                        return mode;
                }
            }
        }

        _ruleCache[proto.ID] = result;
        return result;
    }

    private void BuildRules()
    {
        if (_parentModes != null)
            return;

        _parentModes = new Dictionary<string, Render3DMode>();
        _componentRules = new List<(Render3DMode, List<Type>)>();
        _shapeParents = new Dictionary<string, (float, bool)>();
        _shapeComponents = new List<(float, bool, List<Type>)>();
        var factory = _entMan.ComponentFactory;

        foreach (var set in _protos.EnumeratePrototypes<Render3DRulesPrototype>())
        {
            foreach (var rule in set.Rules)
            {
                foreach (var parent in rule.Parents)
                    _parentModes[parent] = rule.Mode;

                var types = new List<Type>();
                foreach (var name in rule.Components)
                {
                    if (factory.TryGetRegistration(name, out var reg))
                        types.Add(reg.Type);
                }

                if (types.Count > 0)
                    _componentRules.Add((rule.Mode, types));
            }

            foreach (var shape in set.Shapes)
            {
                foreach (var parent in shape.Parents)
                    _shapeParents[parent] = (shape.Thickness, shape.Lean);

                var types = new List<Type>();
                foreach (var name in shape.Components)
                {
                    if (factory.TryGetRegistration(name, out var reg))
                        types.Add(reg.Type);
                }

                if (types.Count > 0)
                    _shapeComponents.Add((shape.Thickness, shape.Lean, types));
            }
        }
    }

    /// <summary>
    ///     Thickness (negative = use the default) and lean for an entity from the explicit component, else the shape
    ///     rules of its prototype (nearest ancestor wins), else the component rules.
    /// </summary>
    public (float Thickness, bool Lean) GetShape(EntityUid uid)
    {
        if (_render3d.TryComp(uid, out var comp) && (comp.Thickness >= 0f || !comp.Lean))
            return (comp.Thickness, comp.Lean);

        if (!_meta.TryComp(uid, out var meta) || meta.EntityPrototype is not { } proto)
            return (-1f, true);

        if (!_shapeCache.TryGetValue(proto.ID, out var cached))
        {
            BuildRules();
            cached = null;
            foreach (var (ancestorId, _) in _protos.EnumerateAllParents<EntityPrototype>(proto.ID, includeSelf: true))
            {
                if (_shapeParents!.TryGetValue(ancestorId, out var shape))
                {
                    cached = shape;
                    break;
                }
            }

            _shapeCache[proto.ID] = cached;
        }

        if (cached is { } found)
            return found;

        if (_shapeComponents!.Count > 0)
        {
            foreach (var (thickness, lean, types) in _shapeComponents)
            {
                foreach (var type in types)
                {
                    if (_entMan.HasComponent(uid, type))
                        return (thickness, lean);
                }
            }
        }

        return (-1f, true);
    }

    public bool IsMob(EntityUid uid) => _mobs.HasComp(uid);

    public Render3DComponent? GetRender3D(EntityUid uid) => _render3d.TryComp(uid, out var c) ? c : null;

    /// <summary>
    ///     A light on an entity (lamps, emergency lights), whether it is switched on or not. Call it for wall-mounted
    ///     things: wall lamps are only wall-mounted by draw depth, they have no wall mount component.
    /// </summary>
    public bool TryGetFixture(EntityUid uid, out PointLightComponent light)
    {
        if (_lights.TryComp(uid, out var found))
        {
            light = found;
            return true;
        }

        light = default!;
        return false;
    }

    public bool IsThrown(EntityUid uid) => _thrown.HasComp(uid);

    public bool IsItem(EntityUid uid) => _items.HasComp(uid);

    public bool IsClosedDoor(EntityUid uid) => _doors.TryComp(uid, out var door) && door.State == DoorState.Closed;

    public WallMountComponent? GetWallMount(EntityUid uid) => _wallMounts.TryComp(uid, out var wm) ? wm : null;
}
