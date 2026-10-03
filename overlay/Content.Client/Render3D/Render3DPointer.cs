using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

// Everything the few edited upstream call sites need lives in this file, so each edit is a one-line call (written
// with fully qualified names, so no `using` edits) that the installer (Tools/ss12) can apply to any codebase.

/// <summary>
///     Where "the mouse" points in the world. While the 3D view has the mouse captured the OS cursor is not what the
///     player aims with (its reported position is a stale or drifting value that can sit over any UI control), so
///     systems that turn <c>IInputManager.MouseScreenPosition</c> into a map position (guns, melee, drag and drop,
///     target outlines) ask here and get the crosshair instead. Otherwise this is the engine's own conversion.
/// </summary>
public static class Render3DPointer
{
    /// <summary>The 3D viewport while it has the mouse captured, otherwise null.</summary>
    public static Render3DViewportControl? Captured;

    /// <summary>True while the 3D view (rather than the 2D one) is what is on screen.</summary>
    public static bool Shown;

    public static MapCoordinates PixelToMap(IEyeManager eyeManager, ScreenCoordinates mouse)
    {
        return Captured is { } captured
            ? captured.PixelToMap(mouse.Position)
            : eyeManager.PixelToMap(mouse);
    }

    /// <summary>
    ///     Drag-and-drop start test. With a captured mouse the OS cursor never moves, so the accumulated mouse-look
    ///     travel is used as well.
    /// </summary>
    public static bool DragMoved(bool movedOnScreen, float deadzone)
    {
        return movedOnScreen || Captured is { } captured && captured.RelativeTravel > deadzone;
    }

    /// <summary>Call when a mouse press starts a potential drag.</summary>
    public static void ResetDrag() => Captured?.ResetRelativeTravel();
}

/// <summary>Lets the 3D pick (what is under the crosshair) take part in <c>GameplayStateBase.GetClickableEntities</c>.</summary>
public static class Render3DPicking
{
    private static bool _merging; // the game thread is the only caller (client sandbox forbids [ThreadStatic])

    /// <summary>
    ///     When the 3D view is showing and its last pick was at <paramref name="coordinates"/>, starts a merge and
    ///     returns the picked entities; the caller appends what the 2D lookup finds and calls <see cref="End"/>.
    /// </summary>
    public static bool TryBegin(IEyeManager eyeManager, MapCoordinates coordinates, [NotNullWhen(true)] out List<EntityUid>? picked)
    {
        picked = null;
        if (_merging || eyeManager.MainViewport is not Render3DViewportControl view
            || !view.TryGetClickable(coordinates, out var entities))
        {
            return false;
        }

        picked = new List<EntityUid>(entities);
        _merging = true;
        return true;
    }

    public static void AddMissing(List<EntityUid> picked, IEnumerable<EntityUid> others)
    {
        foreach (var other in others)
        {
            if (!picked.Contains(other))
                picked.Add(other);
        }
    }

    public static void End() => _merging = false;
}

/// <summary>
///     Implemented (by an optional installer edit) by the world-space health bar overlay so the 3D view can redraw its
///     bars in screen space under the same rules.
/// </summary>
public interface IRender3DHealthBar
{
    /// <summary>False when the entity shows no bar; otherwise the filled fraction (0-1) and the bar colour.</summary>
    bool TryGetBar(EntityUid uid, out float ratio, out Color color);
}

/// <summary>Placement of screen-space overlay labels (popups, map text) in the 3D view.</summary>
public static class Render3DOverlays
{
    private static readonly List<UIBox2> LabelRects = new();
    private static long _labelFrame = -1;
    private static IGameTiming? _timing;

    /// <summary>World to screen pixels: through the viewport's projection in 3D, the affine matrix in 2D.</summary>
    public static Vector2 WorldToScreen(IViewportControl? viewport, Vector2 world, Matrix3x2 matrix)
    {
        return viewport is Render3DViewportControl view ? view.WorldToScreen(world) : Vector2.Transform(world, matrix);
    }

    /// <summary>False for map text that should not be drawn in the 3D view (too far, behind a wall).</summary>
    public static bool ShowMapText(IViewportControl? viewport, Vector2 world)
    {
        return viewport is not Render3DViewportControl view || view.IsLabelVisible(world, 14f);
    }

    /// <summary>
    ///     False when a label at <paramref name="drawPosition"/> would overlap one already drawn this frame (3D only:
    ///     perspective stacks distant labels on top of each other).
    /// </summary>
    public static bool AllowLabel(IViewportControl? viewport, Vector2 drawPosition, Vector2 dimensions)
    {
        if (viewport is not Render3DViewportControl)
            return true;

        _timing ??= IoCManager.Resolve<IGameTiming>();
        if (_labelFrame != _timing.CurFrame)
        {
            _labelFrame = _timing.CurFrame;
            LabelRects.Clear();
        }

        var rect = UIBox2.FromDimensions(drawPosition, dimensions);
        foreach (var other in LabelRects)
        {
            if (other.Intersects(rect))
                return false;
        }

        LabelRects.Add(rect);
        return true;
    }
}
