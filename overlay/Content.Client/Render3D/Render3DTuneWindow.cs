using System.Globalization;
using System.Linq;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Client.Render3D;

/// <summary>
///     A developer's tuning window: a slider, box or switch for every client-side <c>render3d.*</c> setting, applied at once
///     (the 3D view reads them every frame), so a value can be found by looking at the game instead of by editing a file and
///     restarting. It lists whatever the running client has registered, so a new setting shows up here by itself. Opened with
///     <c>render3d_tune</c>. Nothing is saved to another computer: "Copy changes" puts the changed values on the clipboard as
///     lines of <c>name = value</c> (and in the log) to hand over or to put into <c>client_config.toml</c>.
/// </summary>
public sealed partial class Render3DTuneWindow : DefaultWindow
{
    private const string Prefix = "render3d.";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private ILogManager _logs = default!;

    private readonly BoxContainer _list;
    private readonly LineEdit _search;
    private readonly Label _status;
    private readonly List<Row> _rows = new();

    /// <summary>The values the settings had when the window opened, to reset to and to tell what changed.</summary>
    private readonly Dictionary<string, object> _initial = new();

    /// <summary>Slider ranges for the settings where a range based on the current value would be unhelpful.</summary>
    private static readonly Dictionary<string, (float Min, float Max)> Ranges = new()
    {
        ["render3d.wall_height"] = (0.8f, 6f),
        ["render3d.eye_height"] = (0.3f, 3f),
        ["render3d.table_height"] = (0.2f, 1.5f),
        ["render3d.effect_height"] = (0f, 3f),
        ["render3d.fov"] = (40f, 120f),
        ["render3d.third_person_distance"] = (0.8f, 3f),
        ["render3d.mouse_sensitivity"] = (0.05f, 4f),
        ["render3d.render_scale"] = (0.25f, 1f),
        ["render3d.supersample"] = (1f, 2f),
        ["render3d.ground_radius"] = (8f, 48f),
        ["render3d.billboard_cap"] = (16f, 1000f),
        ["render3d.thickness_layers"] = (0f, 8f),
        ["render3d.cap_hysteresis"] = (0.1f, 1f),
        ["render3d.yaw_send_rate"] = (4f, 30f),
    };

    private sealed class Row
    {
        public string Name = "";
        public Type Type = typeof(float);
        public Control Container = default!;
        public Action<object> Refresh = _ => { };
    }

