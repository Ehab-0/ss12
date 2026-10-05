using System;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class CameraYawThrottleTest
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    [Test]
    public void SendIntervalFollowsTheRate()
    {
        Assert.That(CameraYawSystem.SendInterval(10f).TotalSeconds, Is.EqualTo(0.1).Within(1e-6));
        Assert.That(CameraYawSystem.SendInterval(20f).TotalSeconds, Is.EqualTo(0.05).Within(1e-6));
    }

    [Test]
    public void SendIntervalIsKeptWithinTheLimits()
    {
        // never faster than the old 30 per second, never slower than 4 per second, whatever the cvar holds
        Assert.That(CameraYawSystem.SendInterval(1000f).TotalSeconds, Is.EqualTo(1.0 / 30.0).Within(1e-6));
        Assert.That(CameraYawSystem.SendInterval(0f).TotalSeconds, Is.EqualTo(0.25).Within(1e-6));
        Assert.That(CameraYawSystem.SendInterval(-5f).TotalSeconds, Is.EqualTo(0.25).Within(1e-6));
    }

    [Test]
    public void SendIntervalFallsBackForNonFiniteRates()
    {
        Assert.That(CameraYawSystem.SendInterval(float.NaN).TotalSeconds, Is.EqualTo(1.0 / 12.0).Within(1e-6));
        Assert.That(CameraYawSystem.SendInterval(float.PositiveInfinity).TotalSeconds, Is.EqualTo(1.0 / 12.0).Within(1e-6));
    }

    [Test]
    public void NothingIsSentWhileTheYawMatches()
    {
        var yaw = Angle.FromDegrees(40);
        Assert.That(CameraYawSystem.ShouldSend(yaw, yaw, TimeSpan.FromSeconds(10), TimeSpan.Zero, Interval), Is.False);
        // within the one degree threshold
        Assert.That(CameraYawSystem.ShouldSend(Angle.FromDegrees(40.5), yaw, TimeSpan.FromSeconds(10), TimeSpan.Zero, Interval), Is.False);
    }

    [Test]
    public void ATurnIsSentOnceTheIntervalHasPassed()
    {
        var last = TimeSpan.FromSeconds(5);
        var desired = Angle.FromDegrees(20);
        var implied = Angle.Zero;

        Assert.That(CameraYawSystem.ShouldSend(desired, implied, last + TimeSpan.FromMilliseconds(99), last, Interval), Is.False);
        Assert.That(CameraYawSystem.ShouldSend(desired, implied, last + Interval, last, Interval), Is.True);
        Assert.That(CameraYawSystem.ShouldSend(desired, implied, last + TimeSpan.FromSeconds(2), last, Interval), Is.True);
    }

    [Test]
    public void AHeldBackTurnIsStillSentLater()
    {
        // the mouse stops turning inside the interval: the yaw still differs, so the update goes out as soon as it may
        var last = TimeSpan.FromSeconds(1);
        var desired = Angle.FromDegrees(30);
        var implied = Angle.FromDegrees(10);

        Assert.That(CameraYawSystem.ShouldSend(desired, implied, last + TimeSpan.FromMilliseconds(40), last, Interval), Is.False);
        Assert.That(CameraYawSystem.ShouldSend(desired, implied, last + TimeSpan.FromMilliseconds(101), last, Interval), Is.True);
    }

    [Test]
    public void TheShortestWayRoundIsUsed()
    {
        // 359 degrees and 1 degree are 2 degrees apart, not 358
        var last = TimeSpan.Zero;
        var now = TimeSpan.FromSeconds(1);
        Assert.That(CameraYawSystem.ShouldSend(Angle.FromDegrees(359), Angle.FromDegrees(1), now, last, Interval), Is.True);
        Assert.That(CameraYawSystem.ShouldSend(Angle.FromDegrees(359.6), Angle.FromDegrees(0.2), now, last, Interval), Is.False);
    }

    [Test]
    public void AtTheDefaultRateAtMostTwelveUpdatesGoOutPerSecond()
    {
        // simulate a mouse that turns 5 degrees every 8 ms for one second and count what would be sent
        var interval = CameraYawSystem.SendInterval(12f);
        var implied = Angle.Zero;
        var last = TimeSpan.FromSeconds(-1);
        var sent = 0;
        for (var ms = 0; ms < 1000; ms += 8)
        {
            var now = TimeSpan.FromMilliseconds(ms);
            var desired = Angle.FromDegrees(ms / 8 * 5);
            if (!CameraYawSystem.ShouldSend(desired, implied, now, last, interval))
                continue;

            sent++;
            last = now;
            implied = desired;
        }

        Assert.That(sent, Is.InRange(10, 12));
    }
}
