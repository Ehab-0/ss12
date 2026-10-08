namespace Content.Client.Render3D;

/// <summary>The settings the dynamic view's stops are built from (the same sliders the other cameras have).</summary>
public readonly record struct DynamicViewSettings(
    float WalkFov,
    float ThirdDistance,
    float IsoPitch,
    float IsoFov,
    float OverheadPitch,
    float OverheadDistance,
    float OverheadFov,
    float TopDistance,
    float WalkWall,
    float ElevatedWall);

/// <summary>Where the dynamic view is, for one position of the wheel.</summary>
/// <param name="Distance">How far the camera hangs from the point it looks at (tiles).</param>
/// <param name="PitchDown">The look-down angle in degrees (0 = level).</param>
/// <param name="FovDegrees">Vertical field of view in degrees.</param>
/// <param name="TargetHeight">Height of the point the camera looks at: the head at first, the middle of the body when high up.</param>
/// <param name="Side">How far the camera is shifted to its right, over the shoulder (tiles).</param>
/// <param name="IsoWeight">0 when the heading is the grid's axes, 1 when it is the isometric diagonal.</param>
/// <param name="WallHeight">Height of the walls (and of the ceiling, while there is one).</param>
public readonly record struct DynamicViewState(float Distance, float PitchDown, float FovDegrees, float TargetHeight, float Side, float IsoWeight, float WallHeight);

/// <summary>
///     The dynamic view: one number <c>u</c> from 0 to 1 runs all the way from first person (0) through third person, a high third
///     person, isometric and overhead to a straight-down view (about 0.93) and then the classic 2D view (1). Every property of the camera
///     is read off a table of stops, so the wheel moves it smoothly through all of them. Pure, so it can be unit tested.
/// </summary>
public static class DynamicView
{
    // the stops: first person, third person, a high third person, isometric, overhead, straight down, the classic view
    public const float ThirdStop = 0.12f;
    public const float HighStop = 0.40f;
    public const float IsoStop = 0.62f;
    public const float OverheadStop = 0.80f;
    public const float TopStop = 0.93f;

    private static readonly float[] Stops = { 0f, ThirdStop, HighStop, IsoStop, OverheadStop, TopStop, 1f };

    /// <summary>The look-down angle of the third person stop, degrees (the third person camera's usual 0.15 radians).</summary>
    public const float ThirdPitch = 8.6f;

    /// <summary>The look-down angle (degrees) and field of view of the straight-down stop.</summary>
    public const float TopPitch = 85f;

    public const float TopFov = 60f;

    /// <summary>The height of the point the camera looks at: the head, and the middle of the body (<see cref="OverheadCamera.TargetHeight"/>).</summary>
    public const float HeadTargetHeight = 0.9f;

    /// <summary>The sideways shift of the third person stop (tiles): the camera sits a little to the right, over the shoulder.</summary>
    public const float ThirdSide = 0.35f;

    // Where things switch. A little hysteresis (an "on" above the "off") keeps a switch from flickering when the wheel rests on it.

    /// <summary>From here up the mouse is a free cursor, the view turns to the grid's heading and the camera counts as a high one.</summary>
    public const float FreeOn = 0.41f;

    public const float FreeOff = 0.39f;

    /// <summary>From here up there is no ceiling (the camera would be inside it).</summary>
    public const float NoCeilingOn = 0.30f;

    public const float NoCeilingOff = 0.28f;

    /// <summary>The walls come down from the walking height to the high one between these.</summary>
    public const float WallStart = 0.30f;

    public const float WallEnd = IsoStop;

    /// <summary>The classic 2D view fades in over this range, and takes over completely at <see cref="TwoDOn"/>.</summary>
    public const float FadeStart = 0.93f;

    public const float FadeEnd = 0.985f;
    public const float TwoDOn = 0.995f;
    public const float TwoDOff = 0.985f;

    /// <summary>How fast the camera glides to where the wheel put it (1 / seconds).</summary>
    public const float GlideRate = 9f;

    /// <summary>The furthest the wheel goes: the classic view when it is allowed, otherwise the straight-down view.</summary>
    public static float MaxU(bool allow2D) => allow2D ? 1f : TopStop;

