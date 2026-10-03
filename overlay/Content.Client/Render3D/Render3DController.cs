using Content.Client.Administration.Managers;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
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

    private Render3DViewportControl? _control;
    private Render3DCompat? _compat;
    private MainViewport? _host;
    private bool _freeKeyHeld;
    private bool _relative;

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
        InitializeQuality();

        _console.RegisterCommand("render3d_settings", "Open the 3D view settings window", "render3d_settings",
            (_, _, _) => ToggleSettingsWindow());

    }

    public void OnStateEntered(GameplayState state)
    {
        Render3DKeys.Register(_input);

        CommandBinds.Builder
            .Bind(Render3DKeys.ToggleView, new Render3DInputHandler(down => { if (down) ToggleView(); }, consume: true))
            .Bind(Render3DKeys.ToggleCameraMode, new Render3DInputHandler(down => { if (down) ToggleCameraMode(); }, consume: true))
            .Bind(Render3DKeys.OpenSettings, new Render3DInputHandler(down => { if (down) ToggleSettingsWindow(); }, consume: true))
            .Bind(Render3DKeys.FreeCursor, new Render3DInputHandler(down => _freeKeyHeld = down, consume: false))
            .Bind(ContentKeyFunctions.ZoomIn, new Render3DInputHandler(down => { if (down) AdjustDistance(-0.2f); }, () => Active))
            .Bind(ContentKeyFunctions.ZoomOut, new Render3DInputHandler(down => { if (down) AdjustDistance(0.2f); }, () => Active))
            .Register<Render3DController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<Render3DController>();
        _freeKeyHeld = false;
        SetRelative(false);
    }

    private Render3DSettingsWindow? _settingsWindow;

    private void ToggleSettingsWindow()
    {
        if (_settingsWindow is { Disposed: false, IsOpen: true })
        {
            _settingsWindow.Close();
            return;
        }

        _settingsWindow = new Render3DSettingsWindow();
        _settingsWindow.OpenCentered();
    }

    private void OnScreenLoad()
    {
        var host = UIManager.ActiveScreen?.GetWidget<MainViewport>();
        if (host == null)
            return;

        OnScreenUnload();

        _host = host;
        _control = new Render3DViewportControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Visible = false,
        };
        _control.Camera.Mode = _cfg.GetCVar(CCVars.Render3DFirstPerson) ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
        host.AddChild(_control);
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

    private void ToggleCameraMode()
    {
        if (_control == null || !Active)
            return;

        var first = _control.Camera.Mode != CameraMode.FirstPerson;
        _control.Camera.Mode = first ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
        _cfg.SetCVar(CCVars.Render3DFirstPerson, first);
    }

    private void AdjustDistance(float delta)
    {
        if (!Active)
            return;

        var d = Math.Clamp(_cfg.GetCVar(CCVars.Render3DThirdPersonDistance) + delta, 0.8f, 3.0f);
        _cfg.SetCVar(CCVars.Render3DThirdPersonDistance, d);
    }

    // ---- per frame ----

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_control == null || _host == null)
            return;

        var want = ShouldBeActive();
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

        UpdateMouseMode();
        UpdateAutoQuality(args.DeltaSeconds);
    }

    private void UpdateMouseMode()
    {
        var control = _control;
        var want = Active
            && control is { VisibleInTree: true }
            && (_clyde.MainWindow.IsFocused || DevForceCapture)
            && !_freeKeyHeld
            && UIManager.KeyboardFocused == null
            && !AnyWindowOrPopupOpen()
            && _player.LocalEntity != null;

        SetRelative(want);

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

    private void SetRelative(bool relative)
    {
        if (_relative == relative)
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
