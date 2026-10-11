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
    private static readonly float[] StopsFromThird = { ThirdStop, HighStop, IsoStop, OverheadStop, TopStop, 1f };

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

    /// <summary>About how long (seconds) the camera takes to settle where the wheel put it.</summary>
    public const float GlideTime = 0.2f;

    /// <summary>The furthest the wheel goes: the classic view when it is allowed, otherwise the straight-down view.</summary>
    public static float MaxU(bool allow2D) => allow2D ? 1f : TopStop;

    /// <summary>The camera for a position of the wheel.</summary>
    public static DynamicViewState Evaluate(float u, in DynamicViewSettings s)
    {
        u = Math.Clamp(u, 0f, 1f);
        var third = s.ThirdDistance;
        var pitch = Pick(u, 0f, ThirdPitch, 28f, s.IsoPitch, s.OverheadPitch, TopPitch, TopPitch);
        var fov = Pick(u, s.WalkFov, s.WalkFov, (s.WalkFov + s.IsoFov) * 0.5f, s.IsoFov, s.OverheadFov, TopFov, TopFov);

        // How much of the floor the picture shows (its height in tiles at the point looked at) at the stops from the third person one on.
        var heights = StopHeights(s);
        float distance;
        if (u <= ThirdStop)
        {
            // from the character's eyes out to the third person distance (the one that shows the third person stop's floor)
            distance = heights[0] / (2f * MathF.Tan(s.WalkFov * MathF.PI / 360f)) * (u / ThirdStop);
        }
        else
        {
            // From there on the distance is read off how much of the floor the picture shows, which grows steadily (and evenly per notch: it
            // is interpolated by ratio). Picking the distance and the field of view independently let the narrowing field of view of the
            // isometric stop outweigh the distance growing, and the picture zoomed in a little on the way to it.
            var visible = PickRatio(u, heights[0], heights[1], heights[2], heights[3], heights[4], heights[4]);
            distance = visible / (2f * MathF.Tan(fov * MathF.PI / 360f));
        }

        var height = Pick(u, HeadTargetHeight, HeadTargetHeight, 0.7f, OverheadCamera.TargetHeight, OverheadCamera.TargetHeight,
            OverheadCamera.TargetHeight, OverheadCamera.TargetHeight);
        var side = Pick(u, 0f, ThirdSide, 0f, 0f, 0f, 0f, 0f);
        var iso = Pick(u, 0f, 0f, 1f, 1f, 0f, 0f, 0f);
        var wall = s.WalkWall + (s.ElevatedWall - s.WalkWall) * SmoothStep(WallStart, WallEnd, u);
        return new DynamicViewState(distance, pitch, fov, height, side, iso, wall);
    }

    /// <summary>
    ///     How far the camera has risen clear of the walls (0 = down among them, 1 = well over their tops), for a camera at
    ///     <paramref name="cameraHeight"/> and walls <paramref name="wallHeight"/> tall. The spring arm's hold on the camera lets go with it.
    /// </summary>
    public static float Clearance(float cameraHeight, float wallHeight) => SmoothStep(wallHeight + 0.3f, wallHeight + 2.0f, cameraHeight);

    /// <summary>
    ///     How much of the heading the player has dragged the view to still counts: all of it up to the overhead stop, fading out to the
    ///     straight-down view, so that the hand-over to the (north-up) classic 2D view is not turned.
    /// </summary>
    public static float TurnWeight(float u) => 1f - SmoothStep(OverheadStop, TopStop, u);

    /// <summary>The least step up (a ratio) between two stops' picture heights, so the wheel always zooms out on the way up.</summary>
    private const float MinGrowth = 1.02f;

    /// <summary>The most of the isometric stop's floor the third person stop may show.</summary>
    private const float ThirdShare = 0.6f;

    /// <summary>
    ///     How tall (tiles) the picture is at the third person, high third person, isometric, overhead and straight-down stops. The straight-down
    ///     one is fixed (it has to match the classic 2D view it fades into); the others are what the settings give, with two safeguards that
    ///     keep the wheel meaningful. A wide walking field of view (110, say) shows far more floor than the narrow isometric stop does at a
    ///     similar distance, so the third person stop is held to <see cref="ThirdShare"/> of the isometric stop's floor (the dynamic view's
    ///     third person is then a little closer than the walking camera, for such a field of view); and no stop shows less floor than the
    ///     one before (lifted by at least <see cref="MinGrowth"/>, but not past the top). Without them the picture zoomed in half way up and
    ///     out again, or squeezed the whole wheel into a few percent of zoom.
    /// </summary>
    public static float[] StopHeights(in DynamicViewSettings s)
    {
        var top = Visible(s.TopDistance, TopFov);
        var iso = Visible(s.OverheadDistance, s.IsoFov);
        var overhead = Visible(s.OverheadDistance, s.OverheadFov);
        var third = MathF.Min(Visible(s.ThirdDistance, s.WalkFov), MathF.Min(iso * ThirdShare, top * 0.9f));

        // each stop is at least a little above the one before, and at most as high as the top allows the ones after it to be
        float Step(float previous, float wanted, int stopsLeft)
        {
            var ceiling = top * MathF.Pow(1f / MinGrowth, stopsLeft);
            return MathF.Min(MathF.Max(wanted, previous * MinGrowth), ceiling);
        }

        var high = Step(third, third + (MathF.Max(iso, third) - third) * 0.5f, 3);
        iso = Step(high, iso, 2);
        overhead = Step(iso, overhead, 1);
        return new[] { third, high, iso, overhead, top };
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

    /// <summary>
    ///     The value at <paramref name="u"/>, given one value per stop (seven): a smooth curve through the stops that never overshoots them
    ///     (a monotone cubic), so the speed does not jump as the wheel passes a stop, as it would with straight lines between them.
    /// </summary>
    private static float Pick(float u, float v0, float v1, float v2, float v3, float v4, float v5, float v6)
    {
        var values = new[] { v0, v1, v2, v3, v4, v5, v6 };
        return Monotone(u, Stops, values);
    }

    /// <summary>How tall (tiles) the picture is, at the distance of the point looked at, for a distance and a vertical field of view in degrees.</summary>
    public static float Visible(float distance, float fovDegrees) => 2f * distance * MathF.Tan(fovDegrees * MathF.PI / 360f);

    /// <summary>
    ///     Like <see cref="Pick"/>, for the values of the stops from the third person one on (six values), but the curve runs through the
    ///     logarithms of the values, so a stop-to-stop change is by a ratio, not a difference. Values must be above zero.
    /// </summary>
    private static float PickRatio(float u, float v1, float v2, float v3, float v4, float v5, float v6)
    {
        var logs = new[]
        {
            MathF.Log(MathF.Max(v1, 1e-4f)), MathF.Log(MathF.Max(v2, 1e-4f)), MathF.Log(MathF.Max(v3, 1e-4f)),
            MathF.Log(MathF.Max(v4, 1e-4f)), MathF.Log(MathF.Max(v5, 1e-4f)), MathF.Log(MathF.Max(v6, 1e-4f)),
        };

        return MathF.Exp(Monotone(u, StopsFromThird, logs));
    }

    /// <summary>
    ///     A monotone cubic through the points (<paramref name="xs"/>, <paramref name="ys"/>) (Fritsch-Carlson): it passes through every
    ///     point, goes up or down only where the points do, and is flat where they are, so it neither overshoots nor wobbles.
    /// </summary>
    public static float Monotone(float x, float[] xs, float[] ys)
    {
        var n = xs.Length;
        if (x <= xs[0])
            return ys[0];
        if (x >= xs[n - 1])
            return ys[n - 1];

        var k = 0;
        while (x > xs[k + 1])
            k++;

        var h = xs[k + 1] - xs[k];
        var t = (x - xs[k]) / h;
        var m0 = Slope(xs, ys, k);
        var m1 = Slope(xs, ys, k + 1);
        var t2 = t * t;
        var t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * ys[k] + (t3 - 2f * t2 + t) * h * m0
            + (-2f * t3 + 3f * t2) * ys[k + 1] + (t3 - t2) * h * m1;
    }

    private static float Delta(float[] xs, float[] ys, int i) => (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);

    private static float Slope(float[] xs, float[] ys, int k)
    {
        var n = xs.Length;
        if (k > 0 && k < n - 1)
        {
            var d0 = Delta(xs, ys, k - 1);
            var d1 = Delta(xs, ys, k);
            if (d0 * d1 <= 0f)
                return 0f;

            var h0 = xs[k] - xs[k - 1];
            var h1 = xs[k + 1] - xs[k];
            var w1 = 2f * h1 + h0;
            var w2 = h1 + 2f * h0;
            return (w1 + w2) / (w1 / d0 + w2 / d1);
        }

        // the ends: a one-sided estimate, kept from pointing the wrong way or overshooting
        var i0 = k == 0 ? 0 : n - 2;
        var i1 = k == 0 ? 1 : n - 3;
        var da = Delta(xs, ys, i0);
        var db = Delta(xs, ys, i1);
        var ha = xs[i0 + 1] - xs[i0];
        var hb = k == 0 ? xs[2] - xs[1] : xs[n - 2] - xs[n - 3];
        var m = ((2f * ha + hb) * da - ha * db) / (ha + hb);
        if (MathF.Sign(m) != MathF.Sign(da))
            return 0f;
        if (MathF.Sign(da) != MathF.Sign(db) && MathF.Abs(m) > 3f * MathF.Abs(da))
            return 3f * da;

        return m;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
