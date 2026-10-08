using System.Linq;
using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>
///     Settings for the 3D view, built from engine controls only (no Content option infrastructure), so it works in any
///     server codebase the 3D view is installed into. Changes apply immediately. Opened with the
///     <c>render3d_settings</c> command or the Render3DSettings key.
/// </summary>
/// <remarks>
///     Five tabs: General (on/off, quality preset, render scale), Camera (the camera, and only the settings that camera has), Interface
///     (crosshair, pointing list, minimap), Controls (the keys) and Graphics (the individual effects and the shape of things).
/// </remarks>
public sealed partial class Render3DSettingsWindow : DefaultWindow
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    // The tabs are a row of toggle buttons that swap pages (not the engine's TabContainer, whose header layout asserted in a debug build).
    private readonly BoxContainer _tabBar;
    private readonly Control _pages;
    private readonly ButtonGroup _tabGroup = new();
    private readonly List<(string Key, Button Button, Control Page)> _tabList = new();

    /// <summary>The names of the tabs, as the <c>render3d_settings</c> command takes them.</summary>
    public static readonly string[] TabNames = { "general", "camera", "interface", "controls", "graphics" };

    /// <summary>The page (or section of a page) the Add... helpers put their controls into.</summary>
    private BoxContainer _body;

    private readonly List<(BoundKeyFunction Function, Button Button)> _keyButtons = new();
    private BoundKeyFunction? _capturing;
    private readonly List<(Render3DQuality.Effect Effect, CheckBox Box)> _fxBoxes = new();
    private OptionButton? _qualityButton;
    private OptionButton? _cameraButton;
    private readonly Dictionary<CameraMode, BoxContainer> _cameraSections = new();
    private Control? _dynamicBox;
    private bool _refreshing;

    public Render3DSettingsWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("render3d-settings-title");
        MinSize = new Vector2(500, 560);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, HorizontalExpand = true, SeparationOverride = 4 };
        _tabBar = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        _pages = new Control { VerticalExpand = true, HorizontalExpand = true };
        root.AddChild(_tabBar);
        root.AddChild(_pages);
        ContentsContainer.AddChild(root);
        _body = null!;

        BuildGeneral();
        BuildCamera();
        BuildInterface();
        BuildControls();
        BuildGraphics();
        ShowTab(0);
    }

    // ---------------------------------------------------------------- pages

    /// <summary>Adds a scrolling tab page with its button in the tab bar, and makes the page the target of the Add... helpers.</summary>
    private BoxContainer NewPage(string titleLocId)
    {
        var index = _tabList.Count;
        var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 4 };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false, Visible = false };
        scroll.AddChild(page);
        _pages.AddChild(scroll);

        var button = new Button
        {
            Text = Loc.GetString(titleLocId),
            ToggleMode = true,
            Group = _tabGroup,
            HorizontalExpand = true,
        };
        button.OnPressed += _ => ShowTab(index);
        _tabBar.AddChild(button);

        _tabList.Add((TabNames[index], button, scroll));
        _body = page;
        return page;
    }

    /// <summary>Shows a tab by its number.</summary>
    public void ShowTab(int index)
    {
        index = Math.Clamp(index, 0, _tabList.Count - 1);
        for (var i = 0; i < _tabList.Count; i++)
        {
            _tabList[i].Page.Visible = i == index;
            _tabList[i].Button.Pressed = i == index;
        }
    }

    /// <summary>Shows a tab by name (see <see cref="TabNames"/>); an unknown name leaves the tab as it is. Returns whether it was found.</summary>
    public bool ShowTab(string name)
    {
        for (var i = 0; i < _tabList.Count; i++)
        {
            if (!string.Equals(_tabList[i].Key, name, StringComparison.OrdinalIgnoreCase))
                continue;

            ShowTab(i);
            return true;
        }

        return false;
    }

    private void BuildGeneral()
    {
        NewPage("render3d-tab-general");
        AddCheck(CCVars.Render3DEnabled, "render3d-settings-use-3d");
        AddHeader("render3d-settings-performance");
        AddQualityRow();
        AddCheck(CCVars.Render3DAutoQuality, "render3d-settings-auto-quality");
        AddSlider(CCVars.Render3DRenderScale, "render3d-settings-render-scale", 0.25f, 1f, "0.00");
    }

    private void BuildCamera()
    {
        var page = NewPage("render3d-tab-camera");

        // the camera, and the settings only some of the cameras have
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new Label { Text = Loc.GetString("render3d-settings-camera"), MinWidth = 170 });
        _cameraButton = new OptionButton { HorizontalExpand = true };
        foreach (var mode in CameraModes.Cycle)
            _cameraButton.AddItem(Loc.GetString(CameraModes.LocId(mode)), (int) mode);

        _cameraButton.OnItemSelected += args =>
        {
            if (_refreshing)
                return;

            _ui.GetUIController<Render3DController>().SetCameraMode((CameraMode) args.Id);
            RefreshCamera();
        };
        row.AddChild(_cameraButton);
        page.AddChild(row);

        _dynamicBox = AddCheck(CCVars.Render3DDynamicCamera, "render3d-settings-dynamic");

        foreach (var mode in CameraModes.Cycle)
        {
            var section = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Visible = false };
            page.AddChild(section);
            _cameraSections[mode] = section;
            _body = section;

            section.AddChild(new Label
            {
                Text = Loc.GetString(CameraModes.LocId(mode) + "-hint"),
                FontColorOverride = HintColor,
            });

            switch (mode)
            {
                case CameraMode.ThirdPerson:
                    AddSlider(CCVars.Render3DThirdPersonDistance, "render3d-settings-distance", 0.8f, 3f, "0.0");
                    AddSlider(CCVars.Render3DFov, "render3d-settings-fov", 50, 110, "0");
                    AddSlider(CCVars.Render3DWallHeight, "render3d-settings-wall-height", 1f, 4f, "0.00");
                    AddLookSettings();
                    break;
                case CameraMode.FirstPerson:
                    AddSlider(CCVars.Render3DFov, "render3d-settings-fov", 50, 110, "0");
                    AddSlider(CCVars.Render3DWallHeight, "render3d-settings-wall-height", 1f, 4f, "0.00");
                    AddLookSettings();
                    break;
                case CameraMode.Shoulder:
                    AddSlider(CCVars.Render3DShoulderDistance, "render3d-settings-distance", 0.5f, 2f, "0.0");
                    AddSlider(CCVars.Render3DShoulderFov, "render3d-settings-fov", 40, 100, "0");
                    AddSlider(CCVars.Render3DWallHeight, "render3d-settings-wall-height", 1f, 4f, "0.00");
                    AddLookSettings();
                    break;
                case CameraMode.Overhead:
                    AddSlider(CCVars.Render3DOverheadPitch, "render3d-settings-pitch", 35f, 85f, "0");
                    AddSlider(CCVars.Render3DOverheadDistance, "render3d-settings-distance", 4f, 16f, "0.0");
                    AddSlider(CCVars.Render3DOverheadFov, "render3d-settings-fov", 30, 70, "0");
                    AddSlider(CCVars.Render3DOverheadWallHeight, "render3d-settings-wall-height", 0.6f, 2.3f, "0.00");
                    break;
                case CameraMode.Isometric:
                    AddSlider(CCVars.Render3DIsoPitch, "render3d-settings-pitch", 20f, 60f, "0");
                    AddSlider(CCVars.Render3DOverheadDistance, "render3d-settings-distance", 4f, 16f, "0.0");
                    AddSlider(CCVars.Render3DIsoFov, "render3d-settings-fov", 15, 60, "0");
                    AddSlider(CCVars.Render3DOverheadWallHeight, "render3d-settings-wall-height", 0.6f, 2.3f, "0.00");
                    break;
                case CameraMode.Orbit:
                    AddSlider(CCVars.Render3DOverheadDistance, "render3d-settings-distance", 4f, 16f, "0.0");
                    AddSlider(CCVars.Render3DOverheadFov, "render3d-settings-fov", 30, 70, "0");
                    AddSlider(CCVars.Render3DOverheadWallHeight, "render3d-settings-wall-height", 0.6f, 2.3f, "0.00");
                    AddLookSettings();
                    break;
            }
        }

        _body = page;
        RefreshCamera();
    }

    /// <summary>The mouse look settings (the cameras that turn with the mouse; the orbit camera uses them for its drag).</summary>
    private void AddLookSettings()
    {
        AddSlider(CCVars.Render3DMouseSensitivity, "render3d-settings-sensitivity", 0.1f, 3f, "0.00");
        AddCheck(CCVars.Render3DInvertY, "render3d-settings-invert-y");
    }

    private void BuildInterface()
    {
        NewPage("render3d-tab-interface");
        AddHeader("render3d-settings-pointing");
        AddCheck(CCVars.Render3DCrosshairNames, "render3d-settings-crosshair-names");
        AddCheck(CCVars.Render3DPointList, "render3d-settings-point-list");
        AddHeader("render3d-settings-minimap-header");
        AddMinimapShowCheck();
        AddCheck(CCVars.Render3DMinimapRotate, "render3d-settings-minimap-rotate");
        AddSlider(CCVars.Render3DMinimapSize, "render3d-settings-minimap-size", 120, 360, "0");
        AddSlider(CCVars.Render3DMinimapRange, "render3d-settings-minimap-range", 10, 40, "0");
    }

    private void BuildControls()
    {
        NewPage("render3d-tab-controls");
        foreach (var (function, _, locId) in Render3DKeys.All)
            AddKeyRow(function, locId);
    }

    private void BuildGraphics()
    {
        NewPage("render3d-tab-graphics");
        AddSliderRow("render3d-settings-supersample", 1f, 2f, "0.00", _cfg.GetCVar(CCVars.Render3DSupersample), v =>
        {
            _cfg.SetCVar(CCVars.Render3DSupersample, MathF.Round(v * 20f) / 20f);
            _cfg.SetCVar(CCVars.Render3DQuality, Render3DQuality.Detect(_cfg));
        });
        foreach (var effect in Render3DQuality.Effects.Where(e => !e.Shape))
            AddEffectCheck(effect);

        AddHeader("render3d-settings-shapes");
        AddSliderRow("render3d-settings-thickness-layers", 0f, 6f, "0", _cfg.GetCVar(CCVars.Render3DThicknessLayers), v =>
        {
            _cfg.SetCVar(CCVars.Render3DThicknessLayers, (int) MathF.Round(v));
            _cfg.SetCVar(CCVars.Render3DQuality, Render3DQuality.Detect(_cfg));
        });
        foreach (var effect in Render3DQuality.Effects.Where(e => e.Shape))
            AddEffectCheck(effect);
    }

    // ---------------------------------------------------------------- controls

    private void AddHeader(string locId)
    {
        _body.AddChild(new Label { Text = Loc.GetString(locId), Margin = new Thickness(0, 6, 0, 2), StyleClasses = { "LabelHeading" } });
    }

    private CheckBox AddCheck(CVarDef<bool> cvar, string locId)
    {
        var box = new CheckBox { Text = Loc.GetString(locId), Pressed = _cfg.GetCVar(cvar) };
        box.OnToggled += args => _cfg.SetCVar(cvar, args.Pressed);
        _body.AddChild(box);
        return box;
    }

    /// <summary>The minimap box: ticked when the minimap is small or large, and ticking it brings back the small one.</summary>
    private void AddMinimapShowCheck()
    {
        var box = new CheckBox { Text = Loc.GetString("render3d-settings-minimap"), Pressed = _cfg.GetCVar(CCVars.Render3DMinimapMode) > 0 };
        box.OnToggled += args => _cfg.SetCVar(CCVars.Render3DMinimapMode, args.Pressed ? 1 : 0);
        _body.AddChild(box);
    }

    private void AddSlider(CVarDef<int> cvar, string locId, float min, float max, string format)
        => AddSliderRow(locId, min, max, format, _cfg.GetCVar(cvar), v => _cfg.SetCVar(cvar, (int) MathF.Round(v)));

    private void AddSlider(CVarDef<float> cvar, string locId, float min, float max, string format)
        => AddSliderRow(locId, min, max, format, _cfg.GetCVar(cvar), v => _cfg.SetCVar(cvar, MathF.Round(v * 100f) / 100f));

    private void AddSliderRow(string locId, float min, float max, string format, float value, Action<float> set)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new Label { Text = Loc.GetString(locId), MinWidth = 170 });
        var slider = new Slider { MinValue = min, MaxValue = max, Value = Math.Clamp(value, min, max), HorizontalExpand = true, MinWidth = 140 };
        var label = new Label { Text = slider.Value.ToString(format), MinWidth = 44 };
        slider.OnValueChanged += s =>
        {
            label.Text = s.Value.ToString(format);
            set(s.Value);
        };
        row.AddChild(slider);
        row.AddChild(label);
        _body.AddChild(row);
    }

    private void AddQualityRow()
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new Label { Text = Loc.GetString("render3d-settings-quality"), MinWidth = 170 });
        var button = new OptionButton { HorizontalExpand = true };
        button.AddItem(Loc.GetString("render3d-settings-quality-low"), Render3DQuality.Low);
        button.AddItem(Loc.GetString("render3d-settings-quality-medium"), Render3DQuality.Medium);
        button.AddItem(Loc.GetString("render3d-settings-quality-high"), Render3DQuality.High);
        button.AddItem(Loc.GetString("render3d-settings-quality-custom"), Render3DQuality.Custom);
        button.OnItemSelected += args =>
        {
            if (_refreshing)
                return;

            // "Custom" is only a label for hand-picked settings; choosing a preset applies it
            if (args.Id is >= Render3DQuality.Low and <= Render3DQuality.High)
                Render3DQuality.Apply(_cfg, args.Id);

            RefreshGraphics();
        };
        row.AddChild(button);
        _body.AddChild(row);
        _qualityButton = button;
        RefreshGraphics();
    }

    private void AddEffectCheck(Render3DQuality.Effect effect)
    {
        var box = new CheckBox { Text = Loc.GetString(effect.LocId), Pressed = _cfg.GetCVar(effect.Cvar) };
        box.OnToggled += args =>
        {
            if (_refreshing)
                return;

            Render3DQuality.SetEffect(_cfg, effect, args.Pressed);
            RefreshGraphics();
        };
        _body.AddChild(box);
        _fxBoxes.Add((effect, box));
    }

    /// <summary>Brings the preset dropdown and the effect checkboxes in line with the settings (they also change from the console and from auto quality).</summary>
    private void RefreshGraphics()
    {
        _refreshing = true;
        _qualityButton?.SelectId(Math.Clamp(_cfg.GetCVar(CCVars.Render3DQuality), Render3DQuality.Low, Render3DQuality.Custom));
        foreach (var (effect, box) in _fxBoxes)
            box.Pressed = _cfg.GetCVar(effect.Cvar);
        _refreshing = false;
    }

    /// <summary>
    ///     Brings the camera dropdown in line with the settings (the camera keys change it too) and shows the settings of that camera only; the
    ///     dynamic camera box is only there for the overhead and isometric views.
    /// </summary>
    private void RefreshCamera()
    {
        var mode = CameraModes.FromSettings(
            _cfg.GetCVar(CCVars.Render3DFirstPerson), _cfg.GetCVar(CCVars.Render3DOverhead), _cfg.GetCVar(CCVars.Render3DCameraExtra));

        _refreshing = true;
        _cameraButton?.SelectId((int) mode);
        _refreshing = false;

        foreach (var (m, section) in _cameraSections)
            section.Visible = m == mode;

        if (_dynamicBox != null)
            _dynamicBox.Visible = mode is CameraMode.Overhead or CameraMode.Isometric;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_capturing != null)
            return;

        RefreshGraphics();
        RefreshCamera();
    }

    private static readonly Color HintColor = new(1f, 0.82f, 0.25f);

    private void AddKeyRow(BoundKeyFunction function, string locId)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var name = new Label { Text = Loc.GetString(locId), MinWidth = 220 };
        if (function == Render3DKeys.FreeCursor)
            name.FontColorOverride = HintColor;
        row.AddChild(name);
        var button = new Button { MinWidth = 120, HorizontalExpand = true };
        button.OnPressed += _ => BeginCapture(function);
        row.AddChild(button);
        _body.AddChild(row);
        _keyButtons.Add((function, button));
        RefreshKeyText(function);
    }

    private void RefreshKeyText(BoundKeyFunction function)
    {
        foreach (var (f, button) in _keyButtons)
        {
            if (f != function)
                continue;

            button.Text = _capturing == function
                ? Loc.GetString("render3d-settings-press-key")
                : _input.TryGetKeyBinding(function, out var binding)
                    ? binding.GetKeyString()
                    : Loc.GetString("render3d-settings-unbound");
        }
    }

    private void BeginCapture(BoundKeyFunction function)
    {
        if (_capturing != null)
            return;

        _capturing = function;
        RefreshKeyText(function);
        _input.FirstChanceOnKeyEvent += OnKeyEvent;
    }

    private void EndCapture()
    {
        _input.FirstChanceOnKeyEvent -= OnKeyEvent;
        var function = _capturing;
        _capturing = null;
        if (function is { } f)
            RefreshKeyText(f);
    }

    private void OnKeyEvent(KeyEventArgs args, KeyEventType type)
    {
        if (_capturing is not { } function)
            return;

        args.Handle();
        if (type != KeyEventType.Up)
            return;

        var key = args.Key;
        if (key == Keyboard.Key.Escape)
        {
            EndCapture();
            return;
        }

        var mods = new Keyboard.Key[3];
        var i = 0;
        if (args.Control && key != Keyboard.Key.Control)
            mods[i++] = Keyboard.Key.Control;
        if (args.Shift && key != Keyboard.Key.Shift)
            mods[i++] = Keyboard.Key.Shift;
        if (args.Alt && key != Keyboard.Key.Alt)
            mods[i++] = Keyboard.Key.Alt;

        foreach (var existing in _input.GetKeyBindings(function).ToArray())
            _input.RemoveBinding(existing);

        _input.RegisterBinding(new KeyBindingRegistration
        {
            Function = function,
            BaseKey = key,
            Mod1 = mods[0],
            Mod2 = mods[1],
            Mod3 = mods[2],
            Type = KeyBindingType.State,
            CanFocus = key is Keyboard.Key.MouseLeft or Keyboard.Key.MouseRight or Keyboard.Key.MouseMiddle,
            CanRepeat = false,
        });
        _input.SaveToUserData();
        EndCapture();
    }

    public override void Close()
    {
        if (_capturing != null)
            EndCapture();

        base.Close();
    }
}
