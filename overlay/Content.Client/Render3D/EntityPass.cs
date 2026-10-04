using System.Numerics;
using Content.Client.Clickable;
using Content.Shared.CCVar;
using Content.Shared.Render3D;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Render3D;

/// <summary>
///     Draws entities into the 3D view: gathers visible sprites, classifies them, packs their composited sprites into
///     the <see cref="BillboardAtlas"/> and turns them into depth-tested 3D quads (plan Phase 2).
/// </summary>
public sealed class EntityPass : IDisposable
{
    private readonly IEntityManager _entMan;
    private readonly IClyde _clyde;
    private readonly IPrototypeManager _protos;
    private readonly IConfigurationManager _cfg;
    private readonly SpriteSystem _sprite;
    private readonly SpriteTreeSystem _spriteTree;
    private readonly SharedTransformSystem _xform;
    private readonly TileWorldSystem _tileWorld;
    private readonly EntityClassifier _classifier;
    private readonly EntityQuery<ClickableComponent> _clickable;
    private readonly BillboardAtlas _atlas;
    private readonly QuadBuilder _quads = new();

    private const float MinPickDistance = 0.5f;

    private static readonly ProtoId<ShaderPrototype> EntityShader = "Render3DEntity";
    private static readonly ProtoId<ShaderPrototype> GlowShader = "Render3DGlowMask";
    private ShaderInstance? _glowShader;

    // effect switches, read once per frame in Prepare
    private bool _fxShadows;
    private bool _fxOutline;
    private bool _fxSharp;
    private bool _fxFixtures;
    private bool _curOutline;

    // shape of things (lean and thickness per category), read once per frame in Prepare
    private bool _fxItemLift, _fxItemLean, _fxItemThick;
    private bool _fxCharLean, _fxCharThick;
    private bool _fxObjectLean, _fxObjectThick;
    private int _thickLayers;
    private int _extraQuads;
    private float _downPitch;
    private Vector2 _fwd = Vector2.UnitY;

    /// <summary>Most extra quads (thickness layers) added per frame, so a crowded room cannot overload the draw.</summary>
    private const int MaxExtraQuads = 3000;

    /// <summary>How far an item on the ground is raised so its contact shadow shows around it.</summary>
    private const float ItemLiftHeight = 0.02f;

    /// <summary>Dev channel only (<c>r3d_glow off</c>): ignore the glow atlas, to compare against the plain lit result.</summary>
    public bool DebugNoGlow;

    private ShaderInstance? _shader;
    private EntityDraw3D[] _draws = new EntityDraw3D[512];

    /// <summary>The entities that were drawn last frame (see <see cref="ApplyCap"/>).</summary>
    private readonly HashSet<EntityUid> _wasDrawn = new();
    private int _drawCount;
    private readonly List<Entity<SpriteComponent, TransformComponent>> _query = new();
    private readonly HashSet<(EntityUid, int, int)> _surfaceTiles = new();

    /// <summary>A quad an entity is drawn as, kept for CPU picking.</summary>
    public struct PickQuad
    {
        public EntityUid Uid;
        public Vector3 P0, P1, P3;
        public Vector2 Pos;
    }

    private readonly List<PickQuad> _pick = new();
    private EntityUid _curUid;
    private bool _curClickable;
    private Vector2 _curPos;
    private bool _curTranslucent;
    private float _curSortBias;

    /// <summary>Entity drawn brighter (the crosshair hover highlight that replaces the 2D outline).</summary>
    public EntityUid? HighlightUid;

    /// <summary>The entities drawn this frame (nearest first when capped).</summary>
    public EntityDraw3D[] DrawArray => _draws;

    public int DrawCount => _drawCount;

    public int LastEntityCount { get; private set; }
    public int LastQuadCount => _quads.QuadCount;

    public EntityPass(IEntityManager entMan, IClyde clyde, IPrototypeManager protos, IConfigurationManager cfg)
    {
        _entMan = entMan;
        _clyde = clyde;
        _protos = protos;
        _cfg = cfg;
        _sprite = entMan.System<SpriteSystem>();
        _spriteTree = entMan.System<SpriteTreeSystem>();
        _xform = entMan.System<SharedTransformSystem>();
        _tileWorld = entMan.System<TileWorldSystem>();
        _classifier = new EntityClassifier(entMan, protos);
        _clickable = entMan.GetEntityQuery<ClickableComponent>();
        _atlas = new BillboardAtlas(clyde);
    }

