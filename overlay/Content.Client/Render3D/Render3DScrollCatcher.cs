using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Client.Render3D;

/// <summary>
///     A transparent control laid over the classic 2D viewport while the dynamic view is at its 2D end. The 2D viewport does not take the
///     mouse wheel, so without this the wheel could not bring the camera back down. It hands everything else on: the keys go to the game's
///     viewport input as if the 2D viewport had received them, and the map conversions are the 2D viewport's own, so aiming, outlines and menus
///     behave exactly as in the classic view.
/// </summary>
public sealed class Render3DScrollCatcher : Control, IViewportControl
{
    [Dependency] private IInputManager _input = default!;

    /// <summary>The 2D viewport this one stands in for.</summary>
    public IViewportControl? Target;

    /// <summary>Called with the wheel's movement (positive = rolled away from you).</summary>
    public Action<float>? Wheel;

    public Render3DScrollCatcher()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        Visible = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Handled)
            return;

        _input.ViewportKeyEvent(this, args);
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Handled)
            return;

        _input.ViewportKeyEvent(this, args);
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);
        if (args.Handled)
            return;

        Wheel?.Invoke(args.Delta.Y);
        args.Handle();
    }

    public MapCoordinates ScreenToMap(Vector2 coords) => Target?.ScreenToMap(coords) ?? default;

    public MapCoordinates PixelToMap(Vector2 point) => Target?.PixelToMap(point) ?? default;

    public Vector2 WorldToScreen(Vector2 map) => Target?.WorldToScreen(map) ?? default;

    public Matrix3x2 GetWorldToScreenMatrix() => Target?.GetWorldToScreenMatrix() ?? Matrix3x2.Identity;

    public Matrix3x2 GetLocalToScreenMatrix() => Target?.GetLocalToScreenMatrix() ?? Matrix3x2.Identity;
}
