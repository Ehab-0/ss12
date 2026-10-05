using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class AtlasTileCropTest
{
    [Test]
    public void AOneTileSpriteIsNotCropped()
    {
        var b = new Box2(-0.5f, -0.5f, 0.5f, 0.5f);
        Assert.That(AtlasBounds.TileCrop(b), Is.EqualTo(b));
    }

    [Test]
    public void ASpriteSmallerThanTheTileKeepsItsOwnBounds()
    {
        var b = new Box2(-0.25f, -0.25f, 0.25f, 0.25f);
        Assert.That(AtlasBounds.TileCrop(b), Is.EqualTo(b));
    }

    [Test]
    public void TheDockingClampBelowTheDoorIsCroppedAway()
    {
        // the docking airlock: the door in the tile of the entity and a clamp one tile below it
        var crop = AtlasBounds.TileCrop(new Box2(-0.5f, -1.5f, 0.5f, 0.5f));
        Assert.That(crop, Is.EqualTo(new Box2(-0.5f, -0.5f, 0.5f, 0.5f)));
    }

    [Test]
    public void ASpriteStickingOutOnEverySideIsCroppedToTheTile()
    {
        var crop = AtlasBounds.TileCrop(new Box2(-1f, -1.5f, 2f, 1f));
        Assert.That(crop, Is.EqualTo(new Box2(-0.5f, -0.5f, 0.5f, 0.5f)));
    }

    [Test]
    public void BoundsWhollyOutsideTheTileAreLeftAlone()
    {
        var b = new Box2(2f, 2f, 3f, 3f);
        Assert.That(AtlasBounds.TileCrop(b), Is.EqualTo(b));
    }
}