    /// <summary>The camera for a position of the wheel.</summary>
    public static DynamicViewState Evaluate(float u, in DynamicViewSettings s)
    {
        u = Math.Clamp(u, 0f, 1f);
        var third = s.ThirdDistance;
        var pitch = Pick(u, 0f, ThirdPitch, 28f, s.IsoPitch, s.OverheadPitch, TopPitch, TopPitch);
        var fov = Pick(u, s.WalkFov, s.WalkFov, (s.WalkFov + s.IsoFov) * 0.5f, s.IsoFov, s.OverheadFov, TopFov, TopFov);

        float distance;
        if (u <= ThirdStop)
        {
            // from the character's eyes out to the third person distance
            distance = third * (u / ThirdStop);
        }
        else
        {
            // From there on the distance is read off how much of the floor the picture shows, which grows steadily (and evenly per notch: it
            // is interpolated by ratio). Picking the distance and the field of view independently let the narrowing field of view of the
            // isometric stop outweigh the distance growing, and the picture zoomed in a little on the way to it.
            var heightThird = Visible(third, s.WalkFov);
            var heightIso = Visible(s.OverheadDistance, s.IsoFov);
            var heightOverhead = Visible(s.OverheadDistance, s.OverheadFov);
            var heightTop = Visible(s.TopDistance, TopFov);
            var heightHigh = heightThird + (MathF.Max(heightIso, heightThird) - heightThird) * 0.5f;
            var visible = PickRatio(u, heightThird, heightHigh, heightIso, heightOverhead, heightTop, heightTop);
            distance = visible / (2f * MathF.Tan(fov * MathF.PI / 360f));
        }

        var height = Pick(u, HeadTargetHeight, HeadTargetHeight, 0.7f, OverheadCamera.TargetHeight, OverheadCamera.TargetHeight,
            OverheadCamera.TargetHeight, OverheadCamera.TargetHeight);
        var side = Pick(u, 0f, ThirdSide, 0f, 0f, 0f, 0f, 0f);
        var iso = Pick(u, 0f, 0f, 1f, 1f, 0f, 0f, 0f);
        var wall = s.WalkWall + (s.ElevatedWall - s.WalkWall) * SmoothStep(WallStart, WallEnd, u);
        return new DynamicViewState(distance, pitch, fov, height, side, iso, wall);
    }

    /// <summary>How much of the classic 2D view is blended over the 3D picture: 0 until <see cref="FadeStart"/>, 1 from <see cref="FadeEnd"/>.</summary>
    public static float Fade(float u) => SmoothStep(FadeStart, FadeEnd, u);

    /// <summary>
    ///     The wheel turned by <paramref name="notches"/> (positive = the wheel rolled away from you, which brings the camera closer):
    ///     <paramref name="speed"/> of the whole way per notch, the other way round when <paramref name="invert"/>, within 0 to <paramref name="max"/>.
    /// </summary>
    public static float Step(float u, float notches, float speed, bool invert, float max)
    {
        if (!float.IsFinite(notches) || !float.IsFinite(speed))
            return Math.Clamp(u, 0f, max);

        var delta = (invert ? notches : -notches) * speed;
        return Math.Clamp(u + delta, 0f, max);
    }

    /// <summary>A switch with hysteresis: on from <paramref name="on"/> upwards, off again below <paramref name="off"/>.</summary>
    public static bool Latch(bool current, float u, float on, float off) => current ? u >= off : u >= on;

    /// <summary>The value at <paramref name="u"/>, given one value per stop (seven), linear between neighbouring stops.</summary>
    private static float Pick(float u, float v0, float v1, float v2, float v3, float v4, float v5, float v6)
    {
        for (var i = 1; i < Stops.Length; i++)
        {
            if (u > Stops[i])
                continue;

            var t = (u - Stops[i - 1]) / (Stops[i] - Stops[i - 1]);
            var (from, to) = i switch
            {
                1 => (v0, v1),
                2 => (v1, v2),
                3 => (v2, v3),
                4 => (v3, v4),
                5 => (v4, v5),
                _ => (v5, v6),
            };
            return from + (to - from) * t;
        }

        return v6;
    }

    /// <summary>How tall (tiles) the picture is, at the distance of the point looked at, for a distance and a vertical field of view in degrees.</summary>
    public static float Visible(float distance, float fovDegrees) => 2f * distance * MathF.Tan(fovDegrees * MathF.PI / 360f);

    /// <summary>
    ///     Like <see cref="Pick"/>, for the values of the stops from the third person one on (six values), but between two stops the value
    ///     changes by a constant ratio, not a constant difference. Values must be above zero.
    /// </summary>
    private static float PickRatio(float u, float v1, float v2, float v3, float v4, float v5, float v6)
    {
        for (var i = 2; i < Stops.Length; i++)
        {
            if (u > Stops[i])
                continue;

            var t = Math.Clamp((u - Stops[i - 1]) / (Stops[i] - Stops[i - 1]), 0f, 1f);
            var (from, to) = i switch
            {
                2 => (v1, v2),
                3 => (v2, v3),
                4 => (v3, v4),
                5 => (v4, v5),
                _ => (v5, v6),
            };
            return from * MathF.Pow(MathF.Max(to, 1e-4f) / MathF.Max(from, 1e-4f), t);
        }

        return v6;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
