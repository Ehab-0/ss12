using System;
using System.Numerics;
using Content.Client.Render3D;
using Content.Shared.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

/// <summary>The overhead camera: which way it looks, where it hangs, and that the movement keys follow what is on screen.</summary>
[TestFixture]
[TestOf(typeof(OverheadCamera))]
public sealed class OverheadCameraTest
{
    private static Camera3D MakeCamera(double yaw, float pitchDegrees = 55f, int fovDegrees = 45)
    {
        var cam = new Camera3D
        {
            Mode = CameraMode.Overhead,
            Yaw = yaw,
            Pitch = -MathHelper.DegreesToRadians(pitchDegrees),
            FovY = MathHelper.DegreesToRadians(fovDegrees),
            Size = new Vector2(1920, 1080),
        };
        cam.UpdateBasis();
        return cam;
    }

    [Test]
    public void UpWalksTowardsTheTopOfTheScreen(
        [Values(0, 30, 90, -75, 180)] double gridDegrees,
        [Values(0, 1, 2, 3)] int quarterTurns)
    {
        var grid = Angle.FromDegrees(gridDegrees);
        var yaw = OverheadCamera.Yaw(grid, quarterTurns);
        var cam = MakeCamera(yaw);

        // what the server makes of that yaw (SharedCameraYawSystem.SetYaw) and where the "up" key then walks
        var relative = CameraYawMath.YawToRelativeRotation(new Angle(yaw), grid);
        var walk = CameraYawMath.InputToWorld(new Vector2(0, 1), grid, relative);

        Assert.That(walk.X, Is.EqualTo(cam.ForwardGround.X).Within(1e-4f));
        Assert.That(walk.Y, Is.EqualTo(cam.ForwardGround.Y).Within(1e-4f));

        // and the mover itself is turned by whole quarter turns, like the classic camera
        var quarters = relative.Theta / (Math.PI / 2);
        Assert.That(Math.Abs(quarters - Math.Round(quarters)), Is.LessThan(1e-6), $"relative rotation {relative.Degrees} is not a quarter turn");
    }

    [Test]
    public void NorthIsUpOnAGridThatIsNotTurned()
    {
        var cam = MakeCamera(OverheadCamera.Yaw(Angle.Zero, 0));

        Assert.That(cam.ForwardGround.X, Is.EqualTo(0f).Within(1e-5f));
        Assert.That(cam.ForwardGround.Y, Is.EqualTo(1f).Within(1e-5f));
        // east is to the right of the screen
        Assert.That(cam.Right.X, Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void TurnKeysStepByQuarterTurnsAndWrap()
    {
        Assert.That(OverheadCamera.Turn(0, 1), Is.EqualTo(1));
        Assert.That(OverheadCamera.Turn(3, 1), Is.EqualTo(0));
        Assert.That(OverheadCamera.Turn(0, -1), Is.EqualTo(3));
        Assert.That(OverheadCamera.Turn(2, -1), Is.EqualTo(1));
        Assert.That(OverheadCamera.Turn(2, 0), Is.EqualTo(0), "reset goes back to north up");
        Assert.That(OverheadCamera.Turn(1, 4), Is.EqualTo(1));
    }

    [Test]
    public void TheCameraHangsHighAndLooksAtTheCharacter(
        [Values(0, 90, 217)] double yawDegrees,
        [Values(40f, 55f, 70f)] float pitch,
        [Values(4f, 9f, 16f)] float distance)
    {
        var cam = MakeCamera(MathHelper.DegreesToRadians(yawDegrees), pitch);
        var character = new Vector2(5f, -7f);
        cam.Position = OverheadCamera.Position(character, cam.Forward, distance);

        // the character is in the middle of the picture, `distance` away along the line of sight
        Assert.That(cam.Project(new Vector3(character, OverheadCamera.TargetHeight), out var pixel, out var depth), Is.True);
        Assert.That(pixel.X, Is.EqualTo(cam.Size.X / 2f).Within(0.5f));
        Assert.That(pixel.Y, Is.EqualTo(cam.Size.Y / 2f).Within(0.5f));
        Assert.That(depth, Is.EqualTo(distance).Within(1e-3f));

        // well above the walls the overhead view uses (1.1 tiles), so a wall can never be between the camera and its own top
        Assert.That(cam.Position.Z, Is.GreaterThan(1.1f));
    }

    [Test]
    public void SpritesTurnWithTheViewLikeTheClassicEye()
    {
        // yaw 0: the view is the classic one
        Assert.That(CameraYawMath.OverheadSpriteDirection(Angle.Zero, Angle.Zero), Is.EqualTo(Direction.South));
        Assert.That(CameraYawMath.OverheadSpriteDirection(Angle.FromDegrees(90), Angle.Zero), Is.EqualTo(Direction.East));
        Assert.That(CameraYawMath.OverheadSpriteDirection(Angle.FromDegrees(180), Angle.Zero), Is.EqualTo(Direction.North));

        // a quarter turn to the left (the camera now looks west): something that faces its own way keeps facing the same way
        // on screen only if it turned with the camera, and one that did not turn shows the side that now faces the viewer
        var west = Angle.FromDegrees(90);
        Assert.That(CameraYawMath.OverheadSpriteDirection(west, west), Is.EqualTo(Direction.South));
        Assert.That(CameraYawMath.OverheadSpriteDirection(Angle.Zero, west), Is.EqualTo(Direction.West));
    }
}
