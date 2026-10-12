using System.Globalization;
using System.Linq;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;
using Content.Client.Interactable.Components;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Content.Shared.IdentityManagement;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>
///     The 3D main viewport. Owns the frame (ground capture, raymarch, entity quads, present), the camera and the
///     mapping between screen pixels and world positions that all of SS14's input code goes through
///     (<see cref="IViewportControl"/>).
/// </summary>
public sealed partial class Render3DViewportControl : Control, IViewportControl
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IStateManager _states = default!;
    [Dependency] private IResourceCache _resCache = default!;

    /// <summary>Radians of look per pixel of mouse movement at sensitivity 1.</summary>
    private const float RadiansPerPixel = 0.12f * MathF.PI / 180f;

    /// <summary>How much faster the middle mouse drag turns the high cameras than the mouse look turns the walking ones (a turn takes about a screen width of dragging).</summary>
    private const float DragTurnGain = 5f;

    private static readonly ProtoId<ShaderPrototype> RaymarchShader = "Render3DRaymarch";
    private static readonly ProtoId<ShaderPrototype> PresentShader = "Render3DPresent";
    private static readonly ProtoId<ShaderPrototype> ResolveShader = "Render3DResolve";

    private GroundLayer? _ground;
    private TileWorldSystem? _tileWorld;
    private CameraYawSystem? _yawSystem;
    private SharedTransformSystem? _xformSys;
    private IRenderTexture? _scene;
    private IRenderTexture? _composite;
    private IRenderTexture? _final;
    private EntityPass? _entityPass;
    private PostFx? _post;
    private ShaderInstance? _raymarch;
    private ShaderInstance? _present;
    private ShaderInstance? _resolve;

    private ISawmill? _sawmill;
    private TimeSpan _nextErrorLog;
    private TimeSpan _nextStatLog;

    private EntityUid? _controlled;

    // developer timing (CPU side, milliseconds, exponentially averaged); logged with the dev stats line
    private readonly System.Diagnostics.Stopwatch _sw = new();
    private double _msGround, _msPrepare, _msAtlas, _msScene, _msUi;
    private static double Avg(double old, double now) => old * 0.9 + now * 0.1;
    private void Lap(ref double accum)
    {
        accum = Avg(accum, _sw.Elapsed.TotalMilliseconds);
        _sw.Restart();
    }
    private float _cameraToHead = 10f;
    private float _armFraction = 1f;
    private Font? _labelFont;

    // head bob (first person, off by default): the eye rises and falls with the distance walked
    private Vector2 _bobLast;
    private float _bobPhase;
    private float _bobSpeed;

    /// <summary>Vertical eye offset for a gentle walking bob; zero when the effect is off, standing still, or in third person.</summary>
    private float HeadBob(Vector2 eyePos, bool firstPerson)
    {
        var dt = MathF.Max((float) _timing.FrameTime.TotalSeconds, 0.0001f);
        var moved = (eyePos - _bobLast).Length();
        _bobLast = eyePos;

        // a teleport or a camera change is not walking
        var speed = moved > 1f ? 0f : moved / dt;
        _bobSpeed += (MathF.Min(speed, 6f) - _bobSpeed) * MathF.Min(dt * 8f, 1f);

        if (!firstPerson || !_cfg.GetCVar(CCVars.Render3DFxHeadBob))
            return 0f;

        _bobPhase += _bobSpeed * dt * 2.6f;
        return MathF.Sin(_bobPhase * MathF.PI) * 0.022f * Math.Clamp(_bobSpeed / 4f, 0f, 1f);
    }
    private string? _notice;
    private TimeSpan _noticeUntil;

    /// <summary>Shows a short message at the top of the 3D view for a few seconds (for example: quality was lowered).</summary>
    public void ShowNotice(string text, float seconds = 8f)
    {
        _notice = text;
        _noticeUntil = _timing.RealTime + TimeSpan.FromSeconds(seconds);
    }

    private void DrawNotice(DrawingHandleScreen screen, Vector2i pixelSize)
    {
        if (_notice == null || _timing.RealTime > _noticeUntil)
            return;

        _labelFont ??= new VectorFont(_resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
        var dims = screen.GetDimensions(_labelFont, _notice, 1f);
        var pos = new Vector2(pixelSize.X / 2f - dims.X / 2f, pixelSize.Y * 0.72f);
        var pad = new Vector2(10, 6);
        screen.DrawRect(UIBox2.FromDimensions(pos - pad, dims + pad * 2), new Color(0f, 0f, 0f, 0.6f));
        screen.DrawString(_labelFont, pos, _notice, Color.White);
    }

    public Camera3D Camera { get; } = new();

    /// <summary>A camera high above the character is on (overhead, or the dynamic view once it is high): no ceiling, low walls with tops, a free cursor.</summary>
    public bool Elevated => Camera.Elevated;

    /// <summary>Overhead and isometric views: how many quarter turns the view is turned from "the grid's north (or its diagonal) is up".</summary>
    public int OverheadSteps;

    private bool _overheadSnap = true;

    // overhead camera: how far the player has turned it from the grid's heading, by dragging or with the rotate keys (radians); the
    // look-down angle the drag sets is the overhead pitch setting itself
    private double _overheadTurn;

    // dynamic view, high up: how far the player has turned the picture from the grid's heading by dragging (radians); see DynamicView.TurnWeight
    private double _scrollTurn;
    private bool _turnDragging;
    private CameraMode _lastMode = CameraMode.ThirdPerson;

    // dynamic view: where the wheel wants the camera (0 first person to 1 the classic 2D view) and where it has glided to
    private float _scrollTarget = DynamicView.ThirdStop;
    private float _scrollVelocity;
    private bool _scrollInit;
    private bool _scrollFree;
    private bool _scrollNoCeiling;
    private bool _scrollAllow2D;
    private float _scrollBias = float.NaN;
    private DynamicViewState _scrollView;

    /// <summary>The dynamic view's position on its way from first person (0) to the classic 2D view (1), as it is drawn now.</summary>
    public float ScrollU { get; private set; } = DynamicView.ThirdStop;

    /// <summary>The dynamic view has reached the classic 2D view: the controller shows the 2D viewport instead of this one.</summary>
    public bool ScrollTwoD { get; private set; }

    /// <summary>How much of the classic 2D view is blended over this one (0 to 1), near the end of the scroll.</summary>
    public float ScrollFade { get; private set; }

    /// <summary>Set by the controller: how many tiles tall the classic 2D view is, so the straight-down view can match it.</summary>
    public float TwoDTiles = 15f;

    // Set by the controller: the shape of the real 2D viewport (its size, how it is fitted into the window), so the cross-fade can draw the
    // classic picture exactly where the real one will appear.
    public Vector2i TwoDViewportSize;
    public Vector2i? TwoDFixedStretch;
    public int TwoDIgnoreDimension; // 0 none, 1 horizontal, 2 vertical (ScalingViewportIgnoreDimension)
    public bool TwoDBilinear = true;
    private IClydeViewport? _twoDViewport;

    /// <summary>The dynamic view is shown from its walking end (first / third person): the mouse looks around with the crosshair.</summary>
    public bool ScrollWalking => Camera.Mode == CameraMode.DynamicView && !_scrollFree;

    /// <summary>
    ///     Moves the dynamic view with the mouse wheel (<paramref name="notches"/> positive = the wheel rolled away from you: closer to the
    ///     character, towards first person). Remembers where it is.
    /// </summary>
    public void ScrollBy(float notches)
    {
        if (Camera.Mode != CameraMode.DynamicView)
            return;

        _scrollTarget = DynamicView.Step(_scrollTarget, notches, _cfg.GetCVar(CCVars.Render3DDynamicViewSpeed),
            _cfg.GetCVar(CCVars.Render3DDynamicViewInvert), DynamicView.MaxU(_scrollAllow2D));
        _cfg.SetCVar(CCVars.Render3DDynamicViewU, _scrollTarget);
    }

    /// <summary>Puts the dynamic view at a position (0 to 1) and remembers it.</summary>
    public void ScrollTo(float u)
    {
        _scrollTarget = Math.Clamp(u, 0f, DynamicView.MaxU(_scrollAllow2D));
        _cfg.SetCVar(CCVars.Render3DDynamicViewU, _scrollTarget);
    }

    private DynamicViewSettings DynamicViewSettingsNow()
    {
        // the straight-down stop matches the classic view: its distance makes the picture as many tiles tall as that view is
        var topDistance = TwoDTiles / (2f * MathF.Tan(MathHelper.DegreesToRadians(DynamicView.TopFov) * 0.5f));
        return new DynamicViewSettings(
            _cfg.GetCVar(CCVars.Render3DFov),
            Math.Clamp(_cfg.GetCVar(CCVars.Render3DThirdPersonDistance), 0.8f, 3f),
            _cfg.GetCVar(CCVars.Render3DIsoPitch),
            _cfg.GetCVar(CCVars.Render3DIsoFov),
            _cfg.GetCVar(CCVars.Render3DOverheadPitch),
            Math.Clamp(_cfg.GetCVar(CCVars.Render3DOverheadDistance), OverheadCamera.MinDistance, OverheadCamera.MaxDistance),
            _cfg.GetCVar(CCVars.Render3DOverheadFov),
            Math.Clamp(topDistance, OverheadCamera.MinDistance, 40f),
            _cfg.GetCVar(CCVars.Render3DWallHeight),
            _cfg.GetCVar(CCVars.Render3DOverheadWallHeight));
    }

    /// <summary>
    ///     Advances the dynamic view: it glides to where the wheel put it and the switches that depend on its position (free cursor, no
    ///     ceiling, the classic view) are decided. Called every frame by the controller, also while the classic view is shown.
    /// </summary>
    public void TickScroll(float dt, bool allow2D)
    {
        if (Camera.Mode != CameraMode.DynamicView)
        {
            if (_scrollInit)
                ResetScroll();
            return;
        }

        _scrollAllow2D = allow2D;
        var max = DynamicView.MaxU(allow2D);
        if (!_scrollInit)
        {
            _scrollInit = true;
            _scrollTarget = ScrollU = Math.Clamp(_cfg.GetCVar(CCVars.Render3DDynamicViewU), 0f, max);
            _scrollVelocity = 0f;
        }

        _scrollTarget = Math.Min(_scrollTarget, max);
        ScrollU = Math.Clamp(Glide.Damp(ScrollU, ref _scrollVelocity, _scrollTarget, dt, DynamicView.GlideTime), 0f, max);
        _scrollFree = DynamicView.Latch(_scrollFree, ScrollU, DynamicView.FreeOn, DynamicView.FreeOff);
        if (!_scrollFree)
        {
            // down among the walls the view follows the mouse look again: the turn is given up
            _scrollTurn = 0.0;
            _turnDragging = false;
        }

        _scrollNoCeiling = DynamicView.Latch(_scrollNoCeiling, ScrollU, DynamicView.NoCeilingOn, DynamicView.NoCeilingOff);
        ScrollTwoD = allow2D && DynamicView.Latch(ScrollTwoD, ScrollU, DynamicView.TwoDOn, DynamicView.TwoDOff);
        ScrollFade = allow2D ? DynamicView.Fade(ScrollU) : 0f;
        Camera.ScrollElevated = _scrollFree;
        _scrollView = DynamicView.Evaluate(ScrollU, DynamicViewSettingsNow());
    }

    private void ResetScroll()
    {
        _scrollInit = false;
        _scrollFree = false;
        _scrollNoCeiling = false;
        _scrollBias = float.NaN;
        _scrollTurn = 0.0;
        _scrollVelocity = 0f;
        ScrollTwoD = false;
        ScrollFade = 0f;
        Camera.ScrollElevated = false;
    }

    /// <summary>
    ///     The classic view takes over the camera heading (the controller shows the 2D viewport): stop sending the 3D view's yaw, so the
    ///     game's own camera keys work there.
    /// </summary>
    public void ReleaseYaw()
    {
        if (_yawSystem != null)
            _yawSystem.DesiredYaw = null;
    }

    /// <summary>
    ///     Back from the classic view: read the quarter turns the camera has there (the game's camera keys may have turned it) so the 3D view
    ///     picks up from the same heading.
    /// </summary>
    public void SyncStepsFromMover()
    {
        if (_yawSystem?.CurrentYaw() is not { } yaw || _player.LocalEntity is not { } player || _xformSys == null)
            return;

        var grid = Angle.Zero;
        if (_entMan.TryGetComponent(player, out TransformComponent? xform) && xform.GridUid is { } g)
            grid = _xformSys.GetWorldRotation(g);

        var quarters = (yaw - grid).Reduced().Theta / (Math.PI / 2);
        OverheadSteps = OverheadCamera.Turn(0, (int) Math.Round(quarters));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (args.Handled || Camera.Mode != CameraMode.DynamicView)
            return;

        ScrollBy(args.Delta.Y);
        args.Handle();
    }

    /// <summary>
    ///     Turns the overhead camera by eighth turns (the camera rotate keys, snapping to the nearest one), or back to the grid's north
    ///     (<paramref name="quarterTurns"/> 0); the dynamic view turns by quarter turns, counted in <see cref="OverheadSteps"/>.
    /// </summary>
    public void TurnView(int quarterTurns)
    {
        if (Camera.Mode == CameraMode.Overhead)
        {
            const double step = Math.PI / 4;
            _overheadTurn = quarterTurns == 0 ? 0.0 : (Math.Round(_overheadTurn / step) + quarterTurns) * step;
            return;
        }

        if (quarterTurns == 0)
            _scrollTurn = 0.0;

        OverheadSteps = OverheadCamera.Turn(OverheadSteps, quarterTurns);
    }

    /// <summary>Height of walls (and of the ceiling, if there is one): the high cameras have their own, lower value.</summary>
    public float WallHeight => Camera.Mode == CameraMode.DynamicView && _scrollInit
        ? _scrollView.WallHeight
        : _cfg.GetCVar(Elevated ? CCVars.Render3DOverheadWallHeight : CCVars.Render3DWallHeight);

    /// <summary>
    ///     High cameras: how far in front of the camera the character is (view depth), 0 for the other cameras. Distance
    ///     limits, fog and haze are counted from the character, because the camera itself hangs high above and behind it.
    /// </summary>
    public float ViewDepthShift => Elevated || Camera.Mode == CameraMode.DynamicView
        ? MathF.Max(Vector3.Dot(Camera.Head - Camera.Position, Camera.Forward), 0f)
        : 0f;

    /// <summary>True while the cursor can point at things: the crosshair, or in the high views the free cursor over this view.</summary>
    public bool PointerLive => RelativeMouse || Elevated && UserInterfaceManager.CurrentlyHovered == this;

    /// <summary>Control-local pixel of what the player points at: the crosshair, or the free cursor.</summary>
    public Vector2 PointerPixel => RelativeMouse ? CrosshairPixel : _input.MouseScreenPosition.Position - GlobalPixelPosition;

    /// <summary>Debug view selector: 0 = normal, 1 = raw ground capture, 2 = light target, 3 = fov target, 4 to 19 shader views, 41 = billboard atlas, 42 = its glow layer.</summary>
    public int DebugView;

    /// <summary>Set by the controller: the mouse is captured and moves the camera.</summary>
    public bool RelativeMouse;

    public void SetDebugNoGlow(bool off) => _entityPass!.DebugNoGlow = off;

    /// <summary>Developer aid: the billboard atlas and its glow layer as render targets (null before the first frame).</summary>
    public void RequestAtlasAudit(Action<int, IRenderTexture> save) => _entityPass?.RequestAudit(save);

    public (IRenderTexture? Atlas, IRenderTexture? Glow) AtlasTargets => (_entityPass?.AtlasTarget, _entityPass?.GlowAtlasTarget);

    /// <summary>Mouse travel (pixels) accumulated since <see cref="ResetRelativeTravel"/>; used by drag and drop.</summary>
    public float RelativeTravel { get; private set; }

    public void ResetRelativeTravel() => RelativeTravel = 0f;

    /// <summary>Developer override: behave as if the mouse were captured when picking (excludes the own body).</summary>
    public bool DevExcludeSelf;

    public float Sensitivity = 1f;
    public bool InvertY;

    /// <summary>Developer override of the camera look direction (degrees), set by the r3d_look command.</summary>
    public (double Yaw, double Pitch)? LookOverride;

    /// <summary>Last map the camera rendered on (for conversions outside of Draw).</summary>
    private MapId _lastMap = MapId.Nullspace;

    public Render3DViewportControl()
    {
        IoCManager.InjectDependencies(this);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _ground = new GroundLayer(_clyde, _overlays);
        _tileWorld = _entMan.System<TileWorldSystem>();
        _yawSystem = _entMan.System<CameraYawSystem>();
        _xformSys = _entMan.System<SharedTransformSystem>();
        _entityPass = new EntityPass(_entMan, _clyde, _protos, _cfg);
        _post = new PostFx(_clyde, _protos);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        DisposeSteer();
        if (_yawSystem != null)
            _yawSystem.DesiredYaw = null;
        DisposeTargets();
        _ground?.Dispose();
        _ground = null;
        _tileWorld = null;
        _entityPass?.Dispose();
        _entityPass = null;
        _post?.Dispose();
        _post = null;
    }

    private void DisposeTargets()
    {
        _twoDViewport?.Dispose();
        _twoDViewport = null;
        _scene?.Dispose();
        _scene = null;
        _composite?.Dispose();
        _composite = null;
        _final?.Dispose();
        _final = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled)
            return;

        // the overhead camera (and the dynamic view, once it is high) turns while the drag key (the middle mouse button) is held over the view
        if (args.Function == Render3DKeys.OrbitDrag && (Camera.Mode == CameraMode.Overhead || Camera.Mode == CameraMode.DynamicView && _scrollFree || SteerLive))
            _turnDragging = true;

        ForwardKey(args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == Render3DKeys.OrbitDrag)
            EndTurnDrag();

        if (args.Handled)
            return;
        ForwardKey(args);
    }

    private void EndTurnDrag()
    {
        if (!_turnDragging)
            return;

        _turnDragging = false;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        EndTurnDrag();
    }

    /// <summary>
    ///     With the mouse captured the OS cursor sits wherever it was when capture began, so key events would carry a
    ///     meaningless pointer position. Context menus, examine tooltips and popups use that position, so hand the 2D
    ///     input code the crosshair position instead.
    /// </summary>
    private void ForwardKey(GUIBoundKeyEventArgs args)
    {
        if (!RelativeMouse)
        {
            _input.ViewportKeyEvent(this, args);
            return;
        }

        var coords = new ScreenCoordinates(GlobalPixelPosition + CrosshairPixel, _clyde.MainWindow.Id);
        var forwarded = new BoundKeyEventArgs(args.Function, args.State, coords, args.CanFocus);
        _input.ViewportKeyEvent(this, forwarded);
        if (forwarded.Handled)
            args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_turnDragging)
        {
            if (SteerDragTurn(args.Relative))
                return;

            // drag right: the scene turns right; drag down: the camera climbs (looks further down)
            var turn = RadiansPerPixel * Sensitivity * DragTurnGain;
            if (Camera.Mode == CameraMode.DynamicView)
            {
                // the dynamic view keeps the look-down angle its wheel position asks for: only the heading turns
                _scrollTurn += args.Relative.X * turn;
                return;
            }

            _overheadTurn += args.Relative.X * turn;
            var climb = (InvertY ? -args.Relative.Y : args.Relative.Y) * turn * 180f / MathF.PI;
            _cfg.SetCVar(CCVars.Render3DOverheadPitch,
                Math.Clamp(_cfg.GetCVar(CCVars.Render3DOverheadPitch) + climb, OverheadCamera.MinPitchDegrees, OverheadCamera.MaxPitchDegrees));
            return;
        }

        if (!RelativeMouse)
            return;

        RelativeTravel += args.Relative.Length();
        if (Steering)
            return; // the hidden cursor steers (SteerTick)

        var k = RadiansPerPixel * Sensitivity;
        Camera.AddLook(-args.Relative.X * k, (InvertY ? args.Relative.Y : -args.Relative.Y) * k);
    }

    protected override void Resized()
    {
        base.Resized();
        DisposeTargets();
    }

    // ---------------------------------------------------------------- drawing

    protected override void Draw(IRenderHandle handle)
    {
        try
        {
            DrawInternal(handle);
        }
        catch (Exception e)
        {
            // Never let a rendering bug take the whole UI down; log it (rate limited) and draw nothing.
            _sawmill ??= Logger.GetSawmill("render3d");
            var now = _timing.RealTime;
            if (now >= _nextErrorLog)
            {
                _nextErrorLog = now + TimeSpan.FromSeconds(5);
                _sawmill.Error($"3D draw failed: {e}");
            }
        }
    }

    private const float MaxScenePixels = 8_400_000f;

    /// <summary>Supersampling factor (1 = off). Only used while the image is not drawn smaller than the window.</summary>
    private float Supersample => Math.Clamp(_cfg.GetCVar(CCVars.Render3DSupersample), 1f, 2f);

    private Vector2i ComputeSceneSize(Vector2i pixelSize)
    {
        var renderScale = Math.Clamp(_cfg.GetCVar(CCVars.Render3DRenderScale), 0.25f, 1f);

        // keep the supersampled image to a sane size on big screens (about 4K worth of pixels at most)
        var pixels = Math.Max(1f, (float) pixelSize.X * pixelSize.Y * renderScale * renderScale);
        var scale = renderScale * Math.Clamp(Math.Min(Supersample, MathF.Sqrt(MaxScenePixels / pixels)), 1f, 2f);
        return new Vector2i(Math.Max(16, (int) (pixelSize.X * scale)), Math.Max(16, (int) (pixelSize.Y * scale)));
    }

    private void DrawInternal(IRenderHandle handle)
    {
        var screen = handle.DrawingHandleScreen;
        var ground = _ground;
        var tileWorld = _tileWorld;
        if (ground == null || tileWorld == null)
            return;

        var pixelSize = PixelSize;
        if (pixelSize.X <= 0 || pixelSize.Y <= 0)
            return;

        var eye = _eyeManager.CurrentEye;
        if (eye.Position.MapId == MapId.Nullspace)
        {
            _lastMap = MapId.Nullspace;
            screen.DrawRect(new UIBox2(Vector2.Zero, pixelSize), Color.Black);
            return;
        }

        _lastMap = eye.Position.MapId;

        _sw.Restart();
        var radius = Math.Clamp(_cfg.GetCVar(CCVars.Render3DGroundRadius), 8, 26);
        ground.SetRadius(radius);
        ground.Render(eye.Position, eye);
        Lap(ref _msGround);

        if (DebugView is 1 or 2 or 3)
        {
            DrawDebug(screen, ground, pixelSize);
            return;
        }

        var sceneSize = ComputeSceneSize(pixelSize);
        UpdateCamera(eye.Position, sceneSize);
        tileWorld.WallAtlas.Flush(screen);
        tileWorld.UpdateWindows(eye.Position.MapId, new Vector2(Camera.Head.X, Camera.Head.Y), radius);

        if (_cfg.GetCVar(CCVars.Render3DDevChannel) && _timing.RealTime >= _nextStatLog)
        {
            _nextStatLog = _timing.RealTime + TimeSpan.FromSeconds(3);
            _sawmill ??= Logger.GetSawmill("render3d");
            _sawmill.Info($"stats: frame={_timing.CurFrame} t={_timing.RealTime.TotalSeconds:F1} fps={_timing.FramesPerSecondAvg:F1} cam={Camera.Position} head={Camera.Head} yaw={Camera.Yaw:F2} pitch={Camera.Pitch:F2} grids={tileWorld.ActiveGrids.Count} walls={tileWorld.DebugWallCount} entities={_entityPass?.LastEntityCount} quads={_entityPass?.LastQuadCount} ms[ground={_msGround:F2} prepare={_msPrepare:F2} atlas={_msAtlas:F2} scene={_msScene:F2} ui={_msUi:F2}] map={eye.Position.MapId} rel={RelativeMouse}");
        }

        if (_scene == null || _scene.Size != sceneSize)
        {
            _scene?.Dispose();
            _scene = _clyde.CreateRenderTarget(
                sceneSize,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F),
                new TextureSampleParameters { Filter = false },
                "render3d-scene");
        }

        if (_composite == null || _composite.Size != sceneSize)
        {
            _composite?.Dispose();
            _composite = _clyde.CreateRenderTarget(
                sceneSize,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F),
                new TextureSampleParameters { Filter = false },
                "render3d-composite");
        }

        _raymarch ??= _protos.Index(RaymarchShader).InstanceUnique();
        _present ??= _protos.Index(PresentShader).InstanceUnique();

        SetRaymarchParams(ground, tileWorld, sceneSize, radius);

        if (DebugView == 31)
        {
            screen.DrawRect(new UIBox2(Vector2.Zero, pixelSize), Color.Red);
            return;
        }

        var entityPass = _entityPass!;
        // hide the player's own body in first person, or when the spring arm pulled the camera right up to it
        var hide = Camera.Mode == CameraMode.FirstPerson || _cameraToHead < 0.75f ? _player.LocalEntity : null;
        UpdateHighlight(entityPass);
        _sw.Restart();
        entityPass.Prepare(Camera, eye.Position.MapId, radius, hide, WallHeight);
        Lap(ref _msPrepare);
        entityPass.DrawAtlas(screen);
        entityPass.RunAuditIfRequested(screen, _sawmill ??= Logger.GetSawmill("render3d"));
        Lap(ref _msAtlas);

        screen.RenderInRenderTarget(_scene, () =>
        {
            screen.UseShader(_raymarch);
            screen.DrawRect(new UIBox2(Vector2.Zero, sceneSize), Color.White);
            screen.UseShader(null);
        }, Color.Black);

        screen.RenderInRenderTarget(_composite, () =>
        {
            screen.UseShader(_present);
            screen.DrawTextureRect(_scene.Texture, new UIBox2(Vector2.Zero, sceneSize));
            screen.UseShader(null);
            if (DebugView != 40)
                entityPass.DrawQuads(screen, Camera, _scene.Texture, ground, sceneSize);
        }, Color.Black);

        var postSettings = PostFx.Read(_cfg);
        var usePost = _post != null && postSettings.AnyActive && DebugView == 0;
        var ratio = (float) sceneSize.X / pixelSize.X;
        if (ratio > 1.01f)
        {
            // Supersampled: finish the image at the large size, then average it down to the window size.
            if (_final == null || _final.Size != sceneSize)
            {
                _final?.Dispose();
                _final = _clyde.CreateRenderTarget(
                    sceneSize,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba16F),
                    new TextureSampleParameters { Filter = true },
                    "render3d-final");
            }

            var big = new UIBox2(Vector2.Zero, sceneSize);
            var sceneSettings = postSettings with { Dither = false };
            if (usePost)
                _post!.PrepareBloom(screen, _composite.Texture, sceneSize, sceneSettings, Supersample);

            screen.RenderInRenderTarget(_final, () =>
            {
                if (usePost)
                {
                    _post!.Draw(screen, _composite.Texture, _scene.Texture, sceneSize, big, sceneSettings, Supersample);
                }
                else
                {
                    screen.UseShader(_present);
                    screen.DrawTextureRect(_composite.Texture, big);
                    screen.UseShader(null);
                }
            }, Color.Black);

            _resolve ??= _protos.Index(ResolveShader).InstanceUnique();
            _resolve.SetParameter("srcTexel", new Vector2(1f / sceneSize.X, 1f / sceneSize.Y));
            _resolve.SetParameter("spread", new Vector2(0.25f * ratio, 0.25f * (float) sceneSize.Y / pixelSize.Y));
            _resolve.SetParameter("dither", usePost && postSettings.Dither ? 1f : 0f);
            screen.UseShader(_resolve);
            screen.DrawTextureRect(_final.Texture, new UIBox2(Vector2.Zero, pixelSize));
            screen.UseShader(null);
        }
        else if (usePost)
        {
            _post!.Draw(screen, _composite.Texture, _scene.Texture, sceneSize, new UIBox2(Vector2.Zero, pixelSize), postSettings, 1f);
        }
        else
        {
            screen.UseShader(_present);
            screen.DrawTextureRect(_composite.Texture, new UIBox2(Vector2.Zero, pixelSize));
            screen.UseShader(null);
        }

        Lap(ref _msScene);

        // the end of the scroll: the classic 2D picture is blended over this one
        if (Camera.Mode == CameraMode.DynamicView && ScrollFade > 0.002f)
            DrawTwoDFade(screen, pixelSize, eye, ScrollFade);
        else if (_twoDViewport != null)
        {
            _twoDViewport.Dispose();
            _twoDViewport = null;
        }

        if (DebugView is 41 or 42
            && (DebugView == 41 ? entityPass.AtlasTexture : entityPass.GlowAtlasTexture) is { } atlasTex)
        {
            // developer aid: show the billboard atlas (41) or its glow layer (42) over the image
            var side = Math.Min(pixelSize.X, pixelSize.Y);
            screen.DrawRect(new UIBox2(0, 0, side, side), new Color(0.12f, 0.14f, 0.2f));
            screen.DrawTextureRect(atlasTex, new UIBox2(0, 0, side, side));
        }

        // 2D screen-space overlays that must draw over the 3D image (flash, blindness, drunk, ...)
        if (ground.Viewport is { } vp)
        {
            var box = new UIBox2i(Vector2i.Zero, pixelSize).Translated(GlobalPixelPosition);
            vp.RenderScreenOverlaysBelow(handle, this, box);
            vp.RenderScreenOverlaysAbove(handle, this, box);
        }

        DrawStatusIcons(screen);
        DrawNotice(screen, pixelSize);
        Lap(ref _msUi);

        if (RelativeMouse)
        {
            var reach = GetReach(CrosshairPixel);
            DrawCrosshair(screen, pixelSize, reach);
            DrawSteer(screen, pixelSize);
            DrawHoverLabel(screen, reach, CrosshairPixel, new Vector2(pixelSize.X / 2f, pixelSize.Y / 2f + 18f), true);
        }
        else if (PointerLive)
        {
            // the free cursor of the overhead view: the name of what it points at, beside it
            var pointer = PointerPixel;
            DrawHoverLabel(screen, GetReach(pointer), pointer, pointer + new Vector2(18f, 22f), false);
        }
    }

    /// <summary>
    ///     Draws the classic 2D picture over the 3D one with <paramref name="fade"/> as its opacity, from a viewport of its own that looks through
    ///     the same eye, with the size and the fit of the real 2D viewport (see <see cref="TwoDViewportSize"/>), so that when the real one takes over
    ///     nothing moves.
    /// </summary>
    private void DrawTwoDFade(DrawingHandleScreen screen, Vector2i pixelSize, IEye eye, float fade)
    {
        var size = TwoDViewportSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        if (_twoDViewport == null || _twoDViewport.Size != size)
        {
            _twoDViewport?.Dispose();
            _twoDViewport = _clyde.CreateViewport(size, new TextureSampleParameters { Filter = TwoDBilinear });
        }

        _twoDViewport.Eye = eye;
        _twoDViewport.Render();

        // the same fit ScalingViewport has: fitted by the smaller (or the chosen) ratio and centred, or a fixed size centred
        var ours = new Vector2(pixelSize.X, pixelSize.Y);
        var theirs = new Vector2(size.X, size.Y);
        Vector2 drawSize;
        if (TwoDFixedStretch is { } fixedSize)
        {
            drawSize = new Vector2(fixedSize.X, fixedSize.Y);
        }
        else
        {
            var ratio = TwoDIgnoreDimension switch
            {
                1 => ours.Y / theirs.Y,
                2 => ours.X / theirs.X,
                _ => MathF.Min(ours.X / theirs.X, ours.Y / theirs.Y),
            };
            drawSize = theirs * ratio;
        }

        // the classic view has black around its picture when it does not fill the window: fade that in as well, so nothing pops at the end
        screen.DrawRect(new UIBox2(Vector2.Zero, ours), new Color(0f, 0f, 0f, fade));
        var box = UIBox2.FromDimensions((ours - drawSize) / 2f, drawSize);
        screen.DrawTextureRect(_twoDViewport.RenderTarget.Texture, box, new Color(1f, 1f, 1f, fade));
    }

    private void UpdateHighlight(EntityPass pass)
    {
        pass.HighlightUid = null;
        pass.Box = null;
        if (!PointerLive || !_cfg.GetCVar(CCVars.OutlineEnabled))
            return;

        // Whatever the pointer is on is marked: a thing (a sprite gets a thin bright line round it, a table or a door a frame), a wall (a
        // frame on the faces and the top), or the floor tile.
        var pick = Pick(PointerPixel);
        if (pick.FirstIsWall)
        {
            if (_tileWorld != null && _tileWorld.TryGetTileBox(pick.WallGrid, pick.WallTile, out var center, out var sideX, out var sideY))
                pass.Box = new EntityPass.HighlightBox { Center = center, SideX = sideX, SideY = sideY, Height = WallHeight };
        }
        else if (pick.FirstEntity is { } uid)
        {
            pass.HighlightUid = uid;
        }
        else if (pick.OnFloor && _tileWorld != null && _tileWorld.TryGetTileBox(_lastMap, pick.Coords.Position, out var tc, out var tx, out var ty))
        {
            pass.Box = new EntityPass.HighlightBox { Center = tc, SideX = tx, SideY = ty, Height = 0f };
        }
    }

    /// <summary>
    ///     Whether the entity under the crosshair is within hand reach of the local player (null when there is none).
    ///     Held and worn things always count as in reach. Used only to tint the crosshair and its label, so the player
    ///     can tell "too far to pick up / open / cuff" from "wrong target" without trial and error.
    /// </summary>
    private bool? GetReach(Vector2 pixel)
    {
        if (Pick(pixel).FirstEntity is not { } target || !_entMan.EntityExists(target)
            || _player.LocalEntity == null || _xformSys == null)
        {
            return null;
        }

        return InReach(target);
    }

    /// <summary>Whether this entity is within hand reach of the local player (held and worn things always are).</summary>
    private bool InReach(EntityUid target)
    {
        if (_player.LocalEntity is not { } self || _xformSys == null)
            return false;

        var xform = _entMan.GetComponent<TransformComponent>(target);
        if (xform.ParentUid != xform.GridUid && xform.ParentUid != xform.MapUid)
            return true; // inside a container, hand or inventory slot

        var delta = _xformSys.GetWorldPosition(target) - _xformSys.GetWorldPosition(self);
        return delta.LengthSquared() <= SharedInteractionSystem.InteractionRangeSquared;
    }

    /// <summary>
    ///     The entity the player chose from the list of what they point at. While it is set, what the crosshair is "on" is this entity
    ///     (use, attack, pull, the name under the crosshair and the outline all follow it). Cleared when it is gone or far away.
    /// </summary>
    public EntityUid? PinnedTarget;

    private readonly List<(EntityUid Uid, float Distance)> _pointNear = new();
    private readonly List<EntityUid> _pointed = new();

    /// <summary>
    ///     What the crosshair points at, for the list at the side of the view: the entities its ray crosses (nearest first), then the
    ///     ones that lie within half a tile of where it lands, which is the pile on a table. The names are the ones the label under
    ///     the crosshair uses; things with the same name are counted. At most <paramref name="max"/> lines; the rest is
    ///     <paramref name="hidden"/>.
    /// </summary>
    public void DescribePointed(List<(EntityUid Uid, string Text, bool InReach)> lines, int max, out int hidden)
    {
        lines.Clear();
        hidden = 0;
        if (!RelativeMouse)
            return;

        var pick = Pick(CrosshairPixel);
        _pointed.Clear();
        foreach (var uid in pick.Entities)
            _pointed.Add(uid);

        if (_entityPass != null && pick.Coords.MapId == _lastMap)
        {
            _entityPass.Nearby(pick.Coords.Position, 0.5f, _pointNear);
            foreach (var (uid, _) in _pointNear)
            {
                if (!_pointed.Contains(uid))
                    _pointed.Add(uid);
            }
        }

        var self = _player.LocalEntity;
        var names = new List<(string Name, int Count, bool Reach, EntityUid First)>();
        foreach (var uid in _pointed)
        {
            if (uid == self || !_entMan.EntityExists(uid))
                continue;

            string name = Identity.Name(uid, _entMan);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var reach = InReach(uid);
            var index = names.FindIndex(n => n.Name == name && n.Reach == reach);
            if (index >= 0)
                names[index] = (name, names[index].Count + 1, reach, names[index].First);
            else
                names.Add((name, 1, reach, uid));
        }

        // what the player can touch comes first, and in each group loose things (items, a pile) before fixtures (a window, a table, a
        // wall), because the list is for reaching what is stacked; inside a group the order is the order the ray crosses them
        var ordered = new List<(string Name, int Count, bool Reach, EntityUid First)>(names.Count);
        for (var bucket = 0; bucket < 4; bucket++)
        {
            foreach (var n in names)
            {
                var anchored = _entMan.TryGetComponent(n.First, out TransformComponent? xf) && xf.Anchored;
                if ((n.Reach ? 0 : 2) + (anchored ? 1 : 0) == bucket)
                    ordered.Add(n);
            }
        }

        names = ordered;
        for (var i = 0; i < names.Count; i++)
        {
            if (i >= max)
            {
                hidden += names[i].Count;
                continue;
            }

            var (name, count, reach, first) = names[i];
            lines.Add((first, count > 1 ? $"{count} x {name}" : name, reach));
        }
    }

    private void DrawHoverLabel(DrawingHandleScreen screen, bool? reach, Vector2 pickPixel, Vector2 labelPos, bool centered)
    {
        if (!_cfg.GetCVar(CCVars.Render3DCrosshairNames))
            return;

        var first = Pick(pickPixel).FirstEntity;
        if (first is not { } uid || !_entMan.EntityExists(uid))
            return;

        // The same name the 2D hover/context menu shows (respects identity: masked people are "Unknown"). A string, not var:
        // forks such as RMC-14 return their own identity type, which only converts to a string.
        string name = Identity.Name(uid, _entMan);
        if (string.IsNullOrWhiteSpace(name))
            return;

        _labelFont ??= new VectorFont(_resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
        var dims = screen.GetDimensions(_labelFont, name, 1f);
        var pos = centered ? labelPos - new Vector2(dims.X / 2f, 0f) : labelPos;
        screen.DrawString(_labelFont, pos + new Vector2(1, 1), name, new Color(0, 0, 0, 0.8f));
        screen.DrawString(_labelFont, pos, name, reach == false ? new Color(0.72f, 0.72f, 0.72f, 0.9f) : Color.White);
    }

    private static void DrawCrosshair(DrawingHandleScreen screen, Vector2i pixelSize, bool? reach)
    {
        var c = new Vector2(pixelSize.X / 2f, pixelSize.Y / 2f);
        const float arm = 7f;
        const float gap = 3f;
        var white = reach == true ? new Color(0.55f, 1f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
        var black = new Color(0f, 0f, 0f, 0.6f);

        foreach (var (col, off) in new[] { (black, 1f), (white, 0f) })
        {
            var o = new Vector2(off, off);
            screen.DrawLine(c + o + new Vector2(-arm, 0), c + o + new Vector2(-gap, 0), col);
            screen.DrawLine(c + o + new Vector2(gap, 0), c + o + new Vector2(arm, 0), col);
            screen.DrawLine(c + o + new Vector2(0, -arm), c + o + new Vector2(0, -gap), col);
            screen.DrawLine(c + o + new Vector2(0, gap), c + o + new Vector2(0, arm), col);
        }
    }

    /// <summary>Developer aid: point the camera so the crosshair ray hits the floor at <paramref name="target"/>.</summary>
    public void AimAtFloor(Vector2 target)
    {
        for (var i = 0; i < 4; i++)
        {
            var d = new Vector3(target.X - Camera.Position.X, target.Y - Camera.Position.Y, -Camera.Position.Z);
            var yaw = Math.Atan2(-d.X, d.Y);
            var horiz = MathF.Sqrt(d.X * d.X + d.Y * d.Y);
            var pitch = MathF.Atan2(d.Z, MathF.Max(horiz, 1e-3f));
            LookOverride = (MathHelper.RadiansToDegrees(yaw), MathHelper.RadiansToDegrees(pitch));
            Camera.Yaw = yaw;
            Camera.Pitch = pitch;
            Camera.UpdateBasis();
            var head = Camera.Head;
            var fwd = Camera.ForwardGround;
            var right = new Vector2(Camera.Right.X, Camera.Right.Y);
            var dist = Math.Clamp(_cfg.GetCVar(CCVars.Render3DThirdPersonDistance), 0.8f, 3.0f);
            if (Camera.Mode == CameraMode.ThirdPerson)
                Camera.Position = new Vector3(head.X - fwd.X * dist + right.X * 0.35f, head.Y - fwd.Y * dist + right.Y * 0.35f, Camera.Position.Z);
        }
    }

    public string DescribePick()
    {
        var r = Pick(CrosshairPixel);
        var names = string.Join(", ", r.Entities.Select(e => _entMan.ToPrettyString(e).ToString()));
        return $"cam={Camera.Position} yaw={Camera.Yaw:F2} pitch={Camera.Pitch:F2} pick coords={r.Coords} dist={r.Distance:F2} first={(r.FirstEntity == null ? "none" : _entMan.ToPrettyString(r.FirstEntity.Value).ToString())} all=[{names}]";
    }

    public void DumpEntities()
    {
        _sawmill ??= Logger.GetSawmill("render3d");
        _entityPass?.Dump(_sawmill);
    }

    private void DrawDebug(DrawingHandleScreen screen, GroundLayer ground, Vector2i pixelSize)
    {
        var tex = DebugView switch
        {
            2 => ground.LightTarget?.Texture,
            3 => ground.FovMode == GroundFov.Mask ? ground.FovMask : ground.FovTarget?.Texture,
            _ => ground.GroundTexture,
        };

        if (tex != null)
        {
            var side = Math.Min(pixelSize.X, pixelSize.Y);
            screen.DrawTextureRect(tex, new UIBox2(0, 0, side, side));
        }
    }

    // ---------------------------------------------------------------- camera

    private void UpdateCamera(MapCoordinates eyePos, Vector2i sceneSize)
    {
        var cam = Camera;
        if (cam.Mode == CameraMode.DynamicView && _scrollInit)
        {
            UpdateScrollCamera(eyePos, sceneSize);
            return;
        }

        var mode = cam.Mode;
        var elevated = cam.Elevated;
        cam.Size = new Vector2(sceneSize.X, sceneSize.Y);
        cam.FovY = MathHelper.DegreesToRadians((float) _cfg.GetCVar(mode switch
        {
            CameraMode.Overhead => CCVars.Render3DOverheadFov,
            _ => CCVars.Render3DFov,
        }));

        var wallHeight = WallHeight;
        var eyeHeight = _cfg.GetCVar(CCVars.Render3DEyeHeight);

        var player = _player.LocalEntity;
        if (player != _controlled)
        {
            // New body (spawn, ghosting, possession): face the way it faces.
            _controlled = player;
            _overheadSnap = true;
            if (player is { } p && _entMan.TryGetComponent(p, out TransformComponent? pxform))
            {
                var rot = _xformSys!.GetWorldRotation(pxform);
                cam.Yaw = rot.Theta - Math.PI;
                cam.Pitch = cam.Mode == CameraMode.FirstPerson ? 0f : -0.15f;
            }
        }

        if (mode != _lastMode)
        {
            // A new camera. Leaving a high camera for a walking one: look level again (the high cameras look steeply down, and that angle would carry over).
            if (_lastMode is CameraMode.Overhead or CameraMode.DynamicView && !elevated)
                cam.Pitch = mode == CameraMode.FirstPerson ? 0f : -0.15f;

            _turnDragging = false;
            _lastMode = mode;
        }

        if (LookOverride is { } look)
        {
            cam.Yaw = MathHelper.DegreesToRadians(look.Yaw);
            cam.Pitch = (float) MathHelper.DegreesToRadians(look.Pitch);
        }

        var targetYaw = mode == CameraMode.Overhead ? OverheadTargetYaw(player) + _overheadTurn : 0.0;

        if (elevated)
        {
            // Fixed to the grid's heading, turned by the rotate keys and by dragging. The picture glides to a new heading (and follows a drag
            // at once), while the movement keys switch to it at once (see DesiredYaw below).
            var turnDt = (float) _timing.FrameTime.TotalSeconds;
            if (_turnDragging || _overheadSnap || turnDt <= 0f || turnDt > 0.25f || double.IsNaN(cam.Yaw))
                cam.Yaw = targetYaw;
            else
                cam.AddLook((float) Angle.ShortestDistance(new Angle(cam.Yaw), new Angle(targetYaw)).Theta * (1f - MathF.Exp(-turnDt * 12f)), 0f);

            _overheadTurn = new Angle(_overheadTurn).Reduced().Theta;
            _overheadSnap = false;

            var pitchDegrees = Math.Clamp(_cfg.GetCVar(CCVars.Render3DOverheadPitch), OverheadCamera.MinPitchDegrees, OverheadCamera.MaxPitchDegrees);
            cam.Pitch = -MathHelper.DegreesToRadians(pitchDegrees);
        }

        cam.UpdateBasis();

        if (_yawSystem != null)
            _yawSystem.DesiredYaw = player != null ? new Angle(elevated ? targetYaw : cam.Yaw) : null;

        eyeHeight += HeadBob(eyePos.Position, cam.Mode == CameraMode.FirstPerson);
        var head = new Vector3(eyePos.X, eyePos.Y, eyeHeight);
        cam.Head = head;

        if (elevated)
        {
            // High above and behind the character, looking down at it. No ceiling and no spring arm: nothing is above the walls.
            var armLength = Math.Clamp(_cfg.GetCVar(CCVars.Render3DOverheadDistance), OverheadCamera.MinDistance, OverheadCamera.MaxDistance);
            cam.Position = OverheadCamera.Position(new Vector2(head.X, head.Y), cam.Forward, armLength);
            _cameraToHead = armLength;
            _armFraction = 1f;
            cam.UpdateBasis();
            return;
        }

        if (cam.Mode == CameraMode.FirstPerson)
        {
            cam.Position = head;
            _cameraToHead = 0f;
            return;
        }

        // third person
        var dist = Math.Clamp(_cfg.GetCVar(CCVars.Render3DThirdPersonDistance), 0.8f, 3.0f);
        const float side = 0.35f;
        var fwd = cam.ForwardGround;
        var right = new Vector2(cam.Right.X, cam.Right.Y);
        var height = MathF.Min(eyeHeight + 0.25f, wallHeight - 0.2f);
        var desired = new Vector3(
            head.X - fwd.X * dist + right.X * side,
            head.Y - fwd.Y * dist + right.Y * side,
            height);

        // Spring arm: never let the camera sit inside or behind a wall, a window or a closed door. The arm is swept
        // with a small radius so the camera keeps a clear gap to the wall instead of touching it, it shortens at once
        // when something is in the way (no clipping) and lengthens again smoothly (no popping when the view turns).
        var fraction = 1f;
        if (_tileWorld != null)
        {
            var map = eyePos.MapId;
            var tw = _tileWorld;
            fraction = Camera3D.SpringArmFraction(head, desired, 0.2f, 0.3f,
                (Vector3 o, Vector3 dir, float maxT, out float t) =>
                    tw.RayCastWalls(map, o, dir, maxT, wallHeight, out t, out _, out _, out _, includeClosedDoors: true));
        }

        var dt = (float) _timing.FrameTime.TotalSeconds;
        if (fraction < _armFraction || dt <= 0f || dt > 0.25f || float.IsNaN(_armFraction))
            _armFraction = fraction;
        else
            _armFraction += (fraction - _armFraction) * (1f - MathF.Exp(-dt * 6f));

        desired = head + (desired - head) * _armFraction;
        cam.Position = desired;
        _cameraToHead = (desired - head).Length();
        cam.UpdateBasis();
    }

    /// <summary>
    ///     The heading the overhead view starts from: the grid the character stands on, turned by the quarter turns the player chose (the
    ///     dynamic view's rotate keys count them; the overhead camera's turn is added on top).
    /// </summary>
    private double OverheadTargetYaw(EntityUid? player)
    {
        var grid = Angle.Zero;
        if (player is { } p && _xformSys != null && _entMan.TryGetComponent(p, out TransformComponent? xform) && xform.GridUid is { } g)
            grid = _xformSys.GetWorldRotation(g);

        return OverheadCamera.Yaw(grid, OverheadSteps);
    }

    /// <summary>
    ///     The dynamic view: one camera that the wheel moves from first person through third person and isometric to the straight-down view
    ///     (<see cref="DynamicView"/>). It looks at the character from a distance and angle read off the table of stops. Walking, the mouse
    ///     looks around (its pitch adds to the stage's); high up the view turns to the grid's heading and the mouse is a free cursor.
    /// </summary>
    private void UpdateScrollCamera(MapCoordinates eyePos, Vector2i sceneSize)
    {
        var cam = Camera;
        var view = _scrollView;
        cam.Size = new Vector2(sceneSize.X, sceneSize.Y);
        cam.FovY = MathHelper.DegreesToRadians(view.FovDegrees);
        var free = _scrollFree;
        var dt = (float) _timing.FrameTime.TotalSeconds;
        _lastMode = CameraMode.DynamicView;

        var player = _player.LocalEntity;
        if (player != _controlled)
        {
            // New body (spawn, ghosting, possession): face the way it faces.
            _controlled = player;
            _overheadSnap = true;
            _scrollBias = float.NaN;
            if (player is { } p && _entMan.TryGetComponent(p, out TransformComponent? pxform))
                cam.Yaw = _xformSys!.GetWorldRotation(pxform).Theta - Math.PI;
        }

        // The look-down angle the stage asks for, and what the mouse look has added to it since the last frame (cam.Pitch is where the mouse
        // moved it to). Walking, that stays; high up it fades away, so the picture settles on the stage's angle.
        var bias = -MathHelper.DegreesToRadians(view.PitchDown);
        if (float.IsNaN(_scrollBias))
            _scrollBias = cam.Pitch;

        var user = cam.Pitch - _scrollBias;
        if (free)
            user *= dt > 0f && dt < 0.25f ? MathF.Exp(-dt * 8f) : 0f;

        cam.Pitch = Math.Clamp(bias + user, -MathHelper.DegreesToRadians(DynamicView.TopPitch), Camera3D.MaxPitch);
        _scrollBias = bias;

        double targetYaw = 0;
        if (free)
        {
            // Turned to the grid's heading (and the isometric diagonal on the way), and fixed there; the picture glides to it, while the
            // movement keys switch to it at once.
            targetYaw = OverheadTargetYaw(player) + view.IsoWeight * OverheadCamera.IsometricTurn
                + _scrollTurn * DynamicView.TurnWeight(ScrollU);
            if (_turnDragging || _overheadSnap || dt <= 0f || dt > 0.25f || double.IsNaN(cam.Yaw))
                cam.Yaw = targetYaw;
            else
                cam.AddLook((float) Angle.ShortestDistance(new Angle(cam.Yaw), new Angle(targetYaw)).Theta * (1f - MathF.Exp(-dt * 12f)), 0f);
        }

        _overheadSnap = false;
        cam.UpdateBasis();

        if (_yawSystem != null)
            _yawSystem.DesiredYaw = player != null ? new Angle(free ? targetYaw : cam.Yaw) : null;

        var eyeHeight = _cfg.GetCVar(CCVars.Render3DEyeHeight) + HeadBob(eyePos.Position, ScrollU < 0.03f);
        var head = new Vector3(eyePos.X, eyePos.Y, eyeHeight);
        cam.Head = head;

        if (view.Distance < 0.02f)
        {
            // first person
            cam.Position = head;
            _cameraToHead = 0f;
            _armFraction = 1f;
            return;
        }

        var right = new Vector2(cam.Right.X, cam.Right.Y);
        var target = new Vector3(head.X, head.Y, view.TargetHeight);
        var desired = target - cam.Forward * view.Distance + new Vector3(right * view.Side, 0f);

        // Spring arm while the camera is down among the walls (the same one the third person camera has). Its hold lets go as the camera
        // rises over the walls (it is no longer in their way, and a wall below it can only be seen over), so the zoom does not catch on
        // them half way up; and while it lets go, it moves smoothly in both directions instead of snapping in.
        var wallHeight = view.WallHeight;
        var clear = DynamicView.Clearance(desired.Z, wallHeight);
        var fraction = 1f;
        if (_tileWorld != null && clear < 1f)
        {
            var map = eyePos.MapId;
            var tw = _tileWorld;
            fraction = Camera3D.SpringArmFraction(head, desired, 0.2f, 0.3f,
                (Vector3 o, Vector3 dir, float maxT, out float t) =>
                    tw.RayCastWalls(map, o, dir, maxT, wallHeight, out t, out _, out _, out _, includeClosedDoors: true));
            fraction += (1f - fraction) * clear;
        }

        if (dt <= 0f || dt > 0.25f || float.IsNaN(_armFraction))
            _armFraction = fraction;
        else if (fraction < _armFraction)
            _armFraction = clear > 0f
                ? Glide.Smooth(_armFraction, fraction, dt, 16f)
                : fraction; // down among the walls the camera is never let into one
        else
            _armFraction = Glide.Smooth(_armFraction, fraction, dt, 5f);

        desired = head + (desired - head) * _armFraction;
        cam.Position = desired;
        _cameraToHead = (desired - head).Length();
        cam.UpdateBasis();
    }

    private void SetRaymarchParams(GroundLayer ground, TileWorldSystem tileWorld, Vector2i sceneSize, int radius)
    {
        var sh = _raymarch!;
        var cam = Camera;

        var wallHeight = WallHeight;
        // (no ceiling for the high cameras, and for the dynamic view once it has risen to where the ceiling would be in its way)
        var overhead = cam.Elevated || cam.Mode == CameraMode.DynamicView && _scrollNoCeiling;

        // The far cutoff and the fog count from the character; the high cameras hang far above and behind it.
        var shift = ViewDepthShift;
        var maxDist = radius - 0.5f + shift;

        if (ground.GroundTexture is { } groundTex)
            sh.SetParameter("groundTex", groundTex);
        if (ground.LightTarget?.Texture is { } lightTex)
            sh.SetParameter("lightTex", lightTex);
        sh.SetParameter("tileMap", tileWorld.TileMapTexture);
        if (tileWorld.WallAtlas.Texture is { } atlas)
            sh.SetParameter("wallAtlas", atlas);

        sh.SetParameter("camPos", cam.Position);
        sh.SetParameter("camFwd", cam.Forward);
        sh.SetParameter("camRightS", cam.Right * (cam.TanHalfFov * cam.Aspect));
        sh.SetParameter("camUpS", cam.Up * cam.TanHalfFov);
        sh.SetParameter("viewSize", new Vector2(sceneSize.X, sceneSize.Y));
        sh.SetParameter("groundParams", new Vector4(ground.Center.X, ground.Center.Y, ground.WorldSize, 1f / ground.WorldSize));
        sh.SetParameter("sceneParams", new Vector4(wallHeight, maxDist, shift + (maxDist - shift) * 0.7f, DebugView is >= 4 and < 20 or 30 ? DebugView : 0f));
        sh.SetParameter("atlasParams", new Vector4(WallAtlas.SlotsPerRow, 1f / WallAtlas.SlotsPerRow, 0.5f / WallAtlas.SlotPixels, 0f));
        sh.SetParameter("wallUv", ParseUvRect(_cfg.GetCVar(CCVars.Render3DWallUvRect)));
        sh.SetParameter("spaceColor", new Color(0.01f, 0.01f, 0.03f));
        sh.SetParameter("ceilColor", new Color(0.30f, 0.30f, 0.33f));
        sh.SetParameter("aaParams", new Vector4(2f * cam.TanHalfFov / sceneSize.Y, 0, 0, 0));
        sh.SetParameter("fxFlags", new Vector4(
            _cfg.GetCVar(CCVars.Render3DFxSurface) ? 1f : 0f,
            _cfg.GetCVar(CCVars.Render3DFxSky) ? 1f : 0f,
            _cfg.GetCVar(CCVars.Render3DFxHaze) ? 1f : 0f,
            (float) (_timing.RealTime.TotalSeconds % 3600.0)));
        sh.SetParameter("ambientFill", new Vector4(_cfg.GetCVar(CCVars.Render3DFxAmbient) ? 1f : 0f, overhead ? 1f : 0f, shift, 0f));
        sh.SetParameter("tileMapInv", new Vector2(1f / (TileWorldSystem.WindowSize * 2), 1f / (TileWorldSystem.WindowSize * 2)));

        var grids = tileWorld.ActiveGrids;
        SetGrid(sh, 0, grids);
        SetGrid(sh, 1, grids);
        SetGrid(sh, 2, grids);
        SetGrid(sh, 3, grids);
    }

    private static void SetGrid(ShaderInstance sh, int i, List<TileWorldSystem.GridShaderData> grids)
    {
        if (i < grids.Count)
        {
            sh.SetParameter($"gridA{i}", grids[i].A);
            sh.SetParameter($"gridB{i}", grids[i].B);
            sh.SetParameter($"gridC{i}", grids[i].C);
        }
        else
        {
            sh.SetParameter($"gridA{i}", Vector4.Zero);
            sh.SetParameter($"gridB{i}", Vector4.Zero);
            sh.SetParameter($"gridC{i}", Vector4.Zero);
        }
    }

    private static Vector4 ParseUvRect(string value)
    {
        var parts = value.Split(',');
        if (parts.Length == 4
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var c)
            && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return new Vector4(a, b, c, d);
        }

        return new Vector4(0, 0.34f, 1, 1);
    }

    // ---------------------------------------------------------------- IViewportControl

    /// <summary>Pixel (relative to this control) at the centre: where the crosshair is.</summary>
    private Vector2 CrosshairPixel => new Vector2(PixelSize.X, PixelSize.Y) * 0.5f;

    /// <summary>Control-local pixel -> normalised device coordinates (x right, y up, both -1..1).</summary>
    private Vector2 ToNdc(Vector2 localPixel)
    {
        var size = new Vector2(Math.Max(1, PixelSize.X), Math.Max(1, PixelSize.Y));
        return new Vector2(localPixel.X / size.X * 2f - 1f, 1f - localPixel.Y / size.Y * 2f);
    }

    /// <summary>
    ///     Casts the camera ray through a control-local pixel and returns where it lands on the ground plane:
    ///     the floor hit point, the point on a wall face if a wall is hit first, or a point 10 tiles out along the
    ///     ray's heading when it never reaches the floor.
    /// </summary>
    public bool TryCastToGround(Vector2 localPixel, out Vector2 point, out float distance)
    {
        var cam = Camera;
        var ndc = ToNdc(localPixel);
        var dir = cam.Forward + cam.Right * (ndc.X * cam.TanHalfFov * cam.Aspect) + cam.Up * (ndc.Y * cam.TanHalfFov);
        var origin = cam.Position;
        var wallHeight = WallHeight;

        var tFloor = dir.Z < -1e-5f ? -origin.Z / dir.Z : float.PositiveInfinity;
        const float maxRange = 48f;

        if (_tileWorld != null && _lastMap != MapId.Nullspace
            && _tileWorld.RayCastWalls(_lastMap, origin, dir, MathF.Min(tFloor, maxRange), wallHeight,
                out var tWall, out var normal, out _, out _, out _))
        {
            // nudge into the wall tile so the point resolves to the wall (a hit on the top of a wall is already inside it)
            point = new Vector2(origin.X + dir.X * tWall, origin.Y + dir.Y * tWall) - normal * 0.05f;
            distance = tWall;
            return true;
        }

        if (tFloor < maxRange)
        {
            point = new Vector2(origin.X + dir.X * tFloor, origin.Y + dir.Y * tFloor);
            distance = tFloor;
            return true;
        }

        var h = new Vector2(dir.X, dir.Y);
        if (h.LengthSquared() < 1e-8f)
            h = cam.ForwardGround;
        h = Vector2.Normalize(h);
        point = new Vector2(origin.X, origin.Y) + h * 10f;
        distance = 10f;
        return false;
    }

    /// <summary>
    ///     What the crosshair (or free cursor) points at this frame: the map position handed to gameplay code and
    ///     the entities under it, nearest first.
    /// </summary>
    public sealed class PickResult
    {
        public MapCoordinates Coords;
        public readonly List<EntityUid> Entities = new();
        public EntityUid? FirstEntity;
        public float Distance;

        /// <summary>The ray ended on the floor (not a wall, not the sky): <see cref="Coords"/> is a floor point.</summary>
        public bool OnFloor;

        /// <summary>The first thing is a wall, which stands on this tile of this grid.</summary>
        public bool FirstIsWall;
        public EntityUid WallGrid;
        public Vector2i WallTile;
    }

    /// <summary>The most recent pick; <see cref="TryGetClickable"/> matches against it.</summary>
    public PickResult? LastPick { get; private set; }

    private readonly List<EntityPass.PickHit> _hits = new();
    private (long Frame, Vector2 Pixel, PickResult Result)? _pickCache;

    /// <summary>
    ///     Picks through a control-local pixel: entity quads, then walls, then the floor (plan Phase 4). An entity wins
    ///     over a wall/floor within 0.15 tiles of it.
    /// </summary>
    public PickResult Pick(Vector2 localPixel)
    {
        var frame = (long) _timing.CurFrame;
        if (_pickCache is { } c && c.Frame == frame && c.Pixel == localPixel)
        {
            LastPick = c.Result;
            return c.Result;
        }

        var result = new PickResult();
        if (_lastMap == MapId.Nullspace)
        {
            result.Coords = default;
            _pickCache = (frame, localPixel, result);
            LastPick = result;
            return result;
        }

        var cam = Camera;
        var ndc = ToNdc(localPixel);
        var dir = cam.Forward + cam.Right * (ndc.X * cam.TanHalfFov * cam.Aspect) + cam.Up * (ndc.Y * cam.TanHalfFov);
        var origin = cam.Position;
        var wallHeight = WallHeight;

        var onGround = TryCastToGround(localPixel, out var worldPoint, out var worldT);

        // wall under the ray (for targeting walls)
        EntityUid? wallEntity = null;
        var wallGrid = EntityUid.Invalid;
        var wallTile = Vector2i.Zero;
        if (_tileWorld != null
            && _tileWorld.RayCastWalls(_lastMap, origin, dir, MathF.Max(worldT + 0.5f, 0.01f), wallHeight, out _, out _, out wallGrid, out wallTile)
            && _tileWorld.TryGetWallEntity(wallGrid, wallTile, out var wall))
        {
            wallEntity = wall;
        }

        _entityPass?.Pick(origin, dir, _hits);
        DropSeeThroughHits();

        // With the mouse captured the player's own body is in the line of sight of the third-person camera and must
        // not eat every click; it stays pickable with the free cursor (strip/inspect yourself).
        if ((RelativeMouse || DevExcludeSelf) && _player.LocalEntity is { } self)
            _hits.RemoveAll(h => h.Uid == self);

        if (_hits.Count > 0 && Picking3D.EntityBeatsWorld(_hits[0].T, worldT))
        {
            var firstT = _hits[0].T;
            result.Coords = new MapCoordinates(_hits[0].Pos, _lastMap);
            result.FirstEntity = _hits[0].Uid;
            result.Distance = firstT;
            foreach (var h in _hits)
            {
                if (h.T <= firstT + 1.5f)
                    result.Entities.Add(h.Uid);
            }

            if (wallEntity != null && !result.Entities.Contains(wallEntity.Value))
                result.Entities.Add(wallEntity.Value);
        }
        else
        {
            result.Coords = new MapCoordinates(worldPoint, _lastMap);
            result.Distance = worldT;
            result.OnFloor = onGround;
            if (wallEntity != null)
            {
                result.FirstEntity = wallEntity;
                result.Entities.Add(wallEntity.Value);
                result.FirstIsWall = true;
                result.WallGrid = wallGrid;
                result.WallTile = wallTile;
                result.OnFloor = false;
            }
        }

        ApplyPinnedTarget(result, origin);
        _pickCache = (frame, localPixel, result);
        LastPick = result;
        return result;
    }

    /// <summary>
    ///     A thing lying on the floor or a table is drawn on a quad bigger than its picture, and the ray can hit the part where the picture is
    ///     see-through. The classic click code knows which pixels of a sprite count (its click map), so ask it: a hit on a see-through pixel is
    ///     dropped, and the click goes on to what is under it (the table the thing lies on).
    /// </summary>
    private void DropSeeThroughHits()
    {
        if (_xformSys == null || _states.CurrentState is not Content.Client.Gameplay.GameplayStateBase gameplay)
            return;

        for (var i = _hits.Count - 1; i >= 0; i--)
        {
            var hit = _hits[i];
            if (hit.Offset is not { } offset || !_entMan.EntityExists(hit.Uid))
                continue;

            var world = Render3DEngine.DrawnWorldPosition(_xformSys, hit.Uid) + offset;
            if (!gameplay.GetClickableEntities(new MapCoordinates(world, _lastMap), false).Contains(hit.Uid))
                _hits.RemoveAt(i);
        }
    }

    /// <summary>Puts the entity chosen from the list of what the player points at in front of whatever the ray found.</summary>
    private void ApplyPinnedTarget(PickResult result, Vector3 origin)
    {
        if (PinnedTarget is not { } target)
            return;

        if (!_entMan.EntityExists(target) || _xformSys == null || _entMan.GetComponent<TransformComponent>(target).MapID != _lastMap)
        {
            PinnedTarget = null;
            return;
        }

        var world = _xformSys.GetWorldPosition(target);
        if (Vector2.DistanceSquared(world, new Vector2(origin.X, origin.Y)) > PinnedTargetRange * PinnedTargetRange)
        {
            PinnedTarget = null;
            return;
        }

        result.Entities.Remove(target);
        result.Entities.Insert(0, target);
        result.FirstEntity = target;
        result.Coords = new MapCoordinates(world, _lastMap);
    }

    /// <summary>A chosen target further than this (tiles) from the camera is let go.</summary>
    private const float PinnedTargetRange = 9f;

    /// <summary>
    ///     The entities under the last pick, if <paramref name="coordinates"/> is exactly the position that pick
    ///     returned from <see cref="PixelToMap"/> (that is how the 2D click code asks about the same spot).
    /// </summary>
    public bool TryGetClickable(MapCoordinates coordinates, out IReadOnlyList<EntityUid> entities)
    {
        if (LastPick is { } last && last.Coords.MapId == coordinates.MapId && last.Coords.Position == coordinates.Position)
        {
            entities = last.Entities;
            return true;
        }

        entities = Array.Empty<EntityUid>();
        return false;
    }

    private MapCoordinates PixelToMapInternal(Vector2 screenPixel)
    {
        if (_lastMap == MapId.Nullspace)
            return default;

        var local = RelativeMouse ? CrosshairPixel : screenPixel - GlobalPixelPosition;
        return Pick(local).Coords;
    }

    /// <summary>
    ///     Pure geometry: where the camera ray through an (absolute) screen pixel meets the ground plane, ignoring
    ///     walls, entities and the crosshair. Distances are clamped to <see cref="ScreenToMapRange"/> so rays above
    ///     the horizon give a point that is still farther than any nearer pixel. That keeps the screen corners a
    ///     well-formed rectangle for callers such as <c>EyeManager.GetWorldViewbounds</c>.
    /// </summary>
    public MapCoordinates ScreenToMap(Vector2 coords)
    {
        if (_lastMap == MapId.Nullspace)
            return default;

        var cam = Camera;

        // EyeManager.GetWorldViewbounds asks for exactly the top-right and bottom-left corner of the viewport and
        // builds a box from them after rotating by the eye rotation. The true far and near corners only form a valid
        // box for some camera headings, and forks that call it every frame (for example to cull lights) then trip
        // the engine's box assertion. Answer those two queries with the corners of a square around the camera that
        // covers everything we draw, pre-rotated so that the engine's un-rotation gives an axis-aligned box.
        var size = PixelSize;
        var corner = new Vector2(ScreenToMapRange, ScreenToMapRange);
        if (coords.Y == 0 && coords.X == size.X)
            return new MapCoordinates(new Vector2(cam.Position.X, cam.Position.Y) + new Angle(-_eyeManager.CurrentEye.Rotation).RotateVec(corner), _lastMap);

        if (coords.X == 0 && coords.Y == size.Y)
            return new MapCoordinates(new Vector2(cam.Position.X, cam.Position.Y) - new Angle(-_eyeManager.CurrentEye.Rotation).RotateVec(corner), _lastMap);

        var ndc = ToNdc(coords - GlobalPixelPosition);
        var dir = cam.Forward + cam.Right * (ndc.X * cam.TanHalfFov * cam.Aspect) + cam.Up * (ndc.Y * cam.TanHalfFov);

        // depth along the camera forward axis (dir has forward component 1), clamped
        var t = dir.Z < -1e-5f ? MathF.Min(-cam.Position.Z / dir.Z, ScreenToMapRange) : ScreenToMapRange;
        return new MapCoordinates(new Vector2(cam.Position.X + dir.X * t, cam.Position.Y + dir.Y * t), _lastMap);
    }

    private const float ScreenToMapRange = 30f;

    public MapCoordinates PixelToMap(Vector2 point) => PixelToMapInternal(point);

    /// <summary>
    ///     Whether a floating screen-space label for the world point (at the UI anchor height) should be shown: in front
    ///     of the camera, within <paramref name="maxDistance"/> tiles and not behind a wall. Labels are not scaled by
    ///     distance like sprites, so this keeps rows of far-away labels from piling up on the horizon.
    /// </summary>
    public bool IsLabelVisible(Vector2 map, float maxDistance)
    {
        if (_lastMap == MapId.Nullspace)
            return false;

        var anchor = new Vector3(map, _cfg.GetCVar(CCVars.Render3DUiAnchorHeight));
        var cam = Camera;
        if (!cam.Project(anchor, out _, out var depth))
            return false;

        // the overhead camera hangs far from the character: count from the character, not from the camera
        if (depth - ViewDepthShift > maxDistance)
            return false;

        return _tileWorld == null
            || !_tileWorld.RayCastWalls(_lastMap, cam.Position, anchor - cam.Position, 0.97f,
                WallHeight, out _, out _, out _, out _);
    }

    public Vector2 WorldToScreen(Vector2 map)
    {
        var anchor = _cfg.GetCVar(CCVars.Render3DUiAnchorHeight);
        return WorldToScreen(new Vector3(map, anchor));
    }

    public Vector2 WorldToScreen(Vector3 world)
    {
        var p = Camera.ProjectLoose(world);
        var size = Camera.Size;
        if (size.X < 1 || size.Y < 1)
            return GlobalPixelPosition;

        var local = p / size * new Vector2(PixelSize.X, PixelSize.Y);
        return local + GlobalPixelPosition;
    }

    /// <summary>
    ///     There is no exact affine map from the ground plane to the screen under perspective, so this is the best
    ///     affine approximation around the point under the crosshair (finite differences over one tile).
    /// </summary>
    public Matrix3x2 GetWorldToScreenMatrix()
    {
        var p0 = PixelToMapInternal(GlobalPixelPosition + CrosshairPixel).Position;

        var s0 = WorldToScreen(p0);
        var sx = WorldToScreen(p0 + Vector2.UnitX) - s0;
        var sy = WorldToScreen(p0 + Vector2.UnitY) - s0;

        return new Matrix3x2(
            sx.X, sx.Y,
            sy.X, sy.Y,
            s0.X - p0.X * sx.X - p0.Y * sy.X,
            s0.Y - p0.X * sx.Y - p0.Y * sy.Y);
    }

    public Matrix3x2 GetLocalToScreenMatrix()
    {
        return Matrix3x2.CreateTranslation(GlobalPixelPosition);
    }
}
