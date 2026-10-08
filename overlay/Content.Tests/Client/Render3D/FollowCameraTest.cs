using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>The follow camera: it looks where the character is looking, short of walls, and settles on that smoothly.</summary>
[TestFixture]
[TestOf(typeof(FollowCamera))]
public sealed class FollowCameraTest
{
    private static readonly float Open = float.PositiveInfinity;

    [Test]
    public void TheFocusMovesAheadOfTheCharacterInTheDirectionItFaces()
    {
        var offset = FollowCamera.LookAhead(new Vector2(0, 1), 2.5f, Open);
        Assert.That(offset.X, Is.EqualTo(0f).Within(1e-5f));
        Assert.That(offset.Y, Is.EqualTo(2.5f).Within(1e-5f));

        var diagonal = FollowCamera.LookAhead(new Vector2(1, 1), 3f, Open);
        Assert.That(diagonal.Length(), Is.EqualTo(3f).Within(1e-4f));
        Assert.That(diagonal.X, Is.EqualTo(diagonal.Y).Within(1e-5f));
    }

    [Test]
    public void TheLengthOfTheFacingVectorDoesNotMatter()
    {
        var a = FollowCamera.LookAhead(new Vector2(0, -1), 2f, Open);
        var b = FollowCamera.LookAhead(new Vector2(0, -40), 2f, Open);
        Assert.That(b.X, Is.EqualTo(a.X).Within(1e-5f));
        Assert.That(b.Y, Is.EqualTo(a.Y).Within(1e-5f));
    }

    [Test]
    public void TheFocusStopsShortOfAWall()
    {
        // a wall 2 tiles ahead: the focus stays WallMargin short of it
        var offset = FollowCamera.LookAhead(new Vector2(1, 0), 4f, 2f);
        Assert.That(offset.X, Is.EqualTo(2f - FollowCamera.WallMargin).Within(1e-5f));

        // a wall farther than the lead does not shorten it
        Assert.That(FollowCamera.LookAhead(new Vector2(1, 0), 4f, 20f).X, Is.EqualTo(4f).Within(1e-5f));

        // a wall right at the character gives no lead, never a negative one
        Assert.That(FollowCamera.LookAhead(new Vector2(1, 0), 4f, 0.2f), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void NoFacingOrNoLeadGivesNoOffset()
    {
        Assert.That(FollowCamera.LookAhead(Vector2.Zero, 2.5f, Open), Is.EqualTo(Vector2.Zero));
        Assert.That(FollowCamera.LookAhead(new Vector2(0, 1), 0f, Open), Is.EqualTo(Vector2.Zero));
        Assert.That(FollowCamera.LookAhead(new Vector2(0, 1), -3f, Open), Is.EqualTo(Vector2.Zero));
        Assert.That(FollowCamera.LookAhead(new Vector2(float.NaN, 1), 2f, Open), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void TheLeadIsLimited()
    {
        Assert.That(FollowCamera.LookAhead(new Vector2(0, 1), 100f, Open).Length(), Is.EqualTo(FollowCamera.MaxLead).Within(1e-5f));
    }

    [Test]
    public void SmoothingApproachesTheTargetWithoutOvershootingIt()
    {
        var value = 1f;
        for (var i = 0; i < 600; i++)
        {
            var next = FollowCamera.Smooth(value, 2f, 1f / 60f);
            Assert.That(next, Is.GreaterThanOrEqualTo(value));
            Assert.That(next, Is.LessThanOrEqualTo(2f));
            value = next;
        }

        Assert.That(value, Is.EqualTo(2f).Within(1e-3f));

        // and the same going down
        value = 2f;
        for (var i = 0; i < 600; i++)
            value = FollowCamera.Smooth(value, 1f, 1f / 60f);
        Assert.That(value, Is.EqualTo(1f).Within(1e-3f));
    }

    [Test]
    public void APointSettlesOnItsTargetAlongTheStraightLine()
    {
        var current = Vector2.Zero;
        var target = new Vector2(3f, -4f);
        for (var i = 0; i < 600; i++)
        {
            current = FollowCamera.Smooth(current, target, 1f / 60f);
            // always on the line from the start to the target
            Assert.That(current.X * target.Y - current.Y * target.X, Is.EqualTo(0f).Within(1e-3f));
        }

        Assert.That(current.X, Is.EqualTo(3f).Within(1e-3f));
        Assert.That(current.Y, Is.EqualTo(-4f).Within(1e-3f));
    }

    [Test]
    public void ItTakesAboutAThirdOfASecondToCoverMostOfTheWay()
    {
        var value = 0f;
        for (var i = 0; i < 20; i++) // a third of a second at 60 frames a second
            value = FollowCamera.Smooth(value, 1f, 1f / 60f);

        Assert.That(value, Is.InRange(0.6f, 0.9f));
    }

    [Test]
    public void AFrameThatTookTooLongOrNoTimeJumpsToTheTarget()
    {
        Assert.That(FollowCamera.Smooth(1f, 2f, 0f), Is.EqualTo(2f));
        Assert.That(FollowCamera.Smooth(1f, 2f, 1f), Is.EqualTo(2f));
        Assert.That(FollowCamera.Smooth(float.NaN, 2f, 0.016f), Is.EqualTo(2f));
        Assert.That(FollowCamera.Smooth(Vector2.Zero, new Vector2(1, 1), 1f), Is.EqualTo(new Vector2(1, 1)));
        Assert.That(FollowCamera.Smooth(new Vector2(float.NaN, 0), new Vector2(1, 1), 0.016f), Is.EqualTo(new Vector2(1, 1)));
    }
}
