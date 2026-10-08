using System;
using System.Numerics;
using Content.Client.Render3D;
using Content.Shared.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

/// <summary>The isometric heading (the overhead camera turned a diagonal, and the dynamic view's isometric step): a diagonal heading on the grid, movement that still follows the screen, and stable sprite directions.</summary>
[TestFixture]
public sealed class IsometricCameraTest
{
    private static Camera3D MakeCamera(double yaw)
    {
        var cam = new Camera3D
        {
            Mode = CameraMode.Overhead,
            Yaw = yaw,
            Pitch = -MathHelper.DegreesToRadians(38f),
            FovY = MathHelper.DegreesToRadians(30f),
            Size = new Vector2(1920, 1080),
        };
        cam.UpdateBasis();
        return cam;
    }

    [Test]
    public void TheViewIsTurnedADiagonalFromTheGridsAxes([Values(0, 90, -30, 200)] double gridDegrees, [Values(0, 1, 2, 3)] int turns)
    {
        var grid = Angle.FromDegrees(gridDegrees);
        var overhead = OverheadCamera.Yaw(grid, turns);
        var isometric = OverheadCamera.IsometricYaw(grid, turns);

        var difference = Angle.ShortestDistance(new Angle(overhead), new Angle(isometric));
        Assert.That(difference.Degrees, Is.EqualTo(45).Within(1e-6));
    }

    [Test]
    public void UpWalksTowardsTheTopOfTheScreenOnAnyGrid([Values(0, 33, 90, -140)] double gridDegrees, [Values(0, 1, 2, 3)] int turns)
    {
        var grid = Angle.FromDegrees(gridDegrees);
        var yaw = OverheadCamera.IsometricYaw(grid, turns);
        var cam = MakeCamera(yaw);

        var relative = CameraYawMath.YawToRelativeRotation(new Angle(yaw), grid);
        var walk = CameraYawMath.InputToWorld(new Vector2(0, 1), grid, relative);

        Assert.That(walk.X, Is.EqualTo(cam.ForwardGround.X).Within(1e-4f));
        Assert.That(walk.Y, Is.EqualTo(cam.ForwardGround.Y).Within(1e-4f));
    }

    [Test]
    public void TheGridsNorthRunsAlongTheDiagonalOfTheScreen()
    {
        var cam = MakeCamera(OverheadCamera.IsometricYaw(Angle.Zero, 0));
        var north = new Vector2(0, 1);

        // half way between "up" and "right" on the screen: the same angle to the screen's up and to its right
        Assert.That(Vector2.Dot(north, cam.ForwardGround), Is.EqualTo(MathF.Cos(MathF.PI / 4)).Within(1e-4f));
        Assert.That(Math.Abs(Vector2.Dot(north, new Vector2(cam.Right.X, cam.Right.Y))), Is.EqualTo(MathF.Sin(MathF.PI / 4)).Within(1e-4f));
    }

    [Test]
    public void ASpriteDirectionDoesNotFlipOnRoundingNoiseWhenAThingSitsBetweenTwoDirections()
    {
        // with a diagonal view, things turned by a quarter turn sit exactly between two directions
        var yaw = OverheadCamera.IsometricYaw(Angle.Zero, 0);
        foreach (var rotationDegrees in new[] { 0.0, 90.0, 180.0, 270.0 })
        {
            var rotation = Angle.FromDegrees(rotationDegrees);
            var expected = CameraYawMath.OverheadSpriteDirection(rotation, new Angle(yaw));
            foreach (var noise in new[] { -1e-12, -1e-9, 1e-12, 1e-9 })
            {
                Assert.That(CameraYawMath.OverheadSpriteDirection(rotation, new Angle(yaw + noise)), Is.EqualTo(expected),
                    $"rotation {rotationDegrees}, noise {noise}");
            }
        }
    }

    [Test]
    public void TheFourViewsShowFourDifferentSidesOfAThingThatDoesNotTurn()
    {
        var seen = new System.Collections.Generic.HashSet<Direction>();
        for (var turns = 0; turns < 4; turns++)
            seen.Add(CameraYawMath.OverheadSpriteDirection(Angle.Zero, new Angle(OverheadCamera.IsometricYaw(Angle.Zero, turns))));

        Assert.That(seen, Has.Count.EqualTo(4));
    }
}
