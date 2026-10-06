using Content.Shared.Render3D;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>What a rectangle of a glass picture is.</summary>
public enum GlassPart
{
    /// <summary>The tinted, mostly transparent pane.</summary>
    Pane,

    /// <summary>The frame around the pane.</summary>
    Frame,

    /// <summary>A line of a fine mesh (grilles).</summary>
    Mesh,

    /// <summary>A step of the faint diagonal highlight.</summary>
    Shine,
}


/// <summary>
///     How one kind of glass is drawn into the billboard atlas (see <see cref="Render3DGlassRule"/>): the pane and the lines
///     over it are built here as plain rectangles, so the layout can be tested without a screen.
/// </summary>
public sealed class GlassLook
{
    public readonly Color Tint;
    public readonly float Alpha;
    public readonly Color Frame;
    public readonly int FrameWidth;
    public readonly int Mesh;
    public readonly Color MeshColor;
    public readonly bool Shine;
    public readonly bool Bar;

    /// <summary>
    ///     This glass as it looks when the door is open: the frame only, so the doorway stays in place and does not vanish.
    ///     The look itself returns itself.
    /// </summary>
    public GlassLook Opened => _opened ??= new GlassLook(this);

    private GlassLook? _opened;

    private GlassLook(GlassLook closed)
    {
        Tint = closed.Tint;
        Alpha = 0f;
        Frame = closed.Frame;
        FrameWidth = Math.Max(2, closed.FrameWidth);
        Mesh = 0;
        MeshColor = closed.MeshColor;
        Shine = false;
        Bar = false;
        _opened = this;
    }

    public GlassLook(Render3DGlassRule rule)
    {
        Bar = rule.Bar;
        Tint = rule.Tint;
        Alpha = Math.Clamp(rule.Alpha, 0f, 1f);
        Frame = rule.Frame;
        FrameWidth = Math.Clamp(rule.FrameWidth, 0, 6);
        Mesh = rule.Mesh <= 0 ? 0 : Math.Clamp(rule.Mesh, 2, 16);
        MeshColor = rule.MeshColor;
        Shine = rule.Shine;
    }

    /// <summary>
    ///     The rectangles of the picture for a slot of <paramref name="width"/> by <paramref name="height"/> pixels, in the
    ///     order they are drawn (later ones are on top), with x and y from the top left corner of the slot.
    /// </summary>
    public void Layout(int width, int height, List<(UIBox2 Rect, GlassPart Part)> parts)
    {
        parts.Clear();
        parts.Add((new UIBox2(0, 0, width, height), GlassPart.Pane));

        if (Mesh > 0)
        {
            for (var x = Mesh; x < width - FrameWidth; x += Mesh)
                parts.Add((new UIBox2(x, 0, x + 1, height), GlassPart.Mesh));

            for (var y = Mesh; y < height - FrameWidth; y += Mesh)
                parts.Add((new UIBox2(0, y, width, y + 1), GlassPart.Mesh));
        }

        if (Shine && Mesh == 0 && width >= 16 && height >= 16)
        {
            // two diagonal streaks climbing to the right, one pixel steps (the picture is pixel art)
            var inner = FrameWidth + 1;
            for (var k = 0; k < 2; k++)
            {
                var offset = k * (width / 5);
                var run = Math.Min(width, height) * 3 / 5;
                for (var i = 0; i < run; i++)
                {
                    var x = inner + offset + i / 2;
                    var y = height - inner - 2 - i;
                    var streak = k == 0 ? 3 : 1;
                    if (x + streak > width - inner || y < inner)
                        break;

                    parts.Add((new UIBox2(x, y, x + streak, y + 1), GlassPart.Shine));
                }
            }
        }

        if (Bar && width >= 16 && height >= 16)
        {
            // a push bar at about the middle of the door, a little in from the frame
            var y = height * 11 / 24;
            var inset = FrameWidth + 3;
            parts.Add((new UIBox2(inset, y, width - inset, y + 2), GlassPart.Frame));
        }

        if (FrameWidth > 0)
        {
            var f = FrameWidth;
            parts.Add((new UIBox2(0, 0, width, f), GlassPart.Frame));
            parts.Add((new UIBox2(0, height - f, width, height), GlassPart.Frame));
            parts.Add((new UIBox2(0, f, f, height - f), GlassPart.Frame));
            parts.Add((new UIBox2(width - f, f, width, height - f), GlassPart.Frame));
        }
    }
}
