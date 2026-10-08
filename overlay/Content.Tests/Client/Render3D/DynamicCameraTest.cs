using System;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>The dynamic camera: how open a place is, what camera that asks for, and the smoothing towards it.</summary>
[TestFixture]
[TestOf(typeof(DynamicCamera))]
public sealed class DynamicCameraTest
{
    private const float Range = DynamicCamera.ProbeRange;

    [Test]
    public void ACrampedPlaceIsNotOpenAndAnEmptyOneIs()
    {
        Assert.That(DynamicCamera.Openness(new float[] { 0.4f, 0.4f, 0.4f, 0.4f, 0.4f, 0.4f, 0.4f, 0.4f }, Range), Is.LessThan(0.1f));
        Assert.That(DynamicCamera.Openness(new float[] { Range, Range, Range, Range }, Range), Is.EqualTo(1f).Within(1e-6f));
    }

    [Test]
    public void ACorridorIsInBetween()
    {
        // walls close on two sides, open along the corridor
        var corridor = new float[] { Range, 0.6f, 0.6f, Range, Range, 0.6f, 0.6f, Range };
        var open = DynamicCamera.Openness(corridor, Range);
        Assert.That(open, Is.GreaterThan(DynamicCamera.Openness(new float[] { 0.6f, 0.6f }, Range)));
        Assert.That(open, Is.LessThan(1f));
    }

    [Test]
    public void RaysFartherThanTheRangeCountAsTheRange()
    {
        Assert.That(DynamicCamera.Openness(new float[] { 100f, 100f }, Range), Is.EqualTo(1f).Within(1e-6f));
        Assert.That(DynamicCamera.Openness(new float[] { -3f }, Range), Is.EqualTo(0f).Within(1e-6f));
    }

    [Test]
    public void NoRaysMeansNothingIsKnownAndTheCameraIsNotMoved()
    {
        var (scale, pitch) = DynamicCamera.Target(DynamicCamera.Openness(ReadOnlySpan<float>.Empty, Range), 0f);
        Assert.That(DynamicCamera.Openness(ReadOnlySpan<float>.Empty, Range), Is.EqualTo(1f));
        Assert.That(scale, Is.GreaterThan(1f).Or.EqualTo(1f), "an open place does not pull the camera in");
        Assert.That(pitch, Is.LessThanOrEqualTo(0f));
    }

    [Test]
    public void TighterPlacesPullTheCameraInAndTiltItUp()
    {
        var previousScale = 0f;
        var previousPitch = float.MaxValue;
        for (var open = 0f; open <= 1.0001f; open += 0.1f)
        {
            var (scale, pitch) = DynamicCamera.Target(open, 0f);
            Assert.That(scale, Is.GreaterThan(previousScale), $"scale at openness {open}");
            Assert.That(pitch, Is.LessThan(previousPitch), $"pitch at openness {open}");
            previousScale = scale;
            previousPitch = pitch;
        }

        var tight = DynamicCamera.Target(0f, 0f);
        var wide = DynamicCamera.Target(1f, 0f);
        Assert.That(tight.DistanceScale, Is.EqualTo(DynamicCamera.TightScale).Within(1e-5f));
        Assert.That(wide.DistanceScale, Is.EqualTo(DynamicCamera.OpenScale).Within(1e-5f));
        Assert.That(tight.PitchOffsetDegrees, Is.EqualTo(DynamicCamera.TightPitchOffset).Within(1e-5f));
        Assert.That(wide.PitchOffsetDegrees, Is.EqualTo(DynamicCamera.OpenPitchOffset).Within(1e-5f));
    }

    [Test]
    public void RunningPullsTheCameraBackALittle()
    {
        var still = DynamicCamera.Target(0.5f, 0f).DistanceScale;
        var running = DynamicCamera.Target(0.5f, 1f).DistanceScale;
        Assert.That(running, Is.EqualTo(still * (1f + DynamicCamera.SpeedScale)).Within(1e-5f));
        Assert.That(DynamicCamera.Target(0.5f, 7f).DistanceScale, Is.EqualTo(running).Within(1e-6f), "speed past the top counts as the top");
    }

    [Test]
    public void ValuesOutsideTheirRangeAreClamped()
    {
        Assert.That(DynamicCamera.Target(-5f, -5f), Is.EqualTo(DynamicCamera.Target(0f, 0f)));
        Assert.That(DynamicCamera.Target(5f, 5f), Is.EqualTo(DynamicCamera.Target(1f, 1f)));
    }

    [Test]
    public void SmoothingApproachesTheTargetWithoutOvershootingIt()
    {
        var value = 1f;
        for (var i = 0; i < 600; i++)
        {
            var next = DynamicCamera.Smooth(value, 2f, 1f / 60f);
            Assert.That(next, Is.GreaterThanOrEqualTo(value));
            Assert.That(next, Is.LessThanOrEqualTo(2f));
            value = next;
        }

        Assert.That(value, Is.EqualTo(2f).Within(1e-3f));

        // and the same going down
        value = 2f;
        for (var i = 0; i < 600; i++)
            value = DynamicCamera.Smooth(value, 1f, 1f / 60f);
        Assert.That(value, Is.EqualTo(1f).Within(1e-3f));
    }

    [Test]
    public void ItTakesAboutAThirdOfASecondToCoverMostOfTheWay()
    {
        var value = 0f;
        for (var i = 0; i < 20; i++) // a third of a second at 60 frames a second
            value = DynamicCamera.Smooth(value, 1f, 1f / 60f);

        Assert.That(value, Is.InRange(0.6f, 0.9f));
    }

    [Test]
    public void AFrameThatTookTooLongOrNoTimeJumpsToTheTarget()
    {
        Assert.That(DynamicCamera.Smooth(1f, 2f, 0f), Is.EqualTo(2f));
        Assert.That(DynamicCamera.Smooth(1f, 2f, 1f), Is.EqualTo(2f));
        Assert.That(DynamicCamera.Smooth(float.NaN, 2f, 0.016f), Is.EqualTo(2f));
    }
}
