using System;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>The smoothing the dynamic view's wheel glides with: it settles on its target without overshooting.</summary>
[TestFixture]
[TestOf(typeof(Glide))]
public sealed class GlideTest
{
    [Test]
    public void SmoothingApproachesTheTargetWithoutOvershootingIt()
    {
        var value = 1f;
        for (var i = 0; i < 600; i++)
        {
            var next = Glide.Smooth(value, 2f, 1f / 60f);
            Assert.That(next, Is.GreaterThanOrEqualTo(value));
            Assert.That(next, Is.LessThanOrEqualTo(2f));
            value = next;
        }

        Assert.That(value, Is.EqualTo(2f).Within(1e-3f));

        // and the same going down
        value = 2f;
        for (var i = 0; i < 600; i++)
            value = Glide.Smooth(value, 1f, 1f / 60f);
        Assert.That(value, Is.EqualTo(1f).Within(1e-3f));
    }

    [Test]
    public void ItTakesAboutAThirdOfASecondToCoverMostOfTheWay()
    {
        var value = 0f;
        for (var i = 0; i < 20; i++) // a third of a second at 60 frames a second
            value = Glide.Smooth(value, 1f, 1f / 60f);

        Assert.That(value, Is.InRange(0.6f, 0.9f));
    }

    [Test]
    public void AFrameThatTookTooLongOrNoTimeJumpsToTheTarget()
    {
        Assert.That(Glide.Smooth(1f, 2f, 0f), Is.EqualTo(2f));
        Assert.That(Glide.Smooth(1f, 2f, 1f), Is.EqualTo(2f));
        Assert.That(Glide.Smooth(float.NaN, 2f, 0.016f), Is.EqualTo(2f));
    }

    [Test]
    public void TheDampedSpringSettlesOnItsTargetWithoutPassingIt([Values(0.1f, 0.2f, 0.5f)] float smoothTime)
    {
        var value = 0f;
        var velocity = 0f;
        for (var i = 0; i < 600; i++)
        {
            value = Glide.Damp(value, ref velocity, 1f, 1f / 60f, smoothTime);
            Assert.That(value, Is.InRange(0f, 1f));
        }

        Assert.That(value, Is.EqualTo(1f).Within(1e-3f));
    }

    [Test]
    public void TheDampedSpringStartsSoftlyAndKeepsItsSpeedWhenTheTargetMoves()
    {
        var value = 0f;
        var velocity = 0f;
        var first = Glide.Damp(value, ref velocity, 1f, 1f / 60f, 0.2f);
        var second = Glide.Damp(first, ref velocity, 1f, 1f / 60f, 0.2f);
        // it speeds up from rest (an exponential approach would take its biggest step first)
        Assert.That(second - first, Is.GreaterThan(first - value));

        // a target pushed on again while it moves does not stop it: its speed carries over
        value = second;
        var before = velocity;
        value = Glide.Damp(value, ref velocity, 2f, 1f / 60f, 0.2f);
        Assert.That(velocity, Is.GreaterThan(before * 0.5f));
    }

    [Test]
    public void TheDampedSpringJumpsAfterAHitch()
    {
        var velocity = 3f;
        Assert.That(Glide.Damp(0f, ref velocity, 1f, 1f, 0.2f), Is.EqualTo(1f));
        Assert.That(velocity, Is.EqualTo(0f));
        Assert.That(Glide.Damp(float.NaN, ref velocity, 1f, 0.016f, 0.2f), Is.EqualTo(1f));
    }
}
