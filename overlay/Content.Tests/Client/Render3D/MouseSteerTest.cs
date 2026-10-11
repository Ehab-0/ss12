using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class MouseSteerTest
{
    private static readonly Vector2 View = new(1600, 900);

    [Test]
    public void TheCentreOfTheViewIsNoOffset()
    {
        Assert.That(MouseSteer.Offset(View / 2f, View), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void TheJoystickIsRoundWithTheSizeOfTheNarrowerSide()
    {
        var radius = 450f * MouseSteer.FullAt;
        Assert.That(MouseSteer.Radius(View), Is.EqualTo(radius).Within(0.001f));
        Assert.That(MouseSteer.Offset(View / 2f + new Vector2(radius, 0), View).X, Is.EqualTo(1f).Within(0.001f));
        Assert.That(MouseSteer.Offset(View / 2f + new Vector2(0, radius), View).Y, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void NothingTurnsInsideTheDeadZone()
    {
        Assert.That(MouseSteer.Rate(0.1f, 0.12f), Is.Zero);
        Assert.That(MouseSteer.Rate(-0.12f, 0.12f), Is.Zero);
        var (yaw, pitch) = MouseSteer.Turn(new Vector2(0.08f, -0.08f), 0.12f, 150f, 0.016f, true, false);
        Assert.That(yaw, Is.Zero);
        Assert.That(pitch, Is.Zero);
    }

    [Test]
    public void TheRateGrowsSmoothlyAndStopsAtFullSpeed()
    {
        var last = 0f;
        for (var x = 0.13f; x <= 1f; x += 0.01f)
        {
            var rate = MouseSteer.Rate(x, 0.12f);
            Assert.That(rate, Is.GreaterThanOrEqualTo(last));
            last = rate;
        }

        Assert.That(MouseSteer.Rate(1f, 0.12f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(MouseSteer.Rate(3f, 0.12f), Is.EqualTo(1f));
        Assert.That(MouseSteer.Rate(-3f, 0.12f), Is.EqualTo(-1f));
        // half way out turns at a quarter of the full speed, for fine aiming near the middle
        Assert.That(MouseSteer.Rate(0.56f, 0.12f), Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void TheCursorRightTurnsRightAndBelowLooksDown()
    {
        // Camera3D: positive yaw turns counter-clockwise (left), positive pitch looks up
        var (yaw, pitch) = MouseSteer.Turn(new Vector2(1f, 1f), 0.12f, 180f, 1f, true, false);
        Assert.That(yaw, Is.EqualTo(-MathF.PI).Within(0.0001f));
        Assert.That(pitch, Is.EqualTo(-MathF.PI).Within(0.0001f));

        var (_, inverted) = MouseSteer.Turn(new Vector2(1f, 1f), 0.12f, 180f, 1f, true, true);
        Assert.That(inverted, Is.EqualTo(MathF.PI).Within(0.0001f));

        var (_, flat) = MouseSteer.Turn(new Vector2(1f, 1f), 0.12f, 180f, 1f, false, false);
        Assert.That(flat, Is.Zero);
    }

    [Test]
    public void SteeringOnlyStartsOnceTheCursorWasInTheMiddle()
    {
        var far = new Vector2(0.9f, 0f);
        Assert.That(MouseSteer.Arm(false, far, 0.12f), Is.False);
        Assert.That(MouseSteer.Arm(false, new Vector2(0.05f, 0.05f), 0.12f), Is.True);
        // once armed it stays armed wherever the cursor goes
        Assert.That(MouseSteer.Arm(true, far, 0.12f), Is.True);
        // a dead zone of 0 still arms in the very middle
        Assert.That(MouseSteer.Arm(false, Vector2.Zero, 0f), Is.True);
    }

    [Test]
    public void TheEdgeOfAWindowIsWhereALeavingCursorStops()
    {
        var window = new Vector2(1280, 720);
        Assert.That(MouseSteer.AtWindowEdge(new Vector2(0, 300), window), Is.True);
        Assert.That(MouseSteer.AtWindowEdge(new Vector2(1279, 300), window), Is.True);
        Assert.That(MouseSteer.AtWindowEdge(new Vector2(600, 719), window), Is.True);
        Assert.That(MouseSteer.AtWindowEdge(new Vector2(640, 360), window), Is.False);
        Assert.That(MouseSteer.AtWindowEdge(new Vector2(10, 10), window), Is.False);
    }
}
