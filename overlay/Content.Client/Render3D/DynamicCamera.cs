namespace Content.Client.Render3D;

/// <summary>
///     The numbers behind the dynamic camera (overhead and isometric views): where the camera wants to be for how open the space
///     around the character is and how fast it moves. Pure, so it can be unit tested.
/// </summary>
public static class DynamicCamera
{
    /// <summary>How far (tiles) the probe rays look for walls.</summary>
    public const float ProbeRange = 7f;

    /// <summary>How fast the camera settles on its target (1 / seconds): about a third of a second to cover most of the way.</summary>
    public const float SettleRate = 3.5f;

    /// <summary>The distance scale in the tightest and in the most open place.</summary>
    public const float TightScale = 0.70f;
    public const float OpenScale = 1.15f;

    /// <summary>The extra look-down angle (degrees) in the tightest place, and the angle taken off in the most open one.</summary>
    public const float TightPitchOffset = 7f;
    public const float OpenPitchOffset = -4f;

    /// <summary>The distance scale added at full running speed.</summary>
    public const float SpeedScale = 0.12f;

    /// <summary>
    ///     How open the space is, from 0 (a closet: a wall at the character) to 1 (nothing within <paramref name="maxRange"/>): the mean
    ///     distance the probe rays travelled, as a fraction of the range.
    /// </summary>
    public static float Openness(ReadOnlySpan<float> rayDistances, float maxRange)
    {
        if (rayDistances.IsEmpty || maxRange <= 0f)
            return 1f;

        var sum = 0f;
        foreach (var d in rayDistances)
            sum += Math.Clamp(d, 0f, maxRange);

        return sum / (rayDistances.Length * maxRange);
    }

    /// <summary>
    ///     The camera the situation asks for: a scale for the distance (smaller in tight places, larger in open ones and when running)
    ///     and an offset for the look-down angle in degrees (steeper in tight places, so a wall hides less, shallower in open ones).
    /// </summary>
    public static (float DistanceScale, float PitchOffsetDegrees) Target(float openness, float speed01)
    {
        var open = Math.Clamp(openness, 0f, 1f);
        var run = Math.Clamp(speed01, 0f, 1f);
        var scale = (TightScale + (OpenScale - TightScale) * open) * (1f + SpeedScale * run);
        var pitch = TightPitchOffset + (OpenPitchOffset - TightPitchOffset) * open;
        return (scale, pitch);
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
}