    public Render3DTuneWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "3D view tuning";
        MinSize = new Vector2(560, 680);

        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(8) };
        _search = new LineEdit { PlaceHolder = "filter (for example wall, fx, light)", HorizontalExpand = true };
        _search.OnTextChanged += _ => ApplyFilter();
        top.AddChild(_search);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var copy = new Button { Text = "Copy changes" };
        copy.OnPressed += _ => CopyChanges();
        var reset = new Button { Text = "Reset all" };
        reset.OnPressed += _ => ResetAll();
        buttons.AddChild(copy);
        buttons.AddChild(reset);
        _status = new Label { HorizontalExpand = true, Margin = new Thickness(8, 0, 0, 0) };
        buttons.AddChild(_status);
        top.AddChild(buttons);

        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2, Margin = new Thickness(8, 0, 8, 8) };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_list);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, HorizontalExpand = true };
        root.AddChild(top);
        root.AddChild(scroll);
        ContentsContainer.AddChild(root);

        Build();
    }

    private void Build()
    {
        var names = _cfg.GetRegisteredCVars()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && !n.StartsWith(Prefix + "dev_", StringComparison.Ordinal))
            .Where(n => (_cfg.GetCVarFlags(n) & CVar.CLIENTONLY) != 0)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        string? group = null;
        foreach (var name in names)
        {
            var type = _cfg.GetCVarType(name);
            if (type != typeof(bool) && type != typeof(float) && type != typeof(int))
                continue;

            var rest = name.Substring(Prefix.Length);
            var dot = rest.IndexOf('.');
            var head = dot > 0 ? rest.Substring(0, dot) : "main";
            if (head != group)
            {
                group = head;
                _list.AddChild(new Label { Text = head, Margin = new Thickness(0, 8, 0, 2), StyleClasses = { "LabelHeading" } });
            }

            var row = type == typeof(bool) ? AddBool(name) : AddNumber(name, type);
            row.Name = name;
            row.Type = type;
            _rows.Add(row);
            _initial[name] = _cfg.GetCVar(name);
        }
    }

    private Row AddBool(string name)
    {
        var box = new CheckBox { Text = name.Substring(Prefix.Length), Pressed = _cfg.GetCVar<bool>(name) };
        box.OnToggled += args => Set(name, args.Pressed);
        _list.AddChild(box);
        return new Row { Container = box, Refresh = v => box.Pressed = (bool) v };
    }

    private Row AddNumber(string name, Type type)
    {
        var isInt = type == typeof(int);
        var current = AsFloat(_cfg.GetCVar(name));
        var (min, max) = Ranges.TryGetValue(name, out var known) ? known : GuessRange(current, isInt);
        var format = isInt ? "0" : "0.00";

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        row.AddChild(new Label { Text = name.Substring(Prefix.Length), MinWidth = 200, ClipText = true });
        var slider = new Slider { MinValue = min, MaxValue = Math.Max(max, current), Value = Math.Max(min, current), HorizontalExpand = true, MinWidth = 120 };
        var edit = new LineEdit { Text = current.ToString(format, CultureInfo.InvariantCulture), MinWidth = 64 };
        var updating = false;

        slider.OnValueChanged += s =>
        {
            if (updating)
                return;

            var value = isInt ? MathF.Round(s.Value) : MathF.Round(s.Value * 100f) / 100f;
            updating = true;
            edit.Text = value.ToString(format, CultureInfo.InvariantCulture);
            updating = false;
            Set(name, isInt ? (object) (int) value : value);
        };
        edit.OnTextEntered += args =>
        {
            if (!float.TryParse(args.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
                return;

            updating = true;
            slider.MaxValue = Math.Max(slider.MaxValue, value);
            slider.MinValue = Math.Min(slider.MinValue, value);
            slider.Value = value;
            updating = false;
            Set(name, isInt ? (object) (int) MathF.Round(value) : value);
        };

        row.AddChild(slider);
        row.AddChild(edit);
        _list.AddChild(row);

        return new Row
        {
            Container = row,
            Refresh = v =>
            {
                var f = AsFloat(v);
                updating = true;
                slider.MaxValue = Math.Max(slider.MaxValue, f);
                slider.MinValue = Math.Min(slider.MinValue, f);
                slider.Value = f;
                edit.Text = f.ToString(format, CultureInfo.InvariantCulture);
                updating = false;
            },
        };
    }

    private static float AsFloat(object value) => value switch { float f => f, int i => i, _ => 0f };

    private static (float Min, float Max) GuessRange(float current, bool isInt)
    {
        if (current == 0f)
            return (0f, isInt ? 10f : 1f);

        var span = MathF.Max(MathF.Abs(current) * 3f, isInt ? 10f : 1f);
        return (current < 0f ? -span : 0f, span);
    }

    private void Set(string name, object value)
    {
        _cfg.SetCVar(name, value);
        _status.Text = $"{name.Substring(Prefix.Length)} = {Format(value)}";
    }

    private static string Format(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private List<string> ChangedLines()
    {
        var lines = new List<string>();
        foreach (var row in _rows)
        {
            var now = _cfg.GetCVar(row.Name);
            if (!Equals(now, _initial[row.Name]))
                lines.Add($"{row.Name} = {Format(now)}");
        }

        return lines;
    }

    private void CopyChanges()
    {
        var lines = ChangedLines();
        var text = string.Join("\n", lines);
        _clipboard.SetText(text);
        var log = _logs.GetSawmill("render3d.tune");
        log.Info(lines.Count == 0 ? "no changes" : "changed:\n" + text);
        _status.Text = lines.Count == 0 ? "nothing changed yet" : $"copied {lines.Count} changed value(s)";
    }

    private void ResetAll()
    {
        foreach (var row in _rows)
        {
            _cfg.SetCVar(row.Name, _initial[row.Name]);
            row.Refresh(_initial[row.Name]);
        }

        _status.Text = "reset to the values from when this window opened";
    }

    private void ApplyFilter()
    {
        var filter = _search.Text.Trim();
        foreach (var row in _rows)
            row.Container.Visible = filter.Length == 0 || row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
