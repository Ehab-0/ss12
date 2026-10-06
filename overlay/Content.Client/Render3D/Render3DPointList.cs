using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>
///     A small list, at the right edge of the 3D view, of what the crosshair points at: the thing under it first, then the others
///     that lie on the same spot (the pile on a table). Names only, in the same wording as the label under the crosshair; what is
///     out of reach is greyed. Opened and closed with a key and a checkbox in the settings. It sits inside the 3D view control
///     like the minimap, because the viewport widget is a box container that a second child would squeeze.
/// </summary>
public sealed class Render3DPointList : PanelContainer
{
    private const int MaxRows = 10;

    private readonly Label _title;
    private readonly Label[] _rows = new Label[MaxRows];
    private readonly Label _more;

    private static readonly Color FirstColor = Color.White;
    private static readonly Color OtherColor = new(0.86f, 0.88f, 0.94f);
    private static readonly Color FarColor = new(0.58f, 0.60f, 0.66f);

    public Render3DPointList()
    {
        HorizontalAlignment = HAlignment.Right;
        VerticalAlignment = VAlignment.Center;
        Margin = new Thickness(0, 0, 76, 0);
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = new Color(0.04f, 0.05f, 0.10f, 0.72f),
            BorderColor = new Color(0.55f, 0.60f, 0.80f, 0.8f),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 6,
        };

        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MouseFilter = MouseFilterMode.Ignore };
        _title = new Label { Text = Loc.GetString("render3d-pointlist-title"), FontColorOverride = new Color(1f, 0.88f, 0.4f), MouseFilter = MouseFilterMode.Ignore };
        box.AddChild(_title);
        for (var i = 0; i < MaxRows; i++)
        {
            _rows[i] = new Label { Visible = false, MouseFilter = MouseFilterMode.Ignore };
            box.AddChild(_rows[i]);
        }

        _more = new Label { Visible = false, FontColorOverride = FarColor, MouseFilter = MouseFilterMode.Ignore };
        box.AddChild(_more);
        AddChild(box);
        MinWidth = 170;
    }

    /// <summary>Shows these names (the first is what the crosshair is on). <paramref name="hidden"/> is how many more did not fit.</summary>
    public void SetEntries(IReadOnlyList<(string Text, bool InReach)> entries, int hidden)
    {
        for (var i = 0; i < MaxRows; i++)
        {
            var row = _rows[i];
            if (i < entries.Count)
            {
                var (text, reach) = entries[i];
                row.Text = text;
                row.FontColorOverride = !reach ? FarColor : i == 0 ? FirstColor : OtherColor;
                row.Visible = true;
            }
            else
            {
                row.Visible = false;
            }
        }

        if (entries.Count == 0)
        {
            _rows[0].Text = Loc.GetString("render3d-pointlist-nothing");
            _rows[0].FontColorOverride = FarColor;
            _rows[0].Visible = true;
        }

        _more.Visible = hidden > 0;
        if (hidden > 0)
            _more.Text = Loc.GetString("render3d-pointlist-more", ("count", hidden));
    }
}
