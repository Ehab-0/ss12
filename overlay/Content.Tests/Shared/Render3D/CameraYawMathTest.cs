using System.Numerics;
using Content.Shared.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.Render3D;

/// <summary>
///     Pins down the angle conventions the 3D camera relies on (assumption A6 in docs/ss12/PLAN.md) and checks that
///     "up" input walks along the camera's forward direction for any grid rotation.
/// </summary>
[TestFixture]
[TestOf(typeof(CameraYawMath))]
public sealed class CameraYawMathTest
{
    private static void AssertVec(Vector2 expected, Vector2 actual, float tol = 1e-4f)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(tol), $"X of {actual} vs {expected}");
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(tol), $"Y of {actual} vs {expected}");
    }

    [Test]
    public void AngleConventions()
    {
        // Angle.ToWorldVec: zero is SOUTH, +90 is EAST (counter-clockwise).
        AssertVec(new Vector2(0, -1), Angle.Zero.ToWorldVec());
        AssertVec(new Vector2(1, 0), Angle.FromDegrees(90).ToWorldVec());
        AssertVec(new Vector2(0, 1), Angle.FromDegrees(180).ToWorldVec());
        AssertVec(new Vector2(-1, 0), Angle.FromDegrees(-90).ToWorldVec());
        // RotateVec (used by the mover) rotates counter-clockwise: (0,1) rotated by +90 is (-1,0) (west).
        AssertVec(new Vector2(0, 1), Angle.Zero.RotateVec(new Vector2(0, 1)));
        AssertVec(new Vector2(-1, 0), Angle.FromDegrees(90).RotateVec(new Vector2(0, 1)));
        // Forward for yaw 0 is north, yaw +90 is west.
        AssertVec(new Vector2(0, 1), CameraYawMath.Forward(Angle.Zero));
        AssertVec(new Vector2(-1, 0), CameraYawMath.Forward(Angle.FromDegrees(90)));
    }

    [Test]
    public void DirectionConventions()
    {
        // Angle zero is "facing the viewer" = South; East is a positive quarter turn.
        Assert.That(Angle.Zero.GetDir(), Is.EqualTo(Direction.South));
        Assert.That(Angle.FromDegrees(90).GetDir(), Is.EqualTo(Direction.East));
        Assert.That(Angle.FromDegrees(180).GetDir(), Is.EqualTo(Direction.North));
        Assert.That(Angle.FromDegrees(270).GetDir(), Is.EqualTo(Direction.West));
        Assert.That(Direction.South.ToAngle(), Is.EqualTo(Angle.Zero));
        Assert.That(Direction.East.ToAngle().Degrees, Is.EqualTo(90).Within(1e-6));
    }

    [Test]
    public void YawRelativeRotationRoundTrip(
        [Values(0, 33, 90, 135, 180, 270, -45, 359)] double yawDeg,
        [Values(0, 90, 180, 270, 12.5)] double gridDeg)
    {
        var yaw = Angle.FromDegrees(yawDeg);
        var grid = Angle.FromDegrees(gridDeg);

        var rel = CameraYawMath.YawToRelativeRotation(yaw, grid);
        var back = CameraYawMath.RelativeRotationToYaw(rel, grid);

        AssertVec(yaw.ToWorldVec(), back.ToWorldVec());
    }

    [Test]
    public void UpInputWalksAlongCameraForward(
        [Values(0, 33, 90, 135, 180, 270, -45)] double yawDeg,
        [Values(0, 90, 180, 270, 12.5)] double gridDeg)
    {
        var yaw = Angle.FromDegrees(yawDeg);
        var grid = Angle.FromDegrees(gridDeg);
        var rel = CameraYawMath.YawToRelativeRotation(yaw, grid);

        // "up" = (0,1) -> camera forward
        AssertVec(CameraYawMath.Forward(yaw), CameraYawMath.InputToWorld(new Vector2(0, 1), grid, rel));
        // "down" -> backwards
        AssertVec(-CameraYawMath.Forward(yaw), CameraYawMath.InputToWorld(new Vector2(0, -1), grid, rel));
        // "right" -> camera right
        AssertVec(CameraYawMath.Right(yaw), CameraYawMath.InputToWorld(new Vector2(1, 0), grid, rel));
        // "left" -> camera left
        AssertVec(-CameraYawMath.Right(yaw), CameraYawMath.InputToWorld(new Vector2(-1, 0), grid, rel));
    }

    [Test]
    public void EyeRotationIsMinusYaw()
    {
        // EyeLerpingSystem derives the eye rotation as -(gridRotation + RelativeRotation).
        var yaw = Angle.FromDegrees(70);
        var grid = Angle.FromDegrees(90);
        var rel = CameraYawMath.YawToRelativeRotation(yaw, grid);
        var eye = -(grid + rel);
        AssertVec((-yaw).ToWorldVec(), eye.ToWorldVec());
    }

    [Test]
    public void ViewAngleToEntity()
    {
        // Entity directly north of the camera: the ray is angle 0.
        Assert.That(CameraYawMath.ViewAngle(Vector2.Zero, new Vector2(0, 5)).Theta, Is.EqualTo(0).Within(1e-6));
        // West of the camera is +90 degrees.
        Assert.That(CameraYawMath.ViewAngle(Vector2.Zero, new Vector2(-5, 0)).Degrees, Is.EqualTo(90).Within(1e-4));
    }

    // Standard SS14 sprite: rotation 0 faces south, i.e. shows its South state toward a viewer to the south.
    // A camera at the origin looking north at an entity 5 tiles north.
    [TestCase(0, Direction.South, TestName = "FacingTowardCameraShowsSouth")]
    [TestCase(180, Direction.North, TestName = "FacingAwayShowsNorth")]
    [TestCase(90, Direction.East, TestName = "FacingCameraRightShowsEast")]
    [TestCase(270, Direction.West, TestName = "FacingCameraLeftShowsWest")]
    public void SpriteDirectionCameraLookingNorth(double entityRotDeg, Direction expected)
    {
        var dir = CameraYawMath.SpriteDirection(Angle.FromDegrees(entityRotDeg), Vector2.Zero, new Vector2(0, 5));
        Assert.That(dir, Is.EqualTo(expected));
    }

    // An entity with rotation 0 faces south. Seen from the south it shows its front (South sprite); from the
    // north its back (North sprite). From the east the camera looks west, so south is to the camera's left: West
    // sprite; from the west the camera looks east, south is to its right: East sprite.
    [TestCase(0, -5, Direction.South, TestName = "ViewedFromSouthShowsSouth")]
    [TestCase(0, 5, Direction.North, TestName = "ViewedFromNorthShowsNorth")]
    [TestCase(5, 0, Direction.West, TestName = "ViewedFromEastShowsWest")]
    [TestCase(-5, 0, Direction.East, TestName = "ViewedFromWestShowsEast")]
    public void SpriteDirectionAroundSouthFacingEntity(float camX, float camY, Direction expected)
    {
        // Entity at the origin, rotation 0 (facing south). Camera positions around it.
        var dir = CameraYawMath.SpriteDirection(Angle.Zero, new Vector2(camX, camY), Vector2.Zero);
        Assert.That(dir, Is.EqualTo(expected));
    }

    [Test]
    public void WalkingTowardCameraShowsSouthAwayShowsNorthStrafeShowsSide()
    {
        var cam = Vector2.Zero;
        var pos = new Vector2(0, 5); // directly ahead of a camera looking north

        // A mob that walks "toward the camera" turns to face south (rotation 0).
        Assert.That(CameraYawMath.SpriteDirection(Angle.Zero, cam, pos), Is.EqualTo(Direction.South));
        // walks away from the camera: faces north (rotation 180)
        Assert.That(CameraYawMath.SpriteDirection(Angle.FromDegrees(180), cam, pos), Is.EqualTo(Direction.North));
        // strafes to the camera's right: faces east (rotation 90)
        Assert.That(CameraYawMath.SpriteDirection(Angle.FromDegrees(90), cam, pos), Is.EqualTo(Direction.East));
        // and to the left
        Assert.That(CameraYawMath.SpriteDirection(Angle.FromDegrees(270), cam, pos), Is.EqualTo(Direction.West));
    }
}
