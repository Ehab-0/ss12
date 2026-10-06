using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>
///     Puts a window that opens because the player used something (a vending machine, a console, a medical scanner) next to the
///     cursor. With the mouse captured the cursor is wherever it was left, and the game opens such windows at a fixed place of the
///     screen, so the menu appeared far from where the player was looking and clicking. The cursor comes back as soon as a window opens,
///     so the window is moved to it.
/// </summary>
public sealed partial class Render3DController
{
    private readonly HashSet<BaseWindow> _knownWindows = new();
    private readonly List<(BaseWindow Window, TimeSpan Since)> _unplaced = new();
    private TimeSpan _lastInteraction = TimeSpan.FromHours(-1); // long ago (MinValue would overflow when subtracted from)

    /// <summary>A window that opens within this long after the player used something is taken to be what they asked for.</summary>
    private static readonly TimeSpan InteractionWindow = TimeSpan.FromSeconds(1.5);

    /// <summary>Called when the player uses, activates or pulls something in the world.</summary>
    private void NoteInteraction() => _lastInteraction = _timing.RealTime;

    private void UpdateWindowPlacement()
    {
        var root = UIManager.WindowRoot;
        _knownWindows.RemoveWhere(w => w.Parent == null || !w.Visible);

        var recent = _timing.RealTime - _lastInteraction < InteractionWindow;
        foreach (var child in root.Children)
        {
            if (child is not BaseWindow window || !window.Visible || !_knownWindows.Add(window))
                continue;

            if (Active && recent && window != _settingsWindow && window != _tuneWindow)
                _unplaced.Add((window, _timing.RealTime));
        }

        for (var i = _unplaced.Count - 1; i >= 0; i--)
        {
            var (window, since) = _unplaced[i];
            if (window.Parent == null || !window.Visible || _timing.RealTime - since > TimeSpan.FromSeconds(1))
            {
                _unplaced.RemoveAt(i);
                continue;
            }

            // the size is known once the window has been laid out
            if (window.Size.X < 8f || window.Size.Y < 8f)
                continue;

            var mouse = UIManager.MousePositionScaled.Position;
            var bounds = root.Size;
            var x = Math.Clamp(mouse.X - window.Size.X / 2f, 0f, MathF.Max(0f, bounds.X - window.Size.X));
            var y = Math.Clamp(mouse.Y - window.Size.Y / 2f, 0f, MathF.Max(0f, bounds.Y - window.Size.Y));
            _captureLog.Verbose($"window placed: mouse {mouse} window {window.Size} was at {window.Position} now {x:F0},{y:F0} bounds {bounds} relative={_relative}");
            LayoutContainer.SetPosition(window, new System.Numerics.Vector2(x, y));
            _unplaced.RemoveAt(i);
        }
    }
}
