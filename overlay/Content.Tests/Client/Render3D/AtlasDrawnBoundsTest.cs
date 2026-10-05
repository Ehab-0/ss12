using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class AtlasDrawnBoundsTest
{
    private static readonly Box2 TwoByOne = new(-1f, -0.5f, 1f, 0.5f);

    [Test]
    public void NothingSetLeavesTheBoundsAlone()
    {
        Assert.That(AtlasBounds.Drawn(TwoByOne, Vector2.Zero, Angle.Zero, Angle.Zero), Is.EqualTo(TwoByOne));
    }

    [Test]
    public void ASpriteRotationTurnsTheArtAboutTheOrigin()
    {
        // a wide sprite lying down: a quarter turn makes it tall
        var b = AtlasBounds.Drawn(TwoByOne, Vector2.Zero, Angle.FromDegrees(90), Angle.Zero);
        Assert.That(b.Width, Is.EqualTo(1f).Within(1e-4));
        Assert.That(b.Height, Is.EqualTo(2f).Within(1e-4));
        Assert.That(b.Left, Is.EqualTo(-0.5f).Within(1e-4));
        Assert.That(b.Bottom, Is.EqualTo(-1f).Within(1e-4));
    }

    [Test]
    public void TheOffsetTurnsWithTheEntityButNotWithTheSpriteRotation()
    {
        // the engine moves the layers by the offset after turning them by the sprite rotation, then turns both by the entity
        var offset = new Vector2(0f, 1f);
        var b = AtlasBounds.Drawn(new Box2(-0.5f, -0.5f, 0.5f, 0.5f), offset, Angle.FromDegrees(45), Angle.Zero);
        Assert.That(b.Center.X, Is.EqualTo(0f).Within(1e-4));
        Assert.That(b.Center.Y, Is.EqualTo(1f).Within(1e-4));

        var turned = AtlasBounds.Drawn(new Box2(-0.5f, -0.5f, 0.5f, 0.5f), offset, Angle.Zero, Angle.FromDegrees(90));
        Assert.That(turned.Center.X, Is.EqualTo(-1f).Within(1e-4));
        Assert.That(turned.Center.Y, Is.EqualTo(0f).Within(1e-4));
    }

    [TestCase(0f, 0f, 0f)]
    [TestCase(90f, 0f, 0f)]
    [TestCase(30f, 90f, 0f)]
    [TestCase(30f, 0f, 1f)]
    [TestCase(200f, 45f, 1f)]
    public void EveryCornerOfTheArtIsInsideTheBounds(float entityDegrees, float spriteDegrees, float offsetY)
    {
        var local = new Box2(-0.75f, -0.25f, 0.5f, 0.5f);
        var offset = new Vector2(0.25f, offsetY);
        var entity = Angle.FromDegrees(entityDegrees);
        var sprite = Angle.FromDegrees(spriteDegrees);
        var b = AtlasBounds.Drawn(local, offset, sprite, entity);

        var corners = new[]
        {
            new Vector2(local.Left, local.Bottom), new Vector2(local.Right, local.Bottom),
            new Vector2(local.Right, local.Top), new Vector2(local.Left, local.Top),
        };

        foreach (var corner in corners)
        {
            // the engine's order: sprite rotation, then offset, then the entity rotation
            var p = entity.RotateVec(sprite.RotateVec(corner) + offset);
            Assert.That(p.X, Is.InRange(b.Left - 1e-4f, b.Right + 1e-4f));
            Assert.That(p.Y, Is.InRange(b.Bottom - 1e-4f, b.Top + 1e-4f));
        }
    }

    [Test]
    public void ASpriteThatNeverRotatesIsDrawnUnturned()
    {
        Assert.That(AtlasBounds.DrawnRotation(Angle.FromDegrees(130), noRotation: true, snapCardinals: false).Theta, Is.EqualTo(0).Within(1e-9));
        Assert.That(AtlasBounds.DrawnRotation(Angle.FromDegrees(130), noRotation: true, snapCardinals: true).Theta, Is.EqualTo(0).Within(1e-9));
    }

    [Test]
    public void ASpriteThatSnapsKeepsOnlyWhatIsLeftAfterTheQuarterTurn()
    {
        var left = AtlasBounds.DrawnRotation(Angle.FromDegrees(100), noRotation: false, snapCardinals: true);
        Assert.That(left.Reduced().FlipPositive().Degrees, Is.EqualTo(10).Within(1e-3));

        var back = AtlasBounds.DrawnRotation(Angle.FromDegrees(-80), noRotation: false, snapCardinals: true);
        Assert.That(back.Reduced().FlipPositive().Degrees, Is.EqualTo(10).Within(1e-3));
    }

    [Test]
    public void AnOrdinarySpriteIsDrawnWithTheWholeRotation()
    {
        var a = AtlasBounds.DrawnRotation(Angle.FromDegrees(130), noRotation: false, snapCardinals: false);
        Assert.That(a.Degrees, Is.EqualTo(130).Within(1e-3));
    }
}
