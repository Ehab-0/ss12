using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class EntityShapeTest
{
    private const float Eps = 1e-4f;

    [Test]
    public void DownPitchIsZeroLevelAndQuarterTurnStraightDown()
    {
        Assert.That(EntityShape.DownPitch(new Vector3(0, 1, 0)), Is.EqualTo(0f).Within(Eps));
        Assert.That(EntityShape.DownPitch(new Vector3(0, 0, -1)), Is.EqualTo(MathF.PI / 2).Within(Eps));
        Assert.That(EntityShape.DownPitch(new Vector3(0, 0.7071f, -0.7071f)), Is.EqualTo(MathF.PI / 4).Within(1e-3f));
        // looking up counts as level, never negative
        Assert.That(EntityShape.DownPitch(new Vector3(0, 0.7071f, 0.7071f)), Is.EqualTo(0f).Within(Eps));
    }

    [Test]
    public void FlatLeanShrinksAsTheCameraLooksMoreDownAndIsCapped()
    {
        Assert.That(EntityShape.FlatLean(MathF.PI / 2), Is.EqualTo(0f).Within(Eps), "no tilt straight down");

        var previous = float.MaxValue;
        for (var deg = 0; deg <= 90; deg += 5)
        {
            var lean = EntityShape.FlatLean(deg * MathF.PI / 180f);
            Assert.That(lean, Is.LessThanOrEqualTo(EntityShape.MaxFlatLean + Eps));
            Assert.That(lean, Is.GreaterThanOrEqualTo(0f));
            Assert.That(lean, Is.LessThanOrEqualTo(previous + Eps), "never grows when the camera looks more down");
            previous = lean;
        }

        Assert.That(EntityShape.FlatLean(0f), Is.EqualTo(EntityShape.MaxFlatLean).Within(Eps));
    }

    [Test]
    public void StandLeanGrowsWithPitchAndIsCapped()
    {
        Assert.That(EntityShape.StandLean(0f), Is.EqualTo(0f).Within(Eps));
        Assert.That(EntityShape.StandLean(MathF.PI / 2), Is.EqualTo(EntityShape.MaxStandLean).Within(Eps));
        Assert.That(EntityShape.StandLean(0.3f), Is.GreaterThan(EntityShape.StandLean(0.1f)));
    }

    [Test]
    public void LeanedFlatCardKeepsItsSizeItsNearEdgeAndItsCentre(
        [Values(0f, 0.4f, 0.9f, 1.05f)] float lean,
        [Values(0f, 90f, 200f, 310f)] float fwdDegrees)
    {
        var a = fwdDegrees * MathF.PI / 180f;
        var fwd = new Vector2(MathF.Cos(a), MathF.Sin(a));
        // a 1.0 x 0.6 card, axis aligned, centred on the origin
        Vector2[] corners = { new(-0.5f, 0.3f), new(0.5f, 0.3f), new(0.5f, -0.3f), new(-0.5f, -0.3f) };
        var sMin = float.MaxValue;
        var sMax = float.MinValue;
        foreach (var c in corners)
        {
            var s = Vector2.Dot(c, fwd);
            sMin = MathF.Min(sMin, s);
            sMax = MathF.Max(sMax, s);
        }

        var pts = new Vector3[4];
        for (var i = 0; i < 4; i++)
            pts[i] = EntityShape.LeanFlatPoint(corners[i], 0.1f, fwd, sMin, sMax, lean);

        // rigid: all four edge lengths and the diagonal are unchanged
        Assert.That(Vector3.Distance(pts[0], pts[1]), Is.EqualTo(1.0f).Within(1e-3f));
        Assert.That(Vector3.Distance(pts[1], pts[2]), Is.EqualTo(0.6f).Within(1e-3f));
        Assert.That(Vector3.Distance(pts[2], pts[3]), Is.EqualTo(1.0f).Within(1e-3f));
        Assert.That(Vector3.Distance(pts[0], pts[2]), Is.EqualTo(MathF.Sqrt(1.0f + 0.36f)).Within(1e-3f));

        // nothing goes below the base height, and the lowest point is on it
        var minZ = float.MaxValue;
        foreach (var p in pts)
            minZ = MathF.Min(minZ, p.Z);
        Assert.That(minZ, Is.EqualTo(0.1f).Within(1e-3f));

        // the horizontal centre of the card stays at the origin
        var cx = (pts[0].X + pts[1].X + pts[2].X + pts[3].X) / 4f;
        var cy = (pts[0].Y + pts[1].Y + pts[2].Y + pts[3].Y) / 4f;
        var shift = new Vector2(cx, cy);
        // (the projection onto the camera direction is what is recentred; the sideways part never moves)
        Assert.That(Vector2.Dot(shift, fwd), Is.EqualTo(0f).Within(1e-3f));
        Assert.That(MathF.Abs(shift.X * fwd.Y - shift.Y * fwd.X), Is.EqualTo(0f).Within(1e-3f));

        // the normal is perpendicular to the card and tilts towards the camera (against forward)
        var normal = EntityShape.FlatNormal(fwd, lean);
        Assert.That(normal.Length(), Is.EqualTo(1f).Within(1e-3f));
        Assert.That(Vector3.Dot(normal, pts[1] - pts[0]), Is.EqualTo(0f).Within(1e-3f));
        Assert.That(Vector3.Dot(normal, pts[3] - pts[0]), Is.EqualTo(0f).Within(1e-3f));
        Assert.That(normal.X * fwd.X + normal.Y * fwd.Y, Is.LessThanOrEqualTo(Eps));
    }

    [Test]
    public void LeanedStandingCardFallsBackFromTheCameraAndKeepsItsHeightAsLength(
        [Values(0f, 0.15f, 0.3f)] float lean)
    {
        var fwd = new Vector2(0, 1);
        var foot = new Vector2(2, 3);
        var top = EntityShape.LeanStandPoint(foot, 0.2f, 1f, fwd, lean);

        Assert.That(Vector3.Distance(top, new Vector3(foot, 0.2f)), Is.EqualTo(1f).Within(1e-3f), "rotation, not a shear");
        Assert.That(top.Y, Is.GreaterThanOrEqualTo(foot.Y - Eps), "the top moves away from the camera");
        Assert.That(top.X, Is.EqualTo(foot.X).Within(Eps));

        var normal = EntityShape.StandNormal(fwd, lean);
        Assert.That(normal.Length(), Is.EqualTo(1f).Within(1e-3f));
        Assert.That(Vector3.Dot(normal, top - new Vector3(foot, 0.2f)), Is.EqualTo(0f).Within(1e-3f));
        Assert.That(normal.Y, Is.LessThanOrEqualTo(Eps), "faces the camera");
        Assert.That(normal.Z, Is.GreaterThanOrEqualTo(-Eps), "tilts up, not down");
    }

    [Test]
    public void LayerCountFallsOffWithDistanceAndIsZeroWhenOff()
    {
        Assert.That(EntityShape.LayerCount(0, 1f), Is.EqualTo(0));
        Assert.That(EntityShape.LayerCount(4, 1f), Is.EqualTo(4));
        Assert.That(EntityShape.LayerCount(4, EntityShape.NearRange), Is.EqualTo(4));
        Assert.That(EntityShape.LayerCount(4, EntityShape.NearRange + 1f), Is.EqualTo(2));
        Assert.That(EntityShape.LayerCount(1, EntityShape.NearRange + 1f), Is.EqualTo(1));
        Assert.That(EntityShape.LayerCount(4, EntityShape.FarRange + 0.1f), Is.EqualTo(0));
    }

    [Test]
    public void LayerShadeGoesFromFullBrightnessToTheSideShade()
    {
        Assert.That(EntityShape.LayerShade(0, 4), Is.EqualTo(1f).Within(Eps));
        Assert.That(EntityShape.LayerShade(4, 4), Is.EqualTo(EntityShape.SideShade).Within(Eps));
        for (var k = 1; k <= 4; k++)
            Assert.That(EntityShape.LayerShade(k, 4), Is.LessThan(EntityShape.LayerShade(k - 1, 4)));
    }

    [Test]
    public void DefaultThicknessIsPositiveForEveryCategory()
    {
        foreach (var category in new[] { EntityCategory.Item, EntityCategory.Character, EntityCategory.Object })
            Assert.That(EntityShape.DefaultThickness(category), Is.GreaterThan(0.02f).And.LessThan(0.4f));
    }
}
