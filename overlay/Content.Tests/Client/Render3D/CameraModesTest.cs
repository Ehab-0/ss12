using System;
using System.Linq;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>The cameras, how the three stored settings map to them, and the order the camera key goes through.</summary>
[TestFixture]
[TestOf(typeof(CameraModes))]
public sealed class CameraModesTest
{
    [Test]
    public void EveryCameraIsInTheCycleExactlyOnce()
    {
        var all = Enum.GetValues<CameraMode>();
        Assert.That(CameraModes.Cycle, Is.EquivalentTo(all));
        Assert.That(CameraModes.Cycle.Distinct().Count(), Is.EqualTo(CameraModes.Cycle.Length));
    }

    [Test]
    public void TheCameraKeyVisitsEveryCameraAndComesBack()
    {
        var seen = new System.Collections.Generic.List<CameraMode>();
        var mode = CameraMode.ThirdPerson;
        for (var i = 0; i < CameraModes.Cycle.Length; i++)
        {
            seen.Add(mode);
            mode = CameraModes.Next(mode);
        }

        Assert.That(seen, Is.EquivalentTo(CameraModes.Cycle));
        Assert.That(mode, Is.EqualTo(CameraMode.ThirdPerson), "after a full round the key is back where it started");
    }

    [Test]
    public void TheSettingsOfACameraGiveThatCameraBack([ValueSource(nameof(AllModes))] CameraMode mode, [Values(false, true)] bool firstPerson)
    {
        var (first, overhead, extra) = CameraModes.ToSettings(mode, firstPerson);
        Assert.That(CameraModes.FromSettings(first, overhead, extra), Is.EqualTo(mode));
    }

    [Test]
    public void TheHighAndShoulderCamerasKeepTheWalkingCameraTheyCameFrom([Values(false, true)] bool firstPerson)
    {
        foreach (var mode in new[] { CameraMode.Overhead, CameraMode.Isometric, CameraMode.Orbit, CameraMode.Shoulder })
            Assert.That(CameraModes.ToSettings(mode, firstPerson).FirstPerson, Is.EqualTo(firstPerson), $"{mode}");

        // and the walking cameras set it
        Assert.That(CameraModes.ToSettings(CameraMode.FirstPerson, false).FirstPerson, Is.True);
        Assert.That(CameraModes.ToSettings(CameraMode.ThirdPerson, true).FirstPerson, Is.False);
    }

    [Test]
    public void TheOtherCameraSettingWinsOverTheOldFlags()
    {
        Assert.That(CameraModes.FromSettings(true, true, 1), Is.EqualTo(CameraMode.Isometric));
        Assert.That(CameraModes.FromSettings(false, false, 2), Is.EqualTo(CameraMode.Orbit));
        Assert.That(CameraModes.FromSettings(false, false, 3), Is.EqualTo(CameraMode.Shoulder));
        Assert.That(CameraModes.FromSettings(true, true, 0), Is.EqualTo(CameraMode.Overhead));
        Assert.That(CameraModes.FromSettings(true, false, 0), Is.EqualTo(CameraMode.FirstPerson));
        Assert.That(CameraModes.FromSettings(false, false, 0), Is.EqualTo(CameraMode.ThirdPerson));
        // a setting from a newer build that this one does not know is the walking camera, not an error
        Assert.That(CameraModes.FromSettings(false, false, 99), Is.EqualTo(CameraMode.ThirdPerson));
    }

    [Test]
    public void TheWalkingKeySwitchesBetweenTheTwoWalkingCamerasAndLeavesTheOthers()
    {
        Assert.That(CameraModes.ToggleWalking(CameraMode.ThirdPerson, false), Is.EqualTo(CameraMode.FirstPerson));
        Assert.That(CameraModes.ToggleWalking(CameraMode.FirstPerson, true), Is.EqualTo(CameraMode.ThirdPerson));

        foreach (var mode in new[] { CameraMode.Overhead, CameraMode.Isometric, CameraMode.Orbit, CameraMode.Shoulder })
        {
            Assert.That(CameraModes.ToggleWalking(mode, true), Is.EqualTo(CameraMode.FirstPerson), $"{mode}");
            Assert.That(CameraModes.ToggleWalking(mode, false), Is.EqualTo(CameraMode.ThirdPerson), $"{mode}");
        }
    }

    [Test]
    public void OnlyTheHighCamerasAreElevated()
    {
        foreach (var mode in CameraModes.Cycle)
        {
            var cam = new Camera3D { Mode = mode };
            var high = mode is CameraMode.Overhead or CameraMode.Isometric or CameraMode.Orbit;
            Assert.That(cam.Elevated, Is.EqualTo(high), $"{mode}");
        }
    }

    [Test]
    public void EveryCameraHasItsOwnName()
    {
        var ids = CameraModes.Cycle.Select(CameraModes.LocId).ToList();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        Assert.That(ids, Has.All.StartWith("render3d-camera-"));
    }

    public static CameraMode[] AllModes => CameraModes.Cycle;
}
