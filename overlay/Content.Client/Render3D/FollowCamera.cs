using System.Numerics;

namespace Content.Client.Render3D;

/// <summary>
///     The numbers behind the follow camera (overhead and isometric views): the camera looks where the character is looking. Its focus
///     moves ahead of the character, in the direction the character faces, by a lead distance, but never through a wall, and settles on
///     that smoothly. Pure, so it can be unit tested.
/// </summary>
public static class FollowCamera
{
    /// <summary>The default lead (tiles): how far ahead of the character the camera looks.</summary>
    public const float DefaultLead = 2.5f;

    public const float MinLead = 0f;
    public const float MaxLead = 6f;

    /// <summary>How far short of a wall in the facing direction the focus stops (tiles).</summary>
    public const float WallMargin = 0.75f;

    /// <summary>How fast the camera settles on its target (1 / seconds): about a third of a second to cover most of the way.</summary>
    public const float SettleRate = 3.5f;

    /// <summary>
    ///     The offset of the camera's focus from the character: <paramref name="lead"/> tiles along <paramref name="facing"/>, shortened so
    ///     it stays <see cref="WallMargin"/> short of a wall <paramref name="wallDistance"/> tiles away in that direction (use infinity when
    ///     there is none). A character with no facing, or no lead, gives no offset.
    /// </summary>
    public static Vector2 LookAhead(Vector2 facing, float lead, float wallDistance)
    {
        if (!float.IsFinite(facing.X) || !float.IsFinite(facing.Y) || facing.LengthSquared() < 1e-8f)
            return Vector2.Zero;

        var reach = Math.Clamp(lead, MinLead, MaxLead);
        if (reach <= 0f)
            return Vector2.Zero;

        if (!float.IsNaN(wallDistance))
            reach = Math.Clamp(wallDistance - WallMargin, 0f, reach);

        return Vector2.Normalize(facing) * reach;
    }

    /// <summary>
    ///     One step of exponential smoothing towards <paramref name="target"/>. A frame that took too long (a hitch, a pause) or no time
    ///     at all jumps straight to the target instead of lurching.
    /// </summary>
    public static float Smooth(float current, float target, float dt, float rate = SettleRate)
    {
        if (float.IsNaN(current) || dt <= 0f || dt > 0.25f)
            return target;

        return current + (target - current) * (1f - MathF.Exp(-dt * rate));
    }

    /// <summary>The same smoothing for a point.</summary>
    public static Vector2 Smooth(Vector2 current, Vector2 target, float dt, float rate = SettleRate)
    {
        if (float.IsNaN(current.X) || float.IsNaN(current.Y) || dt <= 0f || dt > 0.25f)
            return target;

        return current + (target - current) * (1f - MathF.Exp(-dt * rate));
    }
}
