using System.Numerics;

namespace Content.Client.Render3D;

/// <summary>
///     Mouse-look without a captured mouse (engines before 286 cannot capture it): the cursor is hidden and works like a
///     joystick. Inside a dead zone around the centre of the view nothing turns; further out the view turns faster, up to
///     full speed at <see cref="FullAt"/> of the way to the nearer edge of the view.
///     <para>
///         The cursor cannot be put back in the middle, so steering only starts (<see cref="Arm"/>) once the cursor has been
///         inside the dead zone. Without that, closing a window with the cursor near an edge would spin the camera.
///     </para>
/// </summary>
public static class MouseSteer
{
    /// <summary>The fraction of the distance from the centre to the nearer edge of the view where the full speed is reached.</summary>
    public const float FullAt = 0.85f;

    /// <summary>In a window (not fullscreen) the cursor this close to the window's edge has probably left the window: no turning.</summary>
    public const float WindowEdgePixels = 3f;

    /// <summary>The radius in pixels that an offset of 1 means: the joystick is round, with the size of the narrower side.</summary>
    public static float Radius(Vector2 viewSize) => MathF.Max(1f, MathF.Min(viewSize.X, viewSize.Y) * 0.5f * FullAt);

    /// <summary>
    ///     The cursor's offset from the centre of the view in joystick units: 0 at the centre, length 1 at <see cref="FullAt"/>
    ///     of the way to the nearer edge. X grows to the right, Y downwards (screen pixels). Not clamped.
    /// </summary>
    public static Vector2 Offset(Vector2 cursor, Vector2 viewSize) => (cursor - viewSize * 0.5f) / Radius(viewSize);

    /// <summary>
    ///     The turn rate for one axis of <paramref name="offset"/>, from -1 to 1: 0 inside the dead zone, then rising with the
    ///     square of the distance past it (fine control near the middle), 1 from an offset of 1 on.
    /// </summary>
    public static float Rate(float offset, float deadZone)
    {
        deadZone = Math.Clamp(deadZone, 0f, 0.9f);
        var a = MathF.Abs(offset);
        if (a <= deadZone)
            return 0f;

        var t = MathF.Min((a - deadZone) / (1f - deadZone), 1f);
        return MathF.Sign(offset) * t * t;
    }

    /// <summary>Whether the cursor is inside the dead zone (round, like the joystick).</summary>
    public static bool InDeadZone(Vector2 offset, float deadZone) => offset.Length() <= Math.Clamp(deadZone, 0f, 0.9f);

    /// <summary>
    ///     Whether steering may start or go on: it stays off (<paramref name="armed"/> false) after anything that let go of the
    ///     mouse until the cursor is back inside the dead zone.
    /// </summary>
    public static bool Arm(bool armed, Vector2 offset, float deadZone) => armed || InDeadZone(offset, deadZone);

    /// <summary>Whether a window cursor position is at the edge of the window, where it stops when the cursor leaves the window.</summary>
    public static bool AtWindowEdge(Vector2 cursorInWindow, Vector2 windowSize) =>
        cursorInWindow.X <= WindowEdgePixels || cursorInWindow.Y <= WindowEdgePixels
        || cursorInWindow.X >= windowSize.X - 1f - WindowEdgePixels || cursorInWindow.Y >= windowSize.Y - 1f - WindowEdgePixels;

    /// <summary>
    ///     The camera turn for one frame, in radians (yaw, pitch), as <see cref="Camera3D.AddLook"/> takes them: the cursor to
    ///     the right turns right (negative yaw), the cursor below the centre looks down (negative pitch) unless inverted.
    /// </summary>
    public static (float Yaw, float Pitch) Turn(Vector2 offset, float deadZone, float degreesPerSecond, float dt, bool pitch, bool invertY)
    {
        var speed = degreesPerSecond * MathF.PI / 180f * dt;
        var yaw = -Rate(offset.X, deadZone) * speed;
        var look = pitch ? Rate(offset.Y, deadZone) * speed * (invertY ? 1f : -1f) : 0f;
        return (yaw, look);
    }
}
