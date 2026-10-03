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
public sealed partial class Render3DSettingsWindow : DefaultWindow
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IInputManager _input = default!;

    private readonly BoxContainer _body;
    private readonly List<(BoundKeyFunction Function, Button Button)> _keyButtons = new();
    private BoundKeyFunction? _capturing;
    private readonly List<(Render3DQuality.Effect Effect, CheckBox Box)> _fxBoxes = new();
    private OptionButton? _qualityButton;
    private bool _refreshing;

    public Render3DSettingsWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("render3d-settings-title");
        MinSize = new Vector2(460, 520);

        _body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 4 };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_body);
        ContentsContainer.AddChild(scroll);

        AddHeader("render3d-settings-view");
        AddCheck(CCVars.Render3DEnabled, "render3d-settings-use-3d");
        AddCheck(CCVars.Render3DFirstPerson, "render3d-settings-first-person");
        AddSlider(CCVars.Render3DFov, "render3d-settings-fov", 50, 110, "0");
        AddSlider(CCVars.Render3DMouseSensitivity, "render3d-settings-sensitivity", 0.1f, 3f, "0.00");
        AddSlider(CCVars.Render3DRenderScale, "render3d-settings-render-scale", 0.25f, 1f, "0.00");
        AddCheck(CCVars.Render3DInvertY, "render3d-settings-invert-y");
        AddCheck(CCVars.Render3DCrosshairNames, "render3d-settings-crosshair-names");

        AddHeader("render3d-settings-graphics");
        AddQualityRow();
        AddCheck(CCVars.Render3DAutoQuality, "render3d-settings-auto-quality");
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

        AddHeader("render3d-settings-keys");
        foreach (var (function, _, locId) in Render3DKeys.All)
            AddKeyRow(function, locId);
    }

    private void AddHeader(string locId)
    {
        _body.AddChild(new Label { Text = Loc.GetString(locId), Margin = new Thickness(0, 6, 0, 2), StyleClasses = { "LabelHeading" } });
    }

    private void AddCheck(CVarDef<bool> cvar, string locId)
    {
        var box = new CheckBox { Text = Loc.GetString(locId), Pressed = _cfg.GetCVar(cvar) };
        box.OnToggled += args => _cfg.SetCVar(cvar, args.Pressed);
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

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_capturing == null)
            RefreshGraphics();
    }

    private void AddKeyRow(BoundKeyFunction function, string locId)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        row.AddChild(new Label { Text = Loc.GetString(locId), MinWidth = 220 });
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