    /// <summary>
    ///     Gathers and classifies entities around the camera and builds their quads. Must be followed by
    ///     <see cref="DrawAtlas"/> and <see cref="DrawQuads"/> in the same frame.
    /// </summary>
    public void Prepare(Camera3D cam, MapId mapId, float radius, EntityUid? hide)
    {
        _atlas.EnsureSize(_cfg.GetCVar(CCVars.Render3DAtlasSize));
        _atlas.BeginFrame();
        _fxShadows = _cfg.GetCVar(CCVars.Render3DFxShadows);
        _fxOutline = _cfg.GetCVar(CCVars.Render3DFxOutline);
        _fxSharp = _cfg.GetCVar(CCVars.Render3DFxSharp);
        _fxFixtures = _cfg.GetCVar(CCVars.Render3DFxFixtures);
        _fxItemLift = _cfg.GetCVar(CCVars.Render3DFxItemLift);
        _fxItemLean = _cfg.GetCVar(CCVars.Render3DFxItemLean);
        _fxItemThick = _cfg.GetCVar(CCVars.Render3DFxItemThick);
        _fxCharLean = _cfg.GetCVar(CCVars.Render3DFxCharLean);
        _fxCharThick = _cfg.GetCVar(CCVars.Render3DFxCharThick);
        _fxObjectLean = _cfg.GetCVar(CCVars.Render3DFxObjectLean);
        _fxObjectThick = _cfg.GetCVar(CCVars.Render3DFxObjectThick);
        _thickLayers = Math.Clamp(_cfg.GetCVar(CCVars.Render3DThicknessLayers), 0, 8);
        _extraQuads = 0;
        _downPitch = EntityShape.DownPitch(cam.Forward);
        _fwd = cam.ForwardGround;
        _quads.Clear();
        _pick.Clear();
        _drawCount = 0;
        _surfaceTiles.Clear();
        _tileWorld.ClosedDoors.Clear();
        _tileWorld.GlassTiles.Clear();

        if (mapId == MapId.Nullspace)
            return;

        var camXy = new Vector2(cam.Head.X, cam.Head.Y);
        var aabb = Box2.CenteredAround(camXy, new Vector2(radius * 2f, radius * 2f));
        _query.Clear();
        _spriteTree.QueryAabb(_query, mapId, aabb);

        var wallHeight = _cfg.GetCVar(CCVars.Render3DWallHeight);
        var tableHeight = _cfg.GetCVar(CCVars.Render3DTableHeight);
        var effectHeight = _cfg.GetCVar(CCVars.Render3DEffectHeight);
        var r2 = radius * radius;

        // pass 1: which tiles have a surface (table) on them, and which have a closed door (blocks the camera arm)
        foreach (var (uid, sprite, xform) in _query)
        {
            if (_classifier.IsClosedDoor(uid) && xform.GridUid is { } doorGrid && xform.ParentUid == doorGrid)
            {
                var dp = xform.LocalPosition;
                _tileWorld.ClosedDoors.Add((doorGrid, new Vector2i((int) MathF.Floor(dp.X), (int) MathF.Floor(dp.Y))));
            }

            if (!_classifier.IsSurface(uid))
                continue;
            if (xform.GridUid is { } grid && xform.ParentUid == grid)
            {
                var lp = xform.LocalPosition;
                _surfaceTiles.Add((grid, (int) MathF.Floor(lp.X), (int) MathF.Floor(lp.Y)));
            }
        }

        // pass 2: classify and collect
        foreach (var (uid, sprite, xform) in _query)
        {
            if (hide == uid || !sprite.Visible || sprite.Color.A <= 0.01f)
                continue;

            var mode = _classifier.Classify(uid, sprite, xform);
            if (mode == Render3DMode.Skip || mode == Render3DMode.Auto)
                continue;

            if (mode == Render3DMode.GlassBox && xform.GridUid is { } glassGrid && xform.ParentUid == glassGrid)
            {
                var gp = xform.LocalPosition;
                _tileWorld.GlassTiles.Add((glassGrid, new Vector2i((int) MathF.Floor(gp.X), (int) MathF.Floor(gp.Y))));
            }

            var (pos, rot) = _xform.GetWorldPositionRotation(xform);
            var d = pos - camXy;
            var dist2 = d.LengthSquared();
            if (dist2 > r2)
                continue;

            var local = _sprite.GetLocalBounds((uid, sprite));
            if (local.Width < 0.01f || local.Height < 0.01f)
                continue;

            if (_drawCount == _draws.Length)
                Array.Resize(ref _draws, _draws.Length * 2);

            ref var e = ref _draws[_drawCount++];
            e = default;
            e.Uid = uid;
            e.Sprite = sprite;
            e.Mode = mode;
            e.Pos = pos;
            e.WorldRot = rot;
            e.Distance2 = dist2;
            e.Bounds = local;
            e.Glow = HasGlowLayer(uid, sprite);

            e.Category = _classifier.IsMob(uid) ? EntityCategory.Character
                : _classifier.IsItem(uid) ? EntityCategory.Item
                : EntityCategory.Object;
            var shape = _classifier.GetShape(uid);
            e.Thickness = shape.Thickness >= 0f ? shape.Thickness : EntityShape.DefaultThickness(e.Category);
            e.CanLean = shape.Lean;

            var onTable = false;
            if (xform.GridUid is { } g && xform.ParentUid == g && _surfaceTiles.Count > 0)
            {
                var lp = xform.LocalPosition;
                onTable = _surfaceTiles.Contains((g, (int) MathF.Floor(lp.X), (int) MathF.Floor(lp.Y)));
            }

            switch (mode)
            {
                case Render3DMode.Billboard:
                    e.Z = _classifier.IsThrown(uid) ? 0.4f : onTable && _classifier.IsItem(uid) ? tableHeight : 0f;
                    e.DrawDirection = CameraYawMath.SpriteDirection(rot, camXy, pos);
                    // Optional pre-rendered 8-direction impostor in place of the live sprite
                    if (_classifier.GetRender3D(uid)?.Impostor is { } impostor)
                    {
                        try
                        {
                            e.Impostor = _sprite.RsiStateLike(impostor).TextureFor(e.DrawDirection.Value);
                        }
                        catch (Exception)
                        {
                            e.Impostor = null; // missing state: fall back to the live sprite
                        }
                    }

                    break;
                case Render3DMode.FlatFloor:
                    e.Z = (onTable ? tableHeight + 0.01f : 0.01f)
                        + (_fxItemLift && e.Category == EntityCategory.Item ? ItemLiftHeight : 0f);
                    e.DrawRotated = true;
                    e.Bounds = RotatedBounds(local, rot);
                    break;
                case Render3DMode.FlatAir:
                    e.Z = effectHeight;
                    e.DrawRotated = true;
                    e.Bounds = RotatedBounds(local, rot);
                    break;
                case Render3DMode.TableBox:
                    e.Z = tableHeight;
                    e.DrawRotated = true;
                    e.Bounds = RotatedBounds(local, rot);
                    break;
                default:
                    // Panel, EdgePanel, GlassBox, WallDecal: the front of the live sprite
                    e.DrawDirection = Direction.South;
                    break;
            }
        }

        // nearest first, capped
        var cap = Math.Max(16, _cfg.GetCVar(CCVars.Render3DBillboardCap));
        _drawCount = ApplyCap(_draws, _drawCount, cap, _wasDrawn, Math.Clamp(_cfg.GetCVar(CCVars.Render3DCapHysteresis), 0.1f, 1f));

        // slots
        for (var i = 0; i < _drawCount; i++)
        {
            ref var e = ref _draws[i];
            if (!_atlas.TryAllocate(e.Bounds, out e.Slot, out e.SlotOrigin))
                break;
            e.Placed = true;
        }

        LastEntityCount = _drawCount;

        // quads
        var atlasSize = (float) _atlas.Size;
        for (var i = 0; i < _drawCount; i++)
        {
            ref var e = ref _draws[i];
            if (!e.Placed)
                continue;

            BuildQuads(ref e, cam, atlasSize, wallHeight, tableHeight);
        }
    }

