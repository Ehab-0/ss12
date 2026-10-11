namespace Content.Client.Render3D;

/// <summary>Exponential smoothing for a camera value. Pure, so it can be unit tested.</summary>
public static class Glide
{
    /// <summary>The default rate (1 / seconds): about a third of a second to cover most of the way.</summary>
    public const float DefaultRate = 3.5f;

    /// <summary>
    ///     One step of exponential smoothing towards <paramref name="target"/>. A frame that took too long (a hitch, a pause) or no time
    ///     at all jumps straight to the target instead of lurching.
    /// </summary>
    public static float Smooth(float current, float target, float dt, float rate = DefaultRate)
    {
        if (float.IsNaN(current) || dt <= 0f || dt > 0.25f)
            return target;

        return current + (target - current) * (1f - MathF.Exp(-dt * rate));
    }

    /// <summary>
    ///     One step of a critically damped spring towards <paramref name="target"/> (it keeps its <paramref name="velocity"/>): it starts
    ///     and ends softly and does not overshoot, so a value that is pushed by separate steps (wheel notches) moves smoothly all the
    ///     way. <paramref name="smoothTime"/> is roughly how long (seconds) it takes to settle. A frame that took too long or no time at
    ///     all jumps to the target.
    /// </summary>
    public static float Damp(float current, ref float velocity, float target, float dt, float smoothTime)
    {
        if (float.IsNaN(current) || dt <= 0f || dt > 0.25f)
        {
            velocity = 0f;
            return target;
        }

        var omega = 2f / MathF.Max(smoothTime, 1e-3f);
        var x = omega * dt;
        var exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        var change = current - target;
        var temp = (velocity + omega * change) * dt;
        velocity = (velocity - omega * temp) * exp;
        var result = target + (change + temp) * exp;

        // never run past the target
        if ((target - current > 0f) == (result > target))
        {
            velocity = 0f;
            return target;
        }

        return result;
    }
}
