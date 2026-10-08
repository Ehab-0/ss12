using System;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>The dynamic view: one number from first person to the classic 2D view, read off a table of stops.</summary>
[TestFixture]
[TestOf(typeof(DynamicView))]
public sealed class DynamicViewTest
{
    private static readonly DynamicViewSettings Settings = new(
        WalkFov: 80f,
        ThirdDistance: 1.6f,
        IsoPitch: 38f,
        IsoFov: 30f,
        OverheadPitch: 55f,
        OverheadDistance: 9f,
        OverheadFov: 45f,
        TopDistance: 13f,
        WalkWall: 2.3f,
        ElevatedWall: 1.1f);

    // sliders set the way a player had them: a low look-down angle, a closer and wider overhead camera
    private static readonly DynamicViewSettings UserSettings = new(
        WalkFov: 80f,
        ThirdDistance: 1.6f,
        IsoPitch: 38f,
        IsoFov: 30f,
        OverheadPitch: 35f,
        OverheadDistance: 8f,
        OverheadFov: 48f,
        TopDistance: 13f,
        WalkWall: 2.3f,
        ElevatedWall: 0.6f);

    private static DynamicViewState At(float u) => DynamicView.Evaluate(u, Settings);

    [Test]
    public void FirstPersonIsAtTheCharacterLookingLevel()
    {
        var view = At(0f);
        Assert.That(view.Distance, Is.EqualTo(0f));
        Assert.That(view.PitchDown, Is.EqualTo(0f));
        Assert.That(view.FovDegrees, Is.EqualTo(Settings.WalkFov));
        Assert.That(view.TargetHeight, Is.EqualTo(DynamicView.HeadTargetHeight));
        Assert.That(view.WallHeight, Is.EqualTo(Settings.WalkWall));
    }

    [Test]
    public void ThirdPersonIsTheThirdPersonCamera()
    {
        var view = At(DynamicView.ThirdStop);
        Assert.That(view.Distance, Is.EqualTo(Settings.ThirdDistance).Within(1e-5f));
        Assert.That(view.PitchDown, Is.EqualTo(DynamicView.ThirdPitch).Within(1e-5f));
        Assert.That(view.Side, Is.EqualTo(DynamicView.ThirdSide).Within(1e-5f));
        Assert.That(view.FovDegrees, Is.EqualTo(Settings.WalkFov).Within(1e-5f));
    }

    [Test]
    public void IsometricAndOverheadAreTheIsometricAndOverheadCameras()
    {
        var iso = At(DynamicView.IsoStop);
        Assert.That(iso.PitchDown, Is.EqualTo(Settings.IsoPitch).Within(1e-4f));
        Assert.That(iso.FovDegrees, Is.EqualTo(Settings.IsoFov).Within(1e-4f));
        Assert.That(iso.Distance, Is.EqualTo(Settings.OverheadDistance).Within(1e-4f));
        Assert.That(iso.IsoWeight, Is.EqualTo(1f).Within(1e-5f));
        Assert.That(iso.WallHeight, Is.EqualTo(Settings.ElevatedWall).Within(1e-4f));

        var overhead = At(DynamicView.OverheadStop);
        Assert.That(overhead.PitchDown, Is.EqualTo(Settings.OverheadPitch).Within(1e-4f));
        Assert.That(overhead.FovDegrees, Is.EqualTo(Settings.OverheadFov).Within(1e-4f));
        Assert.That(overhead.Distance, Is.EqualTo(Settings.OverheadDistance).Within(1e-4f));
        Assert.That(overhead.IsoWeight, Is.EqualTo(0f).Within(1e-5f));
        Assert.That(overhead.TargetHeight, Is.EqualTo(OverheadCamera.TargetHeight).Within(1e-5f));
    }

    [Test]
    public void TheEndLooksStraightDownFromADistanceThatMatchesTheClassicView()
    {
        foreach (var u in new[] { DynamicView.TopStop, 1f })
        {
            var view = At(u);
            Assert.That(view.PitchDown, Is.EqualTo(DynamicView.TopPitch).Within(1e-4f));
            Assert.That(view.Distance, Is.EqualTo(Settings.TopDistance).Within(1e-4f));
            Assert.That(view.FovDegrees, Is.EqualTo(DynamicView.TopFov).Within(1e-4f));
        }
    }

    /// <summary>How tall (tiles) the picture is at the point looked at.</summary>
    private static float Visible(DynamicViewState view) => DynamicView.Visible(view.Distance, view.FovDegrees);