    /// <summary>
    ///     True when the sprite has a visible unshaded layer and no layer with its own shader (a layer shader would reset
    ///     the glow-mask shader part way through drawing, so those sprites keep the old, lit-glow behaviour).
    /// </summary>
    private bool HasGlowLayer(EntityUid uid, SpriteComponent sprite)
    {
        var glow = false;
        for (var i = 0; _sprite.TryGetLayer((uid, sprite), i, out var layer, false); i++)
        {
            if (!layer.Visible)
                continue;

            if (layer.ShaderPrototype == SpriteSystem.UnshadedId)
                glow = true;
            else if (layer.Shader != null)
                return false;
        }

        return glow;
    }

    private void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
        Vector2 foot, float shade = 1f, float alpha = 1f, bool blend = false, bool pick = true)
    {
        if (HighlightUid == _curUid)
            shade *= 1.6f;
        var translucent = blend || _curTranslucent;
        _quads.Add(p0, p1, p2, p3, uv0, uv1, uv2, uv3, foot, shade, alpha, translucent,
            flags: translucent ? 1f : _curOutline ? 0.5f : 0f, sortBias: _curSortBias);
        if (pick && _curClickable)
            _pick.Add(new PickQuad { Uid = _curUid, P0 = p0, P1 = p1, P3 = p3, Pos = _curPos });
    }

    /// <summary>
    ///     Finds the entities whose quads the ray hits (sprite bounds shrunk by 10%), nearest first. Each entity
    ///     appears once with its nearest hit.
    /// </summary>
    public void Pick(Vector3 origin, Vector3 dir, List<(EntityUid Uid, float T, Vector2 Pos)> results)
    {
        results.Clear();
        foreach (var q in _pick)
        {
            if (!Picking3D.RayQuad(origin, dir, q.P0, q.P1, q.P3, Picking3D.ShrinkFraction, out var t))
                continue;

            // Things this close to the camera are faded out of view (entity.swsl), so they must not eat clicks either.
            if (t < MinPickDistance)
                continue;

            var existing = results.FindIndex(r => r.Uid == q.Uid);
            if (existing >= 0)
            {
                if (t < results[existing].T)
                    results[existing] = (q.Uid, t, q.Pos);
                continue;
            }

            results.Add((q.Uid, t, q.Pos));
        }

        results.Sort((a, b) => a.T.CompareTo(b.T));
    }

    private static Box2 RotatedBounds(Box2 local, Angle rot)
    {
        return new Box2Rotated(local, rot, Vector2.Zero).CalcBoundingBox();
    }

    private void BuildQuads(ref EntityDraw3D e, Camera3D cam, float atlasSize, float wallHeight, float tableHeight)
    {
        var slot = e.Slot;
        const float inset = 0.5f;
        var u0 = (slot.Left + inset) / atlasSize;
        var u1 = (slot.Right - inset) / atlasSize;
        var vTop = 1f - (slot.Top + inset) / atlasSize;
        var vBot = 1f - (slot.Bottom - inset) / atlasSize;
        var uvTl = new Vector2(u0, vTop);
        var uvTr = new Vector2(u1, vTop);
        var uvBr = new Vector2(u1, vBot);
        var uvBl = new Vector2(u0, vBot);

        var pos = e.Pos;
        var b = e.Bounds;
        _curUid = e.Uid;
        _curPos = pos;
        _curClickable = _clickable.HasComp(e.Uid);
        // translucent sprites (construction and placement ghosts, fading effects) are alpha blended, not alpha tested
        _curTranslucent = e.Sprite.Color.A < 0.99f;
        // Things that share a plane (a window and the grille in the same tile, a table and what stands on it) are
        // drawn in sprite draw depth order, not in whatever order the sort leaves ties in, which flickered.
        _curSortBias = -SortBiasPerDepth * e.Sprite.DrawDepth;
        _curOutline = (_fxOutline || _fxItemLift && e.Category == EntityCategory.Item)
            && !_curTranslucent && e.Mode is Render3DMode.Billboard or Render3DMode.FlatFloor;
        switch (e.Mode)
        {
            case Render3DMode.Billboard:
            {
                var right = new Vector2(cam.Right.X, cam.Right.Y);
                if (right.LengthSquared() < 1e-6f)
                    right = Vector2.UnitX;
                right = Vector2.Normalize(right);
                var l = pos + right * b.Left;
                var r = pos + right * b.Right;
                var zb = e.Z;
                var zt = e.Z + b.Height;
                var lean = LeanOn(ref e) ? EntityShape.StandLean(_downPitch) : 0f;
                var layers = LayerCountFor(ref e);
                if (lean > 0.005f)
                {
                    AddCard(ref e,
                        EntityShape.LeanStandPoint(l, zb, b.Height, _fwd, lean), EntityShape.LeanStandPoint(r, zb, b.Height, _fwd, lean),
                        new Vector3(r, zb), new Vector3(l, zb),
                        uvTl, uvTr, uvBr, uvBl, pos, EntityShape.StandNormal(_fwd, lean), layers);
                }
                else
                {
                    AddCard(ref e, new Vector3(l, zt), new Vector3(r, zt), new Vector3(r, zb), new Vector3(l, zb),
                        uvTl, uvTr, uvBr, uvBl, pos, new Vector3(-_fwd, 0f), layers);
                }

                if (_fxShadows && !_curTranslucent && e.Distance2 < ShadowRange * ShadowRange)
                    AddShadow(ref e, tableHeight);
                break;
            }

            case Render3DMode.FlatFloor:
            {
                if (_fxItemLift && e.Category == EntityCategory.Item && !_curTranslucent && e.Distance2 < ShadowRange * ShadowRange)
                    AddItemShadow(ref e, tableHeight);

                var lean = LeanOn(ref e) ? EntityShape.FlatLean(_downPitch) : 0f;
                var layers = LayerCountFor(ref e);
                var z = e.Z + (layers > 0 ? e.Thickness : 0f);
                if (lean > 0.01f)
                {
                    // corners relative to the centre, tilted about the edge nearest the camera
                    var o0 = new Vector2(b.Left, b.Top);
                    var o1 = new Vector2(b.Right, b.Top);
                    var o2 = new Vector2(b.Right, b.Bottom);
                    var o3 = new Vector2(b.Left, b.Bottom);
                    var s0 = Vector2.Dot(o0, _fwd);
                    var s1 = Vector2.Dot(o1, _fwd);
                    var s2 = Vector2.Dot(o2, _fwd);
                    var s3 = Vector2.Dot(o3, _fwd);
                    var sMin = MathF.Min(MathF.Min(s0, s1), MathF.Min(s2, s3));
                    var sMax = MathF.Max(MathF.Max(s0, s1), MathF.Max(s2, s3));

                    AddCard(ref e,
                        PlaceOn(pos, EntityShape.LeanFlatPoint(o0, z, _fwd, sMin, sMax, lean)),
                        PlaceOn(pos, EntityShape.LeanFlatPoint(o1, z, _fwd, sMin, sMax, lean)),
                        PlaceOn(pos, EntityShape.LeanFlatPoint(o2, z, _fwd, sMin, sMax, lean)),
                        PlaceOn(pos, EntityShape.LeanFlatPoint(o3, z, _fwd, sMin, sMax, lean)),
                        uvTl, uvTr, uvBr, uvBl, pos, EntityShape.FlatNormal(_fwd, lean), layers);
                }
                else
                {
                    var l = pos.X + b.Left;
                    var r = pos.X + b.Right;
                    var bt = pos.Y + b.Bottom;
                    var tp = pos.Y + b.Top;
                    AddCard(ref e, new Vector3(l, tp, z), new Vector3(r, tp, z), new Vector3(r, bt, z), new Vector3(l, bt, z),
                        uvTl, uvTr, uvBr, uvBl, pos, Vector3.UnitZ, layers);
                }

                break;
            }

            case Render3DMode.FlatAir:
            {
                AddFlat(pos, b, e.Z, uvTl, uvTr, uvBr, uvBl, pos, 1f);
                break;
            }

            case Render3DMode.TableBox:
            {
                var half = 0.5f;
                var tb = new Box2(-half, -half, half, half);
                AddFlat(pos, tb, tableHeight, uvTl, uvTr, uvBr, uvBl, pos, 1f);
                // sides: a thin strip of the same art, darkened
                var sTop = Vector2.Lerp(uvTl, uvBl, 0.80f);
                var sBot = Vector2.Lerp(uvTl, uvBl, 0.95f);
                var sTopR = Vector2.Lerp(uvTr, uvBr, 0.80f);
                var sBotR = Vector2.Lerp(uvTr, uvBr, 0.95f);
                AddVerticalFace(pos + new Vector2(0, half), Vector2.UnitX, 1f, 0f, tableHeight, sTop, sTopR, sBotR, sBot, pos, 0.55f);
                AddVerticalFace(pos + new Vector2(0, -half), Vector2.UnitX, 1f, 0f, tableHeight, sTop, sTopR, sBotR, sBot, pos, 0.55f);
                AddVerticalFace(pos + new Vector2(half, 0), Vector2.UnitY, 1f, 0f, tableHeight, sTop, sTopR, sBotR, sBot, pos, 0.55f);
                AddVerticalFace(pos + new Vector2(-half, 0), Vector2.UnitY, 1f, 0f, tableHeight, sTop, sTopR, sBotR, sBot, pos, 0.55f);
                break;
            }

            case Render3DMode.Panel:
            {
                var axis = PanelAxis(ref e);
                AddVerticalFace(pos, axis, 1f, 0f, wallHeight, uvTl, uvTr, uvBr, uvBl, pos, 1f);
                break;
            }

            case Render3DMode.EdgePanel:
            {
                var f = e.WorldRot.ToWorldVec();
                var axis = new Vector2(-f.Y, f.X);
                AddVerticalFace(pos + f * 0.46f, axis, 1f, 0f, wallHeight * 0.85f, uvTl, uvTr, uvBr, uvBl, pos, 1f, blend: true);
                break;
            }

            case Render3DMode.GlassBox:
            {
                var ux = e.WorldRot.RotateVec(Vector2.UnitX);
                var uy = e.WorldRot.RotateVec(Vector2.UnitY);
                const float h = 0.49f;
                // Only the faces turned towards the camera are drawn. The far faces are seen through the near ones
                // (the glass is see-through), which doubled every window and grille pattern and made it shimmer as the
                // two copies moved against each other.
                var camXy = new Vector2(cam.Position.X, cam.Position.Y);
                if (Vector2.Dot(camXy - (pos + uy * h), uy) > 0f)
                    AddVerticalFace(pos + uy * h, ux, 1f, 0f, wallHeight, uvTl, uvTr, uvBr, uvBl, pos, 1f, blend: true);
                if (Vector2.Dot(camXy - (pos - uy * h), -uy) > 0f)
                    AddVerticalFace(pos - uy * h, ux, 1f, 0f, wallHeight, uvTl, uvTr, uvBr, uvBl, pos, 1f, blend: true);
                if (Vector2.Dot(camXy - (pos + ux * h), ux) > 0f)
                    AddVerticalFace(pos + ux * h, uy, 1f, 0f, wallHeight, uvTl, uvTr, uvBr, uvBl, pos, 1f, blend: true);
                if (Vector2.Dot(camXy - (pos - ux * h), -ux) > 0f)
                    AddVerticalFace(pos - ux * h, uy, 1f, 0f, wallHeight, uvTl, uvTr, uvBr, uvBl, pos, 1f, blend: true);
                break;
            }

            case Render3DMode.WallDecal:
            {
                var facing = e.WorldRot;
                if (_classifier.GetWallMount(e.Uid) is { } mount)
                    facing += mount.Direction;
                var f = facing.ToWorldVec();
                var center = DecalCenter(ref e, f, out var onWall);
                e.OnWall = onWall;

                var axis = new Vector2(-f.Y, f.X);
                var height = Math.Clamp(b.Height, 0.2f, 1.0f);
                var width = Math.Clamp(b.Width, 0.2f, 1.0f);
                // lamps hang near the ceiling; everything else (posters, buttons, APCs) sits at eye level. This holds at every
                // quality level and for lamps on windows, grilles and doors too (they have no wall in the wall map, onWall is
                // false): drawn at eye level the glowing tube sat just above the floor, where such a thin strip flickered
                // against the floor and the wall behind it and was drawn over the feet of characters. The "fixtures" effect
                // only adds the glow halo.
                PointLightComponent? light = null;
                var fixture = _classifier.TryGetFixture(e.Uid, out light);
                var zMid = fixture ? wallHeight - 0.3f : 0.7f;
                var zb = zMid - height * 0.5f;
                AddVerticalFace(center, axis, width, zb, zb + height, uvTl, uvTr, uvBr, uvBl, center + f * 0.6f, 1f);
                if (fixture && _fxFixtures && light!.Enabled)
                    AddHalo(center + f * 0.06f, axis, zMid, center + f * 0.55f);
                break;
            }
        }
    }

    private const float ShadowRange = 14f;

    /// <summary>Sort bias per unit of sprite draw depth (tiles): far too small to reorder things that are not on the same plane.</summary>
    private const float SortBiasPerDepth = 0.0004f;

    /// <summary>Moves a point given relative to the entity (xy) onto its world position.</summary>
    private static Vector3 PlaceOn(Vector2 pos, Vector3 relative) => new(pos.X + relative.X, pos.Y + relative.Y, relative.Z);

    private bool LeanOn(ref EntityDraw3D e) => e.CanLean && e.Category switch
    {
        EntityCategory.Item => _fxItemLean,
        EntityCategory.Character => _fxCharLean,
        _ => _fxObjectLean,
    };

    /// <summary>How many layers of thickness to give this entity this frame (0 = flat).</summary>
    private int LayerCountFor(ref EntityDraw3D e)
    {
        var on = e.Category switch
        {
            EntityCategory.Item => _fxItemThick,
            EntityCategory.Character => _fxCharThick,
            _ => _fxObjectThick,
        };

        if (!on || e.Thickness < 0.01f || _curTranslucent || _extraQuads >= MaxExtraQuads)
            return 0;

        return EntityShape.LayerCount(_thickLayers, MathF.Sqrt(e.Distance2));
    }

    /// <summary>
    ///     Adds a sprite card (its front face) and, when <paramref name="layers"/> is above zero, copies of it behind
    ///     the face along <paramref name="normal"/>, darker with depth, so the thing has sides like a small slab.
    ///     Only the front face can be picked and gets the outline.
    /// </summary>
    private void AddCard(ref EntityDraw3D e, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 foot, Vector3 normal, int layers = 0)
    {
        AddQuad(p0, p1, p2, p3, uv0, uv1, uv2, uv3, foot);
        if (layers <= 0)
            return;

        var outline = _curOutline;
        _curOutline = false;
        var step = e.Thickness / layers;
        for (var k = layers; k >= 1; k--)
        {
            var off = -normal * (step * k);
            AddQuad(p0 + off, p1 + off, p2 + off, p3 + off, uv0, uv1, uv2, uv3, foot,
                EntityShape.LayerShade(k, layers), pick: false);
            _extraQuads++;
        }

        _curOutline = outline;
    }

    /// <summary>A small soft shadow around an item lying on the floor (or table), which lifts it off the ground image.</summary>
    private void AddItemShadow(ref EntityDraw3D e, float tableHeight)
    {
        var radius = Math.Clamp(MathF.Min(e.Bounds.Width, e.Bounds.Height) * 0.4f, 0.14f, 0.36f);
        var onTable = e.Z > tableHeight - 0.02f;
        var z = (onTable ? tableHeight : 0f) + 0.015f;
        var c = e.Pos;
        _quads.Add(
            new Vector3(c.X - radius, c.Y + radius, z), new Vector3(c.X + radius, c.Y + radius, z),
            new Vector3(c.X + radius, c.Y - radius, z), new Vector3(c.X - radius, c.Y - radius, z),
            new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
            c, 1f, 0.5f, false, flags: 2f, sortBias: 0.6f);
    }

    /// <summary>A soft glow on the wall around a lamp (drawn over the lamp, not clickable).</summary>
    private void AddHalo(Vector2 center, Vector2 axis, float zMid, Vector2 lightAt)
    {
        const float half = 0.75f;
        var l = center - axis * half;
        var r = center + axis * half;
        _quads.Add(
            new Vector3(l, zMid + half), new Vector3(r, zMid + half), new Vector3(r, zMid - half), new Vector3(l, zMid - half),
            new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
            lightAt, 1f, 0.34f, true, flags: 3f, sortBias: -0.08f);
    }

    /// <summary>
    ///     A soft dark spot on the surface under a standing entity (also under a thrown item in flight, which shows how
    ///     high it is). It is drawn before the sprite and is not clickable.
    /// </summary>
    private void AddShadow(ref EntityDraw3D e, float tableHeight)
    {
        var radius = Math.Clamp(e.Bounds.Width * 0.55f, 0.28f, 0.7f);
        var z = (MathF.Abs(e.Z - tableHeight) < 0.02f ? tableHeight : 0f) + 0.02f;
        var c = e.Pos;
        _quads.Add(
            new Vector3(c.X - radius, c.Y + radius, z), new Vector3(c.X + radius, c.Y + radius, z),
            new Vector3(c.X + radius, c.Y - radius, z), new Vector3(c.X - radius, c.Y - radius, z),
            new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1),
            c, 1f, 0.55f, false, flags: 2f, sortBias: 0.6f);
    }

    private void AddFlat(Vector2 pos, Box2 b, float z, Vector2 uvTl, Vector2 uvTr, Vector2 uvBr, Vector2 uvBl, Vector2 foot, float shade)
    {
        var l = pos.X + b.Left;
        var r = pos.X + b.Right;
        var bt = pos.Y + b.Bottom;
        var tp = pos.Y + b.Top;
        AddQuad(new Vector3(l, tp, z), new Vector3(r, tp, z), new Vector3(r, bt, z), new Vector3(l, bt, z),
            uvTl, uvTr, uvBr, uvBl, foot, shade);
    }

    /// <summary>A vertical quad centred on <paramref name="center"/>, spanning <paramref name="width"/> along <paramref name="axis"/>.</summary>
    private void AddVerticalFace(Vector2 center, Vector2 axis, float width, float zBottom, float zTop,
        Vector2 uvTl, Vector2 uvTr, Vector2 uvBr, Vector2 uvBl, Vector2 foot, float shade, bool blend = false)
    {
        var half = axis * (width * 0.5f);
        var l = center - half;
        var r = center + half;
        AddQuad(new Vector3(l, zTop), new Vector3(r, zTop), new Vector3(r, zBottom), new Vector3(l, zBottom),
            uvTl, uvTr, uvBr, uvBl, foot, shade, 1f, blend);
    }

    /// <summary>World direction a door-like panel spans, from neighbouring walls, else from the entity rotation.</summary>
    private Vector2 PanelAxis(ref EntityDraw3D e)
    {
        var xform = _entMan.GetComponent<TransformComponent>(e.Uid);
        if (xform.GridUid is { } grid && xform.ParentUid == grid)
        {
            var lp = xform.LocalPosition;
            var tile = new Vector2i((int) MathF.Floor(lp.X), (int) MathF.Floor(lp.Y));
            var wallX = _tileWorld.TryGetWall(grid, tile + new Vector2i(1, 0)) || _tileWorld.TryGetWall(grid, tile + new Vector2i(-1, 0));
            var wallY = _tileWorld.TryGetWall(grid, tile + new Vector2i(0, 1)) || _tileWorld.TryGetWall(grid, tile + new Vector2i(0, -1));
            var gridRot = _xform.GetWorldRotation(grid);

            if (wallX && !wallY)
                return gridRot.RotateVec(Vector2.UnitX);
            if (wallY && !wallX)
                return gridRot.RotateVec(Vector2.UnitY);

            // no clear answer: use the entity rotation relative to the grid
            var rel = (e.WorldRot - gridRot).Reduced().FlipPositive();
            var quarter = (int) Math.Round(rel.Theta / (Math.PI / 2)) & 3;
            return gridRot.RotateVec(quarter % 2 == 0 ? Vector2.UnitX : Vector2.UnitY);
        }

        return e.WorldRot.RotateVec(Vector2.UnitX);
    }

    /// <summary>Centre of the quad of a wall mounted entity: hugging the face of the wall behind or around it.</summary>
    private Vector2 DecalCenter(ref EntityDraw3D e, Vector2 facing, out bool onWall)
    {
        onWall = true;
        var xform = _entMan.GetComponent<TransformComponent>(e.Uid);
        if (xform.GridUid is { } grid && xform.ParentUid == grid)
        {
            var gridInv = _xform.GetInvWorldMatrix(grid);
            var behind = Vector2.Transform(e.Pos - facing, gridInv);
            var here = Vector2.Transform(e.Pos, gridInv);
            var behindTile = new Vector2i((int) MathF.Floor(behind.X), (int) MathF.Floor(behind.Y));
            var hereTile = new Vector2i((int) MathF.Floor(here.X), (int) MathF.Floor(here.Y));

            if (_tileWorld.TryGetWall(grid, behindTile) && !_tileWorld.TryGetWall(grid, hereTile))
                return e.Pos - facing * 0.48f;

            if (_tileWorld.TryGetWall(grid, hereTile))
                return e.Pos + facing * 0.52f;
        }

        // No wall entity found: most of these hang on windows, grilles or doors in the tile behind (those are not in
        // the wall map), so put the quad against the edge behind the entity.
        onWall = false;
        return e.Pos - facing * 0.48f;
    }

    /// <summary>Developer aid: logs what was drawn last frame (prototype, mode, position, height, bounds).</summary>
    public void Dump(ISawmill log, int max = 500)
    {
        log.Info($"entity pass: {_drawCount} entries, {_quads.QuadCount} quads");
        for (var i = 0; i < Math.Min(_drawCount, max); i++)
        {
            ref var e = ref _draws[i];
            var proto = _entMan.TryGetComponent(e.Uid, out MetaDataComponent? meta) ? meta.EntityPrototype?.ID : "?";
            if (e.Mode == Render3DMode.WallDecal)
            {
                var xf = _entMan.GetComponent<TransformComponent>(e.Uid);
                var walls = "";
                if (xf.GridUid is { } g && xf.ParentUid == g)
                {
                    var lp = xf.LocalPosition;
                    var t = new Vector2i((int) MathF.Floor(lp.X), (int) MathF.Floor(lp.Y));
                    walls = $" here={_tileWorld.TryGetWall(g, t)} N={_tileWorld.TryGetWall(g, t + new Vector2i(0, 1))} E={_tileWorld.TryGetWall(g, t + new Vector2i(1, 0))} S={_tileWorld.TryGetWall(g, t + new Vector2i(0, -1))} W={_tileWorld.TryGetWall(g, t + new Vector2i(-1, 0))} gridRot={_xform.GetWorldRotation(g).Degrees:F0}";
                }

                var mount = _classifier.GetWallMount(e.Uid);
                log.Info($"    decal {proto}: worldRot={e.WorldRot.Degrees:F0} mountDir={(mount == null ? "none" : mount.Direction.Degrees.ToString("F0"))} localRot={xf.LocalRotation.Degrees:F0}{walls}");
            }

            log.Info($"  {i}: {proto} {e.Uid} net={_entMan.GetNetEntity(e.Uid)} mode={e.Mode}{(e.Mode == Render3DMode.WallDecal ? (e.OnWall ? "/wall" : "/edge") : "")}{(e.Mode == Render3DMode.WallDecal && _classifier.TryGetFixture(e.Uid, out var fx) ? $" fixture(enabled={fx.Enabled})" : "")} pos={e.Pos} z={e.Z} bounds={e.Bounds} slot={e.Slot} dir={e.DrawDirection} d2={e.Distance2:F1}");
        }
    }

    /// <summary>Draws the sprites into the atlas. Call from inside a control's Draw.</summary>
    public void DrawAtlas(DrawingHandleScreen screen)
    {
        _glowShader ??= _protos.Index(GlowShader).InstanceUnique();
        _atlas.Draw(screen, _draws, _drawCount, _glowShader);
    }

    /// <summary>
    ///     Draws the prepared quads into the current render target (the scene composite), depth-tested against
    ///     <paramref name="sceneTexture"/> (alpha = planar depth).
    /// </summary>
    public void DrawQuads(DrawingHandleScreen screen, Camera3D cam, Texture sceneTexture, GroundLayer ground, Vector2i sceneSize)
    {
        if (_atlas.Texture == null || _quads.QuadCount == 0)
            return;

        _shader ??= _protos.Index(EntityShader).InstanceUnique();
        var sh = _shader;
        sh.SetParameter("sceneTex", sceneTexture);
        if (_atlas.GlowTexture is { } glow)
            sh.SetParameter("glowTex", glow);
        sh.SetParameter("glowOn", DebugNoGlow ? 0f : 1f);
        if (ground.LightTarget?.Texture is { } light)
            sh.SetParameter("lightTex", light);
        if (ground.FovTarget?.Texture is { } fov)
            sh.SetParameter("fovTex", fov);
        sh.SetParameter("viewSize", new Vector2(sceneSize.X, sceneSize.Y));
        sh.SetParameter("atlasSize", new Vector2(_atlas.Size, _atlas.Size));
        sh.SetParameter("sharpOn", _fxSharp ? 1f : 0f);
        sh.SetParameter("groundParams", new Vector4(ground.Center.X, ground.Center.Y, ground.WorldSize, 1f / ground.WorldSize));
        sh.SetParameter("fovParams", new Vector4(ground.Center.X, ground.Center.Y, ground.FovTarget != null && ground.FovEnabled ? 1f : 0f, -0.75f / 32f));

        _quads.Build(cam, out var vertices, out var indices);
        if (indices.Length == 0)
            return;

        screen.UseShader(sh);
        screen.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _atlas.Texture, indices.Span, vertices.Span);
        screen.UseShader(null);
    }

    public void Dispose()
    {
        _atlas.Dispose();
    }

    /// <summary>
    ///     Keeps the nearest <paramref name="cap"/> of the first <paramref name="count"/> entries (moved to the front) and
    ///     returns how many remain. An entity that was drawn last frame counts as closer (0.6 on the squared distance,
    ///     about 23% on the distance), so the ring where the cut falls does not flicker: with the plain nearest-N rule, the
    ///     distance of the N-th entity changes as the camera moves, and everything near that distance dropped out and came
    ///     back from frame to frame. <paramref name="wasDrawn"/> is replaced by the entities kept.
    /// </summary>
    public static int ApplyCap(EntityDraw3D[] draws, int count, int cap, HashSet<EntityUid> wasDrawn, float hysteresis = DefaultCapHysteresis)
    {
        if (count > cap)
        {
            for (var i = 0; i < count; i++)
                draws[i].CapKey = draws[i].Distance2 * (wasDrawn.Contains(draws[i].Uid) ? hysteresis : 1f);

            Array.Sort(draws, 0, count, CapKeyComparer.Instance);
            count = cap;
        }

        wasDrawn.Clear();
        for (var i = 0; i < count; i++)
            wasDrawn.Add(draws[i].Uid);

        return count;
    }

    private const float DefaultCapHysteresis = 0.6f;

    private sealed class CapKeyComparer : IComparer<EntityDraw3D>
    {
        public static readonly CapKeyComparer Instance = new();

        public int Compare(EntityDraw3D x, EntityDraw3D y) => x.CapKey.CompareTo(y.CapKey);
    }

    private sealed class DistanceComparer : IComparer<EntityDraw3D>
    {
        public static readonly DistanceComparer Instance = new();

        public int Compare(EntityDraw3D x, EntityDraw3D y) => x.Distance2.CompareTo(y.Distance2);
    }
}
