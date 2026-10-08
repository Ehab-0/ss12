using System.Linq;
using Content.Client.Administration.Managers;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.Viewport;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>
///     Decides whether the game is shown in 3D, owns the <see cref="Render3DViewportControl"/> that sits next to the
///     2D <c>ScalingViewport</c> inside the gameplay screen's <see cref="MainViewport"/>, swaps which of the two is
///     visible and registered as <see cref="IEyeManager.MainViewport"/>, and runs the mouse-look / free-cursor
///     lifecycle of the 3D camera.
/// </summary>
public sealed partial class Render3DController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private Robust.Shared.Log.ILogManager _logManager = default!;

    private Render3DViewportControl? _control;
    private Render3DCompat? _compat;
    private MainViewport? _host;

    // the dynamic view's end in the classic 2D view (see UpdateScrollHost)
    private Render3DScrollCatcher? _scrollCatcher;
    private bool _scrollWasTwoD;
    private bool _freeKeyHeld;
    private bool _relative;

    // mouse capture bookkeeping (see UpdateMouseMode)
    private MouseCaptureReason _captureReason = MouseCaptureReason.NotShown;
    private TimeSpan _focusedAt;
    private TimeSpan _lastRequest;
    private Robust.Shared.Log.ISawmill _captureLog = default!;

    /// <summary>Dev channel only: behave as if the window had focus, so the crosshair path can be tested unattended.</summary>
    public bool DevForceCapture;

    public Render3DViewportControl? Control => _control;

    /// <summary>True while the 3D view is the one being shown.</summary>
    public bool Active { get; private set; }

    /// <summary>True while the mouse is captured for mouse-look (the cursor is hidden and the crosshair is live).</summary>
    public bool RelativeMouse => _relative;

    public override void Initialize()
    {
        base.Initialize();

        var load = UIManager.GetUIController<GameplayStateLoadController>();
        load.OnScreenLoad += OnScreenLoad;
        load.OnScreenUnload += OnScreenUnload;
        _captureLog = _logManager.GetSawmill("render3d.mouse");
        _clyde.OnWindowFocused += OnWindowFocused;
        _cfg.OnValueChanged(CCVars.Render3DOverhead, _ => ApplyCameraMode());
        _cfg.OnValueChanged(CCVars.Render3DFirstPerson, _ => ApplyCameraMode());
        _cfg.OnValueChanged(CCVars.Render3DCameraExtra, _ => ApplyCameraMode());
        _console.RegisterCommand("render3d_capture_state", "Say why the mouse is or is not captured for mouse-look", "render3d_capture_state",
            (shell, _, _) => shell.WriteLine($"mouse capture: {_captureReason}, relative={_relative}, focused={_clyde.MainWindow.IsFocused}, "
                + $"freeKey={_freeKeyHeld}, uiFocus={UIManager.KeyboardFocused?.GetType().Name ?? "none"}, window open={AnyWindowOrPopupOpen()}"));
        InitializeQuality();
        InitializeMinimap();
        InitializePointList();

        _console.RegisterCommand("render3d_settings", "Open the 3D view settings window, on a tab if one is named (general, camera, interface, controls, graphics)",
            "render3d_settings [tab]",
            (_, _, args) => ToggleSettingsWindow(args.Length > 0 ? args[0] : null));
        _console.RegisterCommand("render3d_dynamic_view", "Put the dynamic view at a position: 0 is first person, 1 the classic 2D view", "render3d_dynamic_view <0 to 1>",
            (shell, _, args) =>
            {
                if (args.Length != 1 || !float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var u))
                {
                    shell.WriteError("usage: render3d_dynamic_view <0 to 1>");
                    return;
                }

                _control?.ScrollTo(u);
            });
        _console.RegisterCommand("render3d_tune", "Open the developer tuning window: a slider for every client-side render3d.* setting", "render3d_tune",
            (_, _, _) => ToggleTuneWindow());

    }

    public void OnStateEntered(GameplayState state)
    {
        Render3DKeys.Register(_input);

        CommandBinds.Builder
            .Bind(Render3DKeys.ToggleView, new Render3DInputHandler(down => { if (down) ToggleView(); }, consume: true))
            .Bind(Render3DKeys.ToggleCameraMode, new Render3DInputHandler(down => { if (down) ToggleCameraMode(); }, consume: true))
            .Bind(Render3DKeys.ToggleOverhead, new Render3DInputHandler(down => { if (down) ToggleOverhead(); }, consume: true))
            .Bind(Render3DKeys.CycleCamera, new Render3DInputHandler(down => { if (down) CycleCamera(); }, consume: true))
            .Bind(Render3DKeys.ToggleFollow, new Render3DInputHandler(down => { if (down) ToggleFollowCamera(); }, consume: true))
            // the rotate keys turn the high views (the overhead camera by eighth turns, the dynamic view by quarter turns); they go before the mover's own handlers, which would turn the mover alone
            .BindBefore(EngineKeyFunctions.CameraRotateLeft, new Render3DInputHandler(down => { if (down) TurnOverhead(1); }, () => Active && ElevatedOn), typeof(SharedMoverController))
            .BindBefore(EngineKeyFunctions.CameraRotateRight, new Render3DInputHandler(down => { if (down) TurnOverhead(-1); }, () => Active && ElevatedOn), typeof(SharedMoverController))
            .BindBefore(EngineKeyFunctions.CameraReset, new Render3DInputHandler(down => { if (down) TurnOverhead(0); }, () => Active && ElevatedOn), typeof(SharedMoverController))
            .Bind(Render3DKeys.OpenSettings, new Render3DInputHandler(down => { if (down) ToggleSettingsWindow(); }, consume: true))
            .Bind(Render3DKeys.Minimap, new Render3DInputHandler(down => { if (down) ToggleMinimap(); }, consume: true))
            .Bind(Render3DKeys.MinimapSize, new Render3DInputHandler(down => { if (down) SwitchMinimapSize(); }, consume: true))
            .Bind(Render3DKeys.PointList, new Render3DInputHandler(down => { if (down) TogglePointList(); }, consume: true))
            .Bind(Render3DKeys.PointListUp, new Render3DInputHandler(down => { if (down) MovePointList(-1); }, () => PointListVisible))
            .Bind(Render3DKeys.PointListDown, new Render3DInputHandler(down => { if (down) MovePointList(1); }, () => PointListVisible))
            .Bind(EngineKeyFunctions.Use, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(EngineKeyFunctions.UseSecondary, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(ContentKeyFunctions.UseItemInHand, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(ContentKeyFunctions.AltUseItemInHand, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(ContentKeyFunctions.ActivateItemInWorld, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(ContentKeyFunctions.AltActivateItemInWorld, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(ContentKeyFunctions.TryPullObject, new Render3DInputHandler(down => { NoteInteraction(); if (!down) ReleasePointTarget(); }, consume: false))
            .Bind(Render3DKeys.PointListSelect, new Render3DInputHandler(down => { if (down) SelectInPointList(); }, () => PointListVisible))
            .Bind(Render3DKeys.FreeCursor, new Render3DInputHandler(down => _freeKeyHeld = down, consume: false))
            .Bind(ContentKeyFunctions.ZoomIn, new Render3DInputHandler(down => { if (down) AdjustDistance(-0.2f); }, () => UsableNow))
            .Bind(ContentKeyFunctions.ZoomOut, new Render3DInputHandler(down => { if (down) AdjustDistance(0.2f); }, () => UsableNow))
            .Register<Render3DController>();

        MaybeOpenSettingsOnJoin();
        StartAutoQualityOnJoin();
    }

    /// <summary>Opens the settings window by itself when the server asks for it (render3d.settings_on_join).</summary>
    private void MaybeOpenSettingsOnJoin()
    {
        var mode = _cfg.GetCVar(CCVars.Render3DSettingsOnJoin);
        if (mode <= 0 || mode == 1 && _cfg.GetCVar(CCVars.Render3DSettingsShown))
            return;

        // give the screen, the viewport and the player's body a moment to appear first
        Timer.Spawn(TimeSpan.FromSeconds(2), () =>
        {
            if (!Active || _settingsWindow is { Disposed: false, IsOpen: true })
                return;

            _cfg.SetCVar(CCVars.Render3DSettingsShown, true);
            ToggleSettingsWindow();
        });
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<Render3DController>();
        _freeKeyHeld = false;
        SetRelative(false);
    }

    private Render3DSettingsWindow? _settingsWindow;
    private Render3DTuneWindow? _tuneWindow;

    private void ToggleTuneWindow()
    {
        if (_tuneWindow is { Disposed: false, IsOpen: true })
        {
            _tuneWindow.Close();
            return;
        }

        _tuneWindow = new Render3DTuneWindow();
        _tuneWindow.OpenToRight();
    }

    /// <summary>Opens the settings window, or closes it if it is open. With a tab name (see <see cref="Render3DSettingsWindow.TabNames"/>) it opens on that tab, or switches to it if it is already open.</summary>
    private void ToggleSettingsWindow(string? tab = null)
    {
        if (_settingsWindow is { Disposed: false, IsOpen: true })
        {
            if (tab != null && _settingsWindow.ShowTab(tab))
                return;

            _settingsWindow.Close();
            return;
        }

        _settingsWindow = new Render3DSettingsWindow();
        if (tab != null)
            _settingsWindow.ShowTab(tab);
        _settingsWindow.OpenCentered();
    }

    private void OnScreenLoad()
    {
        var host = UIManager.ActiveScreen?.GetWidget<MainViewport>();
        if (host == null)
            return;

        OnScreenUnload();

        // A screen that is loaded again must never end up with two 3D views side by side (seen once after a respawn
        // through the lobby): take away any view this host still has before adding the new one.
        foreach (var stale in host.Children.OfType<Render3DViewportControl>().ToList())
            host.RemoveChild(stale);
        foreach (var stale in host.Viewport.Children.OfType<Render3DScrollCatcher>().ToList())
            host.Viewport.RemoveChild(stale);

        _host = host;
        _control = new Render3DViewportControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
        };
        ApplyCameraMode();
        host.AddChild(_control);
        AddMinimap(_control);
        AddPointList(_control);

        _scrollCatcher = new Render3DScrollCatcher
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Target = host.Viewport,
            Wheel = notches => _control?.ScrollBy(notches),
        };
        // a child of the 2D viewport, not of the host (a box container, which would give it a share of the width)
        host.Viewport.AddChild(_scrollCatcher);
        _scrollWasTwoD = false;
        Active = false;
    }

    private void OnScreenUnload()
    {
        SetRelative(false);

        if (_control != null)
        {
            if (_eyeManager.MainViewport == _control)
                _eyeManager.MainViewport = UIManager.MainViewport;

            // Don't touch the child collection here: the screen unload that follows removes MainViewport (and with it
            // our control, whose ExitedTree disposes its render targets), and unloading can happen mid-frame.
            _control = null;
        }

        _minimap = null;
        _scrollCatcher = null;
        _scrollWasTwoD = false;
        _host = null;
        Active = false;
        Render3DPointer.Shown = false;
    }

    // ---- enforcement ----

    /// <summary>
    ///     Whether the local player may use the classic 2D view: always when the server doesn't enforce 3D, and for
    ///     ghosts/observers and active admins when it does (plan Phase 6).
    /// </summary>
    public bool CanUse2D()
    {
        var ent = _player.LocalEntity;
        return CanUse2D(
            _cfg.GetCVar(CCVars.Render3DEnforced),
            ent != null,
            ent != null && (_compat ??= new Render3DCompat(_entMan)).IsGhost(ent.Value),
            _admin.IsActive());
    }

    /// <summary>The enforcement rule on its own (unit tested): see <see cref="CanUse2D()"/>.</summary>
    public static bool CanUse2D(bool enforced, bool hasBody, bool isGhost, bool isActiveAdmin)
    {
        if (!enforced)
            return true;

        // lobby / no body, observers and active admins may always use the classic view
        return !hasBody || isGhost || isActiveAdmin;
    }

    public bool ShouldBeActive()
    {
        return !CanUse2D() || _cfg.GetCVar(CCVars.Render3DEnabled);
    }

    private void ToggleView()
    {
        if (!CanUse2D())
            return;

        _cfg.SetCVar(CCVars.Render3DEnabled, !_cfg.GetCVar(CCVars.Render3DEnabled));
    }

    /// <summary>A camera high above the character is on (overhead, or the dynamic view once it is high).</summary>
    private bool ElevatedOn => _control is { Elevated: true };

    /// <summary>The camera the settings ask for (see <see cref="CameraModes.FromSettings"/>). Called when any of them changes.</summary>
    private void ApplyCameraMode()
    {
        if (_control == null)
            return;

        _control.Camera.Mode = CameraModes.FromSettings(
            _cfg.GetCVar(CCVars.Render3DFirstPerson), _cfg.GetCVar(CCVars.Render3DOverhead), _cfg.GetCVar(CCVars.Render3DCameraExtra));
    }

    /// <summary>Switches to a camera: sets the three settings that store it (the change is applied by <see cref="ApplyCameraMode"/>).</summary>
    public void SetCameraMode(CameraMode mode)
    {
        var (first, overhead, extra) = CameraModes.ToSettings(mode, _cfg.GetCVar(CCVars.Render3DFirstPerson));
        _cfg.SetCVar(CCVars.Render3DCameraExtra, extra);
        _cfg.SetCVar(CCVars.Render3DOverhead, overhead);
        _cfg.SetCVar(CCVars.Render3DFirstPerson, first);
    }

    private CameraMode CurrentCameraMode => _control?.Camera.Mode ?? CameraMode.ThirdPerson;

    /// <summary>The 3D view is shown, or the dynamic view is at its 2D end (the camera keys still work there, so the camera can be changed or scrolled back).</summary>
    private bool UsableNow => Active || _control is { ScrollTwoD: true };

    private void SwitchCamera(CameraMode mode)
    {
        if (_control == null || !UsableNow)
            return;

        SetCameraMode(mode);
        _control.ShowNotice(Robust.Shared.Localization.Loc.GetString("render3d-notice-camera",
            ("camera", Robust.Shared.Localization.Loc.GetString(CameraModes.LocId(mode)))), 2.5f);
    }

    /// <summary>The key that switches between first and third person; from any other camera it goes back to the walking one.</summary>
    private void ToggleCameraMode()
        => SwitchCamera(CameraModes.ToggleWalking(CurrentCameraMode, _cfg.GetCVar(CCVars.Render3DFirstPerson)));

    private void ToggleOverhead()
        => SwitchCamera(CurrentCameraMode == CameraMode.Overhead
            ? CameraModes.ToggleWalking(CameraMode.Overhead, _cfg.GetCVar(CCVars.Render3DFirstPerson))
            : CameraMode.Overhead);

    /// <summary>The camera key: the next camera in <see cref="CameraModes.Cycle"/>.</summary>
    private void CycleCamera() => SwitchCamera(CameraModes.Next(CurrentCameraMode));

    private void ToggleFollowCamera()
    {
        if (_control == null || !UsableNow)
            return;

        var on = !_cfg.GetCVar(CCVars.Render3DFollowCamera);
        _cfg.SetCVar(CCVars.Render3DFollowCamera, on);
        _control.ShowNotice(Robust.Shared.Localization.Loc.GetString(on ? "render3d-notice-follow-on" : "render3d-notice-follow-off"), 2.5f);
    }

    /// <summary>Turns the high views a step (1 = to the left, -1 = to the right), or back to the grid's north (0).</summary>
    private void TurnOverhead(int quarterTurns) => _control?.TurnView(quarterTurns);

    private void AdjustDistance(float delta)
    {
        if (!UsableNow)
            return;

        if (CurrentCameraMode == CameraMode.DynamicView)
        {
            // the zoom keys do what the wheel does (a notch per press): zoom in, closer to the character
            _control?.ScrollBy(-delta * 5f);
            return;
        }

        if (ElevatedOn)
        {
            // the zoom keys move the high cameras in and out: one tile per press
            _cfg.SetCVar(CCVars.Render3DOverheadDistance,
                Math.Clamp(_cfg.GetCVar(CCVars.Render3DOverheadDistance) + delta * 5f, OverheadCamera.MinDistance, OverheadCamera.MaxDistance));
            return;
        }

        var d = Math.Clamp(_cfg.GetCVar(CCVars.Render3DThirdPersonDistance) + delta, 0.8f, 3.0f);
        _cfg.SetCVar(CCVars.Render3DThirdPersonDistance, d);
    }

    // ---- the dynamic view's end in the classic 2D view ----

    /// <summary>How many tiles tall the classic 2D view is, so the straight-down 3D view can match its scale (15 when that is not known).</summary>
    private float TwoDTilesNow()
    {
        if (_host == null)
            return 15f;

        var height = _host.Viewport.ViewportSize.Y;
        var zoom = _eyeManager.CurrentEye.Zoom.Y;
        return height > 0 && zoom > 0f ? height / (GroundLayer.PixelsPerTile * zoom) : 15f;
    }

    /// <summary>
    ///     Near the end of the scroll the 3D view blends the classic 2D picture over itself (it draws that with a viewport of its own, which needs
    ///     the shape of the real 2D viewport: its size and how it is fitted into the window, handed over here). At the very end the 3D view is
    ///     switched off and the real 2D viewport is the main one (see <see cref="FrameUpdate"/>), with a transparent control over it that takes
    ///     the mouse wheel (the 2D viewport itself ignores it) and hands the rest on.
    /// </summary>
    private void UpdateScrollHost(bool scrollMode)
    {
        if (_host == null || _control == null)
            return;

        var viewport = _host.Viewport;
        _control.TwoDViewportSize = viewport.ViewportSize;
        _control.TwoDFixedStretch = viewport.FixedStretchSize;
        _control.TwoDIgnoreDimension = (int) viewport.IgnoreDimension;
        _control.TwoDBilinear = viewport.StretchMode == ScalingViewportStretchMode.Bilinear;

        if (_scrollCatcher != null)
            _scrollCatcher.Visible = scrollMode && !Active && _control.ScrollTwoD;
    }

    // ---- per frame ----

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_control == null || _host == null)
            return;

        // The dynamic view moves with the wheel: it glides, and at the end of the scroll the classic 2D view takes over (also while that
        // is what is shown, so the wheel can bring the camera back). A server that makes 3D mandatory stops the scroll before it.
        var scrollMode = _control.Camera.Mode == CameraMode.DynamicView;
        _control.TwoDTiles = TwoDTilesNow();
        _control.TickScroll(args.DeltaSeconds, scrollMode && _cfg.GetCVar(CCVars.Render3DDynamicView2D) && CanUse2D());
        var scrollTwoD = scrollMode && _control.ScrollTwoD;
        if (scrollTwoD != _scrollWasTwoD)
        {
            _scrollWasTwoD = scrollTwoD;
            if (scrollTwoD)
                _control.ReleaseYaw();
            else
                _control.SyncStepsFromMover();
        }

        var want = ShouldBeActive() && !scrollTwoD;
        if (want != Active)
            SetActive(want);

        if (Active)
        {
            // ViewportUIController re-registers the 2D viewport when the screen reloads; make sure we win.
            if (_eyeManager.MainViewport != _control)
                _eyeManager.MainViewport = _control;

            _control.DebugView = _cfg.GetCVar(CCVars.Render3DDebugView);
            _control.Sensitivity = _cfg.GetCVar(CCVars.Render3DMouseSensitivity);
            _control.InvertY = _cfg.GetCVar(CCVars.Render3DInvertY);
        }

        UpdateScrollHost(scrollMode);
        UpdateMouseMode();
        UpdateWindowPlacement();
        UpdateAutoQuality(args.DeltaSeconds);
        UpdateMinimap();
        UpdatePointList();
    }

    /// <summary>
    ///     The window got or lost focus (alt+tab). The state of the free-mouse key is not to be trusted across it, and the request
    ///     for the relative mouse mode is made again from scratch.
    /// </summary>
    private void OnWindowFocused(WindowFocusedEventArgs args)
    {
        if (args.Window != _clyde.MainWindow)
            return;

        _freeKeyHeld = false;
        if (args.Focused)
        {
            _focusedAt = _timing.RealTime;
            _lastRequest = TimeSpan.Zero;
            // something (the chat box) can end up with the keyboard focus across an alt+tab and then keeps the mouse free
            if (Active && _control is { VisibleInTree: true } && UIManager.KeyboardFocused != null && !AnyWindowOrPopupOpen())
                UIManager.ReleaseKeyboardFocus();
        }

        SetRelative(false, force: true);
        _captureLog.Verbose($"window {(args.Focused ? "gained" : "lost")} focus");
    }

    private void UpdateMouseMode()
    {
        var control = _control;
        var reason = MouseCapturePolicy.Decide(
            Active && control is { VisibleInTree: true },
            _clyde.MainWindow.IsFocused || DevForceCapture,
            _freeKeyHeld,
            UIManager.KeyboardFocused != null,
            AnyWindowOrPopupOpen(),
            _player.LocalEntity != null,
            control is { Elevated: true });
        var want = reason == MouseCaptureReason.Capture;

        if (reason != _captureReason)
        {
            _captureLog.Verbose($"mouse capture: {reason}"
                + (reason == MouseCaptureReason.UiFocus ? $" ({UIManager.KeyboardFocused?.GetType().Name})" : string.Empty));
            _captureReason = reason;
        }

        SetRelative(want);
        if (want && _clyde.MainWindow.IsFocused)
        {
            // repeat the request for a while after the focus came back, then now and then (see MouseCapturePolicy.ReassertInterval)
            var now = _timing.RealTime;
            var interval = TimeSpan.FromSeconds(MouseCapturePolicy.ReassertInterval((float) (now - _focusedAt).TotalSeconds));
            if (now - _lastRequest >= interval)
            {
                _lastRequest = now;
                _clyde.MainWindow.SetRelativeMouseMode(true);
            }
        }

        if (_relative && control != null)
        {
            // In relative mode the OS cursor stays where it was, so force input routing and hover onto the viewport.
            UIManager.ControlFocused = control;
            UIManager.SetHovered(control);
        }
        else if (control != null && UIManager.ControlFocused == control)
        {
            UIManager.ControlFocused = null;
        }
    }

    private bool AnyWindowOrPopupOpen()
    {
        foreach (var child in UIManager.WindowRoot.Children)
        {
            if (child.Visible)
                return true;
        }

        foreach (var child in UIManager.ModalRoot.Children)
        {
            if (child.Visible)
                return true;
        }

        return false;
    }

    private void SetRelative(bool relative, bool force = false)
    {
        if (_relative == relative && !force)
            return;

        _relative = relative;
        // DevForceCapture only exists for scripted tests of an unfocused window; never grab a real mouse for it.
        if (!relative || _clyde.MainWindow.IsFocused)
            _clyde.MainWindow.SetRelativeMouseMode(relative);

        if (_control != null)
            _control.RelativeMouse = relative;

        Render3DPointer.Captured = relative ? _control : null;
    }

    private void SetActive(bool active)
    {
        if (_control == null || _host == null)
            return;

        Active = active;
        Render3DPointer.Shown = active;
        _control.Visible = active;
        _host.Viewport.Visible = !active;

        if (active)
        {
            _eyeManager.MainViewport = _control;
        }
        else
        {
            _eyeManager.MainViewport = _host.Viewport;
            SetRelative(false);
        }
    }
}