    [Test]
    public void EveryNotchOutShowsMoreOfTheFloorWithNoPauseAndNoZoomingBackIn()
    {
        // The visible area is what the eye reads as "how far out": it must only grow from the third person stop to the end. (With the
        // distance and the field of view picked independently, the isometric stop's narrow field of view made it shrink a little between
        // the high third person and isometric stops: the camera moved back but the picture zoomed in.)
        foreach (var settings in new[] { Settings, UserSettings })
        {
            var previous = Visible(DynamicView.Evaluate(DynamicView.ThirdStop, settings));
            for (var u = DynamicView.ThirdStop; u <= 1f; u += 0.002f)
            {
                var visible = Visible(DynamicView.Evaluate(u, settings));
                Assert.That(visible, Is.GreaterThanOrEqualTo(previous - 1e-3f), $"the picture zoomed in at {u}");
                previous = visible;
            }
        }
    }

    [Test]
    public void EachNotchChangesTheScaleByAboutTheSameAmountBetweenTheStops()
    {
        // evenly, by ratio: a notch near third person must not feel much bigger or smaller than one near isometric
        var ratios = new System.Collections.Generic.List<float>();
        var previous = Visible(DynamicView.Evaluate(DynamicView.ThirdStop, Settings));
        for (var u = DynamicView.ThirdStop + 0.06f; u <= DynamicView.TopStop; u += 0.06f)
        {
            var visible = Visible(DynamicView.Evaluate(u, Settings));
            ratios.Add(visible / previous);
            previous = visible;
        }

        Assert.That(ratios, Is.All.GreaterThan(1.0f));
        Assert.That(ratios, Is.All.LessThan(1.6f));
    }

    [Test]
    public void TheCameraMovesBackAndLooksFurtherDownAsTheWheelRunsUpToIsometric()
    {
        var distance = -1f;
        var pitch = -1f;
        for (var u = 0f; u <= DynamicView.IsoStop + 1e-4f; u += 0.01f)
        {
            var view = At(u);
            Assert.That(view.Distance, Is.GreaterThanOrEqualTo(distance - 1e-4f), $"distance at {u}");
            Assert.That(view.PitchDown, Is.GreaterThanOrEqualTo(pitch - 1e-4f), $"pitch at {u}");
            distance = view.Distance;
            pitch = view.PitchDown;
        }
    }

    [Test]
    public void NothingJumpsBetweenTwoCloseNeighbours()
    {
        var previous = At(0f);
        for (var u = 0.001f; u <= 1f; u += 0.001f)
        {
            var view = At(u);
            Assert.That(Math.Abs(view.Distance - previous.Distance), Is.LessThan(0.1f), $"distance at {u}");
            Assert.That(Math.Abs(view.PitchDown - previous.PitchDown), Is.LessThan(0.5f), $"pitch at {u}");
            Assert.That(Math.Abs(view.FovDegrees - previous.FovDegrees), Is.LessThan(0.5f), $"fov at {u}");
            Assert.That(Math.Abs(view.TargetHeight - previous.TargetHeight), Is.LessThan(0.01f), $"height at {u}");
            Assert.That(Math.Abs(view.Side - previous.Side), Is.LessThan(0.01f), $"side at {u}");
            Assert.That(Math.Abs(view.IsoWeight - previous.IsoWeight), Is.LessThan(0.02f), $"iso at {u}");
            Assert.That(Math.Abs(view.WallHeight - previous.WallHeight), Is.LessThan(0.02f), $"wall at {u}");
            previous = view;
        }
    }

    [Test]
    public void TheWallsComeDownOnlyOnceTheCameraIsHighEnough()
    {
        Assert.That(At(DynamicView.WallStart).WallHeight, Is.EqualTo(Settings.WalkWall).Within(1e-5f));
        Assert.That(At(0f).WallHeight, Is.EqualTo(Settings.WalkWall));

        var wall = float.MaxValue;
        for (var u = 0f; u <= 1f; u += 0.01f)
        {
            var height = At(u).WallHeight;
            Assert.That(height, Is.LessThanOrEqualTo(wall + 1e-5f), $"walls at {u}");
            wall = height;
        }

        Assert.That(At(1f).WallHeight, Is.EqualTo(Settings.ElevatedWall).Within(1e-5f));
    }

    [Test]
    public void TheClassicViewFadesInOnlyAtTheEnd()
    {
        Assert.That(DynamicView.Fade(0f), Is.EqualTo(0f));
        Assert.That(DynamicView.Fade(DynamicView.FadeStart), Is.EqualTo(0f));
        Assert.That(DynamicView.Fade(DynamicView.FadeEnd), Is.EqualTo(1f));
        Assert.That(DynamicView.Fade(1f), Is.EqualTo(1f));

        var fade = 0f;
        for (var u = 0f; u <= 1f; u += 0.001f)
        {
            var f = DynamicView.Fade(u);
            Assert.That(f, Is.GreaterThanOrEqualTo(fade - 1e-6f), $"fade at {u}");
            fade = f;
        }

        // the switch to the real 2D view comes after the picture is fully the 2D one
        Assert.That(DynamicView.TwoDOn, Is.GreaterThan(DynamicView.FadeEnd));
    }

