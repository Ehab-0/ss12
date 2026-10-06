using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>
///     A small list, at the right edge of the 3D view, of what the crosshair points at: the thing under it first, then the others
///     that lie on the same spot (the pile on a table). Rows can be chosen: with the mouse (hold the free-mouse key and click a row) or
///     with the up and down keys and enter; the chosen entity is what the crosshair then acts on. Names only, in the same wording as
///     the label under the crosshair; what is out of reach is greyed. It sits inside the 3D view control like the minimap, because the
///     viewport widget is a box container that a second child would squeeze.
/// </summary>
public sealed class Render3DPointList : PanelContainer
{
    private const int MaxRows = 10;

    private readonly Label _title;
    private readonly Row[] _rows = new Row[MaxRows];
    private readonly Label _more;
    private readonly Label _hint;
    private readonly Label _choose;
    private readonly Label _target;

    /// <summary>A row was clicked (the entity it stands for).</summary>
    public event Action<EntityUid>? RowClicked;

    private static readonly Color FirstColor = Color.White;
    private static readonly Color OtherColor = new(0.86f, 0.88f, 0.94f);
    private static readonly Color FarColor = new(0.58f, 0.60f, 0.66f);
    private static readonly Color TargetColor = new(1f, 0.82f, 0.25f);

    private static readonly StyleBoxFlat HighlightBox = new() { BackgroundColor = new Color(0.35f, 0.40f, 0.65f, 0.55f) };
    private static readonly StyleBoxFlat TargetBox = new() { BackgroundColor = new Color(0.92f, 0.59f, 0.08f, 0.30f) };
    private static readonly StyleBoxFlat PlainBox = new() { BackgroundColor = Color.Transparent };

    private sealed class Row : PanelContainer
    {
        public readonly Label Text = new() { MouseFilter = MouseFilterMode.Ignore };
        public EntityUid Uid;
        public event Action<EntityUid>? Clicked;

        public Row()
        {
            MouseFilter = MouseFilterMode.Stop;
            PanelOverride = PlainBox;
            AddChild(Text);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);
            if (args.Function != EngineKeyFunctions.UIClick || Uid == default)
                return;

            Clicked?.Invoke(Uid);
            args.Handle();
        }
    }

    public Render3DPointList()
    {
        HorizontalAlignment = HAlignment.Right;
        VerticalAlignment = VAlignment.Center;
        Margin = new Thickness(0, 0, 76, 0);
        MouseFilter = MouseFilterMode.Pass;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(0.04f, 0.05f, 0.10f, 0.72f),
            BorderColor = new Color(0.55f, 0.60f, 0.80f, 0.8f),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5,
        };

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MouseFilter = MouseFilterMode.Ignore };
        _title = new Label { Text = Loc.GetString("render3d-pointlist-title"), FontColorOverride = new Color(1f, 0.88f, 0.4f), MouseFilter = MouseFilterMode.Ignore, Margin = new Thickness(2, 0, 0, 2) };
        box.AddChild(_title);
        _target = new Label { Visible = false, FontColorOverride = TargetColor, MouseFilter = MouseFilterMode.Ignore, Margin = new Thickness(2, 0, 0, 2) };
        box.AddChild(_target);
        for (var i = 0; i < MaxRows; i++)
        {
            _rows[i] = new Row { Visible = false };
            _rows[i].Clicked += uid => RowClicked?.Invoke(uid);
            box.AddChild(_rows[i]);
        }

        _more = new Label { Visible = false, FontColorOverride = FarColor, MouseFilter = MouseFilterMode.Ignore, Margin = new Thickness(2, 0, 0, 0) };
        box.AddChild(_more);
        _choose = new Label { FontColorOverride = FarColor, MouseFilter = MouseFilterMode.Ignore, Margin = new Thickness(2, 4, 0, 0), Visible = false };
        box.AddChild(_choose);
        _hint = new Label { FontColorOverride = FarColor, MouseFilter = MouseFilterMode.Ignore, Margin = new Thickness(2, 0, 0, 0), Text = Loc.GetString("render3d-pointlist-hint-toggle", ("key", "L")) };
        box.AddChild(_hint);
        AddChild(box);
        MinWidth = 190;
    }

    /// <summary>The key that switches the list, for the line at the bottom.</summary>
    public void SetToggleKey(string key) => _hint.Text = Loc.GetString("render3d-pointlist-hint-toggle", ("key", key));

    /// <summary>How to choose a row, shown when there is more than one to choose from.</summary>
    public void SetChooseHint(string freeMouseKey)
    {
        _choose.Text = Loc.GetString("render3d-pointlist-hint-choose", ("key", freeMouseKey));
    }

    /// <summary>Shows the entries; <paramref name="hidden"/> is how many more did not fit, <paramref name="targetName"/> the name of the chosen target, if any.</summary>
    public void SetState(PointListState state, int hidden, string? targetName)
    {
        var entries = state.Entries;
        for (var i = 0; i < MaxRows; i++)
        {
            var row = _rows[i];
            if (i < entries.Count)
            {
                var (uid, text, reach) = entries[i];
                var chosen = state.Target == uid;
                row.Uid = uid;
                row.Text.Text = (chosen ? "> " : "  ") + text;
                row.Text.FontColorOverride = chosen ? TargetColor : !reach ? FarColor : i == 0 ? FirstColor : OtherColor;
                row.PanelOverride = i == state.Highlight ? HighlightBox : chosen ? TargetBox : PlainBox;
                row.Visible = true;
            }
            else
            {
                row.Uid = default;
                row.Visible = false;
            }
        }

        if (entries.Count == 0)
        {
            _rows[0].Uid = default;
            _rows[0].Text.Text = "  " + Loc.GetString("render3d-pointlist-nothing");
            _rows[0].Text.FontColorOverride = FarColor;
            _rows[0].PanelOverride = PlainBox;
            _rows[0].Visible = true;
        }

        _choose.Visible = entries.Count >= 2;
        _more.Visible = hidden > 0;
        if (hidden > 0)
            _more.Text = Loc.GetString("render3d-pointlist-more", ("count", hidden));

        _target.Visible = targetName != null;
        if (targetName != null)
            _target.Text = Loc.GetString("render3d-pointlist-target", ("name", targetName));
    }
}
