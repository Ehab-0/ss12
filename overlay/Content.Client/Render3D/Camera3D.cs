using System.Numerics;

namespace Content.Client.Render3D;

public enum CameraMode : byte
{
    ThirdPerson,
    FirstPerson,
}

/// <summary>
///     Plain camera model for the 3D view. World axes: X east, Y north, Z up (map space, 1 tile = 1 unit).
///     <see cref="Yaw"/> follows <c>Content.Shared.Render3D.CameraYawMath</c> (zero looks north, positive turns
///     left/counter-clockwise); pitch is positive when looking up.
/// </summary>
public sealed class Camera3D
{
    public const float MaxPitch = 80f * MathF.PI / 180f;

    public double Yaw;
    public float Pitch;
    public CameraMode Mode = CameraMode.ThirdPerson;

    /// <summary>Position the camera looks out of, after the spring arm.</summary>
    public Vector3 Position;

    /// <summary>Head position the third-person arm hangs off (the character's eyes).</summary>
    public Vector3 Head;

    /// <summary>Vertical field of view in radians.</summary>
    public float FovY = 80f * MathF.PI / 180f;

    /// <summary>Viewport width / height.</summary>
    public float Aspect = 16f / 9f;

    public Vector2 Size = new(1920, 1080);

    public Vector3 Forward { get; private set; } = Vector3.UnitY;
    public Vector3 Right { get; private set; } = Vector3.UnitX;
    public Vector3 Up { get; private set; } = Vector3.UnitZ;
    public float TanHalfFov { get; private set; } = 1f;

    /// <summary>Horizontal forward direction (unit) on the ground plane.</summary>
    public Vector2 ForwardGround
    {
        get
        {
            var f = new Vector2(Forward.X, Forward.Y);
            if (f.LengthSquared() > 1e-8f)
                return Vector2.Normalize(f);
            return new Vector2(-MathF.Sin((float) Yaw), MathF.Cos((float) Yaw));
        }
    }

    public void AddLook(float yawDelta, float pitchDelta)
    {
        Yaw = NormalizeYaw(Yaw + yawDelta);
        Pitch = Math.Clamp(Pitch + pitchDelta, -MaxPitch, MaxPitch);
    }

    private static double NormalizeYaw(double yaw)
    {
        yaw %= Math.Tau;
        if (yaw > Math.PI)
            yaw -= Math.Tau;
        else if (yaw < -Math.PI)
            yaw += Math.Tau;
        return yaw;
    }

    /// <summary>
    ///     Recomputes the basis vectors from yaw/pitch and the projection constants from fov and size.
    ///     Call after changing <see cref="Yaw"/>, <see cref="Pitch"/>, <see cref="FovY"/> or <see cref="Size"/>.
    /// </summary>
    public void UpdateBasis()
    {
        var sy = (float) Math.Sin(Yaw);
        var cy = (float) Math.Cos(Yaw);
        var sp = MathF.Sin(Pitch);
        var cp = MathF.Cos(Pitch);

        // yaw 0 looks north (+Y); +yaw turns counter-clockwise (towards -X): forward_h = (-sin y, cos y)
        Forward = new Vector3(-sy * cp, cy * cp, sp);
        Right = new Vector3(cy, sy, 0f);
        Up = Vector3.Cross(Right, Forward);
        TanHalfFov = MathF.Tan(FovY * 0.5f);
        Aspect = Size.Y > 0 ? Size.X / Size.Y : 1f;
    }

    /// <summary>
    ///     Un-normalised ray direction through a pixel of the view, scaled so its component along
    ///     <see cref="Forward"/> is exactly 1 (so the ray parameter t is the planar view depth).
    /// </summary>
    public Vector3 RayDirection(Vector2 pixel)
    {
        var ndcX = pixel.X / Size.X * 2f - 1f;
        var ndcY = 1f - pixel.Y / Size.Y * 2f;
        return Forward + Right * (ndcX * TanHalfFov * Aspect) + Up * (ndcY * TanHalfFov);
    }

    /// <summary>Projects a world point to pixel coordinates in the view. Returns false when behind the camera.</summary>
    public bool Project(Vector3 world, out Vector2 pixel, out float depth)
    {
        var d = world - Position;
        depth = Vector3.Dot(d, Forward);
        if (depth < 0.01f)
        {
            pixel = default;
            return false;
        }

        var x = Vector3.Dot(d, Right) / (depth * TanHalfFov * Aspect);
        var y = Vector3.Dot(d, Up) / (depth * TanHalfFov);
        pixel = new Vector2((x * 0.5f + 0.5f) * Size.X, (0.5f - y * 0.5f) * Size.Y);
        return true;
    }

    /// <summary>
    ///     Projects a point that may be behind the camera, clamping the depth so the result is a (far off-screen)
    ///     but finite position. Used by UI that must always return *something*.
    /// </summary>
    public Vector2 ProjectLoose(Vector3 world)
    {
        if (Project(world, out var p, out _))
            return p;

        // Behind the camera: push far off-screen in the direction away from the screen centre.
        var d = world - Position;
        var x = Vector3.Dot(d, Right);
        var y = Vector3.Dot(d, Up);
        var dir = new Vector2(x, -y);
        if (dir.LengthSquared() < 1e-6f)
            dir = new Vector2(0, 1);
        dir = Vector2.Normalize(dir);
        return Size * 0.5f + dir * (Size.Length() * 4f);
    }

    /// <summary>
    ///     Spring arm: pulls <paramref name="desired"/> back along the arm from <paramref name="head"/> so it stays
    ///     <paramref name="margin"/> tiles in front of the first wall the arm hits. <paramref name="rayCast"/> gets
    ///     (origin, un-normalised arm vector, max t = 1) and returns the hit t in arm units, if any.
    /// </summary>
    public static Vector3 SpringArm(Vector3 head, Vector3 desired, float margin, RayCastDelegate rayCast)
        => head + (desired - head) * SpringArmFraction(head, desired, margin, 0f, rayCast);

    /// <summary>
    ///     How far along the arm from <paramref name="head"/> to <paramref name="desired"/> (0..1) the camera can go.
    ///     With a <paramref name="radius"/> the arm is swept as a small bundle of rays (to the camera position and to
    ///     points around it), so the camera body and its near plane stay clear of walls, not just its centre point.
    ///     All rays start at the head, which is always a valid place to stand.
    /// </summary>
    public static float SpringArmFraction(Vector3 head, Vector3 desired, float margin, float radius, RayCastDelegate rayCast)
    {
        var arm = desired - head;
        var len = arm.Length();
        if (len < 1e-4f)
            return 1f;

        var best = 1f;
        if (rayCast(head, arm, 1f, out var t))
            best = t;

        if (radius > 0f)
        {
            var flat = new Vector3(arm.X, arm.Y, 0f);
            var side = flat.LengthSquared() > 1e-6f ? Vector3.Normalize(new Vector3(-flat.Y, flat.X, 0f)) : Vector3.UnitX;
            for (var i = 0; i < 4; i++)
            {
                var offset = i switch
                {
                    0 => side * radius,
                    1 => -side * radius,
                    2 => Vector3.UnitZ * (radius * 0.6f),
                    _ => -Vector3.UnitZ * (radius * 0.6f),
                };

                if (rayCast(head, desired + offset - head, 1f, out var ts))
                    best = MathF.Min(best, ts);
            }
        }

        return best >= 1f ? 1f : MathF.Max(0f, best - margin / len);
    }

    public delegate bool RayCastDelegate(Vector3 origin, Vector3 dir, float maxT, out float t);
}