    [Test]
    public void TheWheelMovesTheCameraTowardsTheCharacterWhenRolledAwayFromYou()
    {
        Assert.That(DynamicView.Step(0.5f, 1f, 0.06f, false, 1f), Is.EqualTo(0.44f).Within(1e-5f));
        Assert.That(DynamicView.Step(0.5f, -1f, 0.06f, false, 1f), Is.EqualTo(0.56f).Within(1e-5f));
        Assert.That(DynamicView.Step(0.5f, 2f, 0.06f, false, 1f), Is.EqualTo(0.38f).Within(1e-5f));
    }

    [Test]
    public void InvertingTheWheelTurnsItRoundAndTheEndsAreClamped()
    {
        Assert.That(DynamicView.Step(0.5f, 1f, 0.06f, true, 1f), Is.EqualTo(0.56f).Within(1e-5f));
        Assert.That(DynamicView.Step(0.02f, 5f, 0.06f, false, 1f), Is.EqualTo(0f));
        Assert.That(DynamicView.Step(0.98f, -5f, 0.06f, false, 1f), Is.EqualTo(1f));
        Assert.That(DynamicView.Step(0.9f, -5f, 0.06f, false, DynamicView.TopStop), Is.EqualTo(DynamicView.TopStop));
        Assert.That(DynamicView.Step(0.5f, 0f, 0.06f, false, 1f), Is.EqualTo(0.5f));
        Assert.That(DynamicView.Step(0.5f, float.NaN, 0.06f, false, 1f), Is.EqualTo(0.5f));
    }

    [Test]
    public void WithoutTheClassicViewTheScrollEndsAtTheStraightDownView()
    {
        Assert.That(DynamicView.MaxU(false), Is.EqualTo(DynamicView.TopStop));
        Assert.That(DynamicView.MaxU(true), Is.EqualTo(1f));
        // and the straight-down view has no 2D fade in it
        Assert.That(DynamicView.Fade(DynamicView.MaxU(false)), Is.EqualTo(0f));
    }

    [Test]
    public void ASwitchDoesNotFlickerWhereTheWheelRestsOnIt()
    {
        var on = false;
        // coming up: on at the "on" point
        on = DynamicView.Latch(on, DynamicView.FreeOff + 0.001f, DynamicView.FreeOn, DynamicView.FreeOff);
        Assert.That(on, Is.False);
        on = DynamicView.Latch(on, DynamicView.FreeOn, DynamicView.FreeOn, DynamicView.FreeOff);
        Assert.That(on, Is.True);

        // between the two points it stays as it is, going down as well
        on = DynamicView.Latch(on, (DynamicView.FreeOn + DynamicView.FreeOff) / 2f, DynamicView.FreeOn, DynamicView.FreeOff);
        Assert.That(on, Is.True);
        on = DynamicView.Latch(on, DynamicView.FreeOff - 0.001f, DynamicView.FreeOn, DynamicView.FreeOff);
        Assert.That(on, Is.False);
        on = DynamicView.Latch(on, (DynamicView.FreeOn + DynamicView.FreeOff) / 2f, DynamicView.FreeOn, DynamicView.FreeOff);
        Assert.That(on, Is.False);
    }

    [Test]
    public void TheSwitchesComeInTheOrderTheCameraNeedsThem()
    {
        // no ceiling before the camera can be above it, the free cursor after, and the walls are down by the isometric stop
        Assert.That(DynamicView.NoCeilingOn, Is.LessThan(DynamicView.FreeOn));
        Assert.That(DynamicView.FreeOn, Is.LessThan(DynamicView.IsoStop));
        Assert.That(DynamicView.WallEnd, Is.EqualTo(DynamicView.IsoStop));
        Assert.That(DynamicView.NoCeilingOff, Is.LessThan(DynamicView.NoCeilingOn));
        Assert.That(DynamicView.FreeOff, Is.LessThan(DynamicView.FreeOn));
        Assert.That(DynamicView.TwoDOff, Is.LessThan(DynamicView.TwoDOn));
    }

    [Test]
    public void TheScrollCameraCountsAsAHighOneOnlyOnceItIsFree()
    {
        var cam = new Camera3D { Mode = CameraMode.DynamicView };
        Assert.That(cam.Elevated, Is.False);
        cam.ScrollElevated = true;
        Assert.That(cam.Elevated, Is.True);

        // the flag means nothing for the other cameras
        var third = new Camera3D { Mode = CameraMode.ThirdPerson, ScrollElevated = true };
        Assert.That(third.Elevated, Is.False);
    }
}
