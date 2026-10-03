using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class Render3DMathTest
{
    private static Camera3D MakeCamera(double yawDeg, float pitchDeg, Vector3 pos)
    {
        var cam = new Camera3D
        {
            Yaw = MathHelper.DegreesToRadians(yawDeg),
            Pitch = MathHelper.DegreesToRadians(pitchDeg),
            Position = pos,
            FovY = MathHelper.DegreesToRadians(80f),
            Size = new Vector2(1920, 1080),
        };
        cam.UpdateBasis();
        return cam;
    }

    [Test]
    public void ProjectionAndRayThroughPixelRoundTripOnTheFloor(
        [Values(0, 37, 90, 180, -120)] double yaw,
        [Values(-40f, -15f, 0f)] float pitch)
    {
        var cam = MakeCamera(yaw, pitch, new Vector3(5, -3, 1.0f));
        var tested = 0;

        foreach (var (dx, dy) in new[] { (0f, 3f), (1f, 4f), (-1.5f, 5f), (2f, 2.5f) })
        {
            var forward = cam.ForwardGround;
            var right = new Vector2(cam.Right.X, cam.Right.Y);
            var floor = new Vector2(cam.Position.X, cam.Position.Y) + forward * dy + right * dx;
            if (!cam.Project(new Vector3(floor, 0), out var pixel, out _))
                continue;
            if (pixel.X < 0 || pixel.Y < 0 || pixel.X > cam.Size.X || pixel.Y > cam.Size.Y)
                continue;

            tested++;
            var dir = cam.RayDirection(pixel);
            Assert.That(Picking3D.RayPlaneZ(cam.Position, dir, 0, out _, out var back), Is.True);
            Assert.That(back.X, Is.EqualTo(floor.X).Within(0.01f));
            Assert.That(back.Y, Is.EqualTo(floor.Y).Within(0.01f));
        }

        // pitch 0 looks at the horizon so floor points can be off-screen; the steeper pitches must have hits
        if (pitch < -10f)
            Assert.That(tested, Is.GreaterThan(0));
    }

    [Test]
    public void CentrePixelLooksAlongForward()
    {
        var cam = MakeCamera(30, 10, Vector3.Zero);
        var dir = Vector3.Normalize(cam.RayDirection(new Vector2(960, 540)));
        Assert.That(Vector3.Dot(dir, cam.Forward), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void RayQuadHitsInsideAndMissesOutsideAndShrinks()
    {
        // vertical 1x1 quad at x = 5 spanning y 0..1, z 0..1
        var p0 = new Vector3(5, 0, 1);
        var p1 = new Vector3(5, 1, 1);
        var p3 = new Vector3(5, 0, 0);

        Assert.That(Picking3D.RayQuad(new Vector3(0, 0.5f, 0.5f), Vector3.UnitX, p0, p1, p3, 1f, out var t), Is.True);
        Assert.That(t, Is.EqualTo(5f).Within(1e-4f));
        Assert.That(Picking3D.RayQuad(new Vector3(0, 1.5f, 0.5f), Vector3.UnitX, p0, p1, p3, 1f, out _), Is.False);

        // near the edge: inside the full quad but outside the 90% shrunk one
        var edge = new Vector3(0, 0.97f, 0.5f);
        Assert.That(Picking3D.RayQuad(edge, Vector3.UnitX, p0, p1, p3, 1f, out _), Is.True);
        Assert.That(Picking3D.RayQuad(edge, Vector3.UnitX, p0, p1, p3, Picking3D.ShrinkFraction, out _), Is.False);

        // behind the origin and parallel: no hit
        Assert.That(Picking3D.RayQuad(new Vector3(10, 0.5f, 0.5f), Vector3.UnitX, p0, p1, p3, 1f, out _), Is.False);
        Assert.That(Picking3D.RayQuad(new Vector3(0, 0.5f, 0.5f), Vector3.UnitY, p0, p1, p3, 1f, out _), Is.False);
    }

    [Test]
    public void EntityWinsOverWorldOnlyWithinTolerance()
    {
        Assert.That(Picking3D.EntityBeatsWorld(3.0f, 3.1f), Is.True);
        Assert.That(Picking3D.EntityBeatsWorld(3.2f, 3.0f), Is.False);
    }

    // A 7x3 room (walls on its border) with a pillar at (3,1); everything outside is open.
    private static bool Room(Vector2i c)
    {
        if (c.X < 0 || c.X > 6 || c.Y < 0 || c.Y > 2)
            return false;
        return c.X == 0 || c.X == 6 || c.Y == 0 || c.Y == 2 || c is { X: 3, Y: 1 };
    }

    [Test]
    public void DdaHitsWallsAlongAxes()
    {
        Assert.That(TileWorldSystem.Dda(Room, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 0.5f, 0, 100, 1.25f,
            out var t, out var n, out var tile), Is.True);
        Assert.That(t, Is.EqualTo(1.5f).Within(1e-4f));
        Assert.That(n, Is.EqualTo(new Vector2(-1, 0)));
        Assert.That(tile, Is.EqualTo(new Vector2i(3, 1)));

        Assert.That(TileWorldSystem.Dda(Room, new Vector2(1.5f, 1.5f), new Vector2(-1, 0), 0.5f, 0, 100, 1.25f,
            out t, out n, out tile), Is.True);
        Assert.That(t, Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(tile, Is.EqualTo(new Vector2i(0, 1)));
        Assert.That(n, Is.EqualTo(new Vector2(1, 0)));
    }

    [Test]
    public void DdaIgnoresRaysPassingOverTheWallTops()
    {
        Assert.That(TileWorldSystem.Dda(Room, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 1.0f, 1.0f, 100, 1.25f,
            out _, out _, out _), Is.False);
    }

    [Test]
    public void DdaIntoACornerHitsOneOfTheTwoWalls()
    {
        Assert.That(TileWorldSystem.Dda(Room, new Vector2(1.2f, 1.2f), Vector2.Normalize(new Vector2(-1, -1)), 0.5f, 0, 100, 1.25f,
            out var t, out _, out var tile), Is.True);
        Assert.That(tile.X == 0 || tile.Y == 0, Is.True);
        Assert.That(t, Is.LessThan(0.5f));
    }

    [Test]
    public void DdaOnARotatedGridMatchesTheSameMapInLocalSpace()
    {
        // TileWorldSystem rotates the world ray into grid space with the inverse grid rotation
        var gridRot = Angle.FromDegrees(33);
        var originLocal = new Vector2(1.5f, 1.5f);
        var dirLocal = new Vector2(1, 0);
        var dirWorld = gridRot.RotateVec(dirLocal);
        var back = (-gridRot).RotateVec(dirWorld);

        Assert.That(TileWorldSystem.Dda(Room, originLocal, dirLocal, 0.5f, 0, 100, 1.25f, out var t1, out _, out var tile1), Is.True);
        Assert.That(TileWorldSystem.Dda(Room, originLocal, back, 0.5f, 0, 100, 1.25f, out var t2, out _, out var tile2), Is.True);
        Assert.That(t2, Is.EqualTo(t1).Within(1e-3f));
        Assert.That(tile2, Is.EqualTo(tile1));
    }

    [Test]
    public void SpringArmNeverPlacesTheCameraInsideAWall([Range(0, 359, 7)] int yawDeg, [Values(0.8f, 1.6f, 3f)] float dist)
    {
        // a one tile wide corridor along Y (walls at x=0 and x=2), the player in the middle of it
        static bool Corridor(Vector2i c) => c.X == 0 || c.X == 2;

        var head = new Vector3(1.5f, 10.5f, 0.9f);
        var yaw = MathHelper.DegreesToRadians(yawDeg);
        var forward = new Vector2(-MathF.Sin((float) yaw), MathF.Cos((float) yaw));
        var right = new Vector2(MathF.Cos((float) yaw), MathF.Sin((float) yaw));
        var desired = new Vector3(
            head.X - forward.X * dist + right.X * 0.35f,
            head.Y - forward.Y * dist + right.Y * 0.35f,
            1.15f);

        var cam = Camera3D.SpringArm(head, desired, 0.15f, (Vector3 o, Vector3 dir, float maxT, out float t) =>
            TileWorldSystem.Dda(Corridor, new Vector2(o.X, o.Y), new Vector2(dir.X, dir.Y), o.Z, dir.Z, maxT, 1.6f, out t, out _, out _));

        var cell = new Vector2i((int) MathF.Floor(cam.X), (int) MathF.Floor(cam.Y));
        Assert.That(Corridor(cell), Is.False, $"camera {cam} is inside a wall for yaw {yawDeg}, dist {dist}");
        Assert.That((cam - head).Length(), Is.LessThanOrEqualTo((desired - head).Length() + 1e-4f));
    }

    [Test]
    public void SweptSpringArmKeepsAGapToWallsBesideTheCamera([Range(0, 359, 11)] int yawDeg, [Values(1.2f, 2.5f)] float dist)
    {
        // an open room with a wall column at x=3: the camera must not end up pressed against it
        static bool Pillar(Vector2i c) => c.X == 3 && c.Y is >= 8 and <= 14;

        var head = new Vector3(1.5f, 11.5f, 0.9f);
        var yaw = MathHelper.DegreesToRadians(yawDeg);
        var forward = new Vector2(-MathF.Sin((float) yaw), MathF.Cos((float) yaw));
        var desired = new Vector3(head.X - forward.X * dist, head.Y - forward.Y * dist, 1.15f);

        Camera3D.RayCastDelegate ray = (Vector3 o, Vector3 dir, float maxT, out float t) =>
            TileWorldSystem.Dda(Pillar, new Vector2(o.X, o.Y), new Vector2(dir.X, dir.Y), o.Z, dir.Z, maxT, 1.6f, out t, out _, out _);

        var point = Camera3D.SpringArmFraction(head, desired, 0.2f, 0f, ray);
        var swept = Camera3D.SpringArmFraction(head, desired, 0.2f, 0.3f, ray);

        // the swept arm is never longer than the point arm, and 0..1
        Assert.That(swept, Is.LessThanOrEqualTo(point + 1e-4f));
        Assert.That(swept, Is.InRange(0f, 1f));

        // wherever it ends, there is a gap in front of the camera along the arm and to both of its sides
        var cam = head + (desired - head) * swept;
        var along = Vector2.Normalize(new Vector2(desired.X - head.X, desired.Y - head.Y));
        var side = new Vector2(-along.Y, along.X);
        var c = new Vector2(cam.X, cam.Y);
        foreach (var probe in new[] { c + along * 0.15f, c + side * 0.28f, c - side * 0.28f })
            Assert.That(Pillar(new Vector2i((int) MathF.Floor(probe.X), (int) MathF.Floor(probe.Y))), Is.False, $"{probe}");
    }
}
