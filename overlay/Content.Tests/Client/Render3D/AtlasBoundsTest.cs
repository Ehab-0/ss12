using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class AtlasBoundsTest
{
    private static readonly Box2 OneTile = new(-0.5f, -0.5f, 0.5f, 0.5f);

    /// <summary>
    ///     Where the art of a sprite lands, in pixels relative to the top left corner of its slot, when the atlas draws it at
    ///     the slot origin the way <see cref="BillboardAtlas.Draw"/> does (the engine adds the sprite offset on top of that).
    /// </summary>
    private static UIBox2 ArtInSlot(Box2 slotBounds, Box2 local, Vector2 spriteOffset, out UIBox2i slot)
    {
        var atlas = new BillboardAtlas(1024);
        atlas.BeginFrame();
        Assert.That(atlas.TryAllocate(slotBounds, out slot, out var origin), Is.True);

        // screen y grows downwards; world y grows upwards
        var art = local.Translated(spriteOffset);
        var px = BillboardAtlas.PixelsPerTile;
        return new UIBox2(
            origin.X + art.Left * px,
            origin.Y - art.Top * px,
            origin.X + art.Right * px,
            origin.Y - art.Bottom * px);
    }

    private static bool Inside(UIBox2 art, UIBox2i slot)
    {
        const float eps = 0.01f;
        return art.Left >= -eps && art.Top >= -eps && art.Right <= slot.Width + eps && art.Bottom <= slot.Height + eps;
    }

    [Test]
    public void NoOffsetLeavesTheBoundsAlone()
    {
        Assert.That(AtlasBounds.Unrotated(OneTile, Vector2.Zero), Is.EqualTo(OneTile));
        Assert.That(AtlasBounds.Rotated(OneTile, Vector2.Zero, Angle.Zero), Is.EqualTo(OneTile));
    }

    [Test]
    public void AnOffsetMovesTheBoundsByIt()
    {
        var b = AtlasBounds.Unrotated(OneTile, new Vector2(0f, 1f));
        Assert.That(b.Left, Is.EqualTo(-0.5f).Within(1e-5));
        Assert.That(b.Right, Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(b.Bottom, Is.EqualTo(0.5f).Within(1e-5));
        Assert.That(b.Top, Is.EqualTo(1.5f).Within(1e-5));
    }

    [Test]
    public void TheOffsetTurnsWithAFlatSprite()
    {
        // a quarter turn counter-clockwise takes an offset of one tile up to one tile left
        var b = AtlasBounds.Rotated(OneTile, new Vector2(0f, 1f), Angle.FromDegrees(90));
        Assert.That(b.Center.X, Is.EqualTo(-1f).Within(1e-4));
        Assert.That(b.Center.Y, Is.EqualTo(0f).Within(1e-4));
        Assert.That(b.Width, Is.EqualTo(1f).Within(1e-4));
    }

    [Test]
    public void ARotatedSpriteKeepsItsSizeInTheBoundsWhenTheOffsetIsZero()
    {
        var b = AtlasBounds.Rotated(OneTile, Vector2.Zero, Angle.FromDegrees(45));
        Assert.That(b.Width, Is.EqualTo(MathF.Sqrt(2f)).Within(1e-4));
        Assert.That(b.Center.Length(), Is.EqualTo(0f).Within(1e-4));
    }

    [Test]
    public void TheArtOfAnOffsetSpriteIsInsideItsSlot()
    {
        // a wall lamp: one tile of art, drawn one tile above the entity
        var offset = new Vector2(0f, 1f);
        var art = ArtInSlot(AtlasBounds.Unrotated(OneTile, offset), OneTile, offset, out var slot);
        Assert.That(Inside(art, slot), Is.True, $"art {art} slot {slot}");
    }

    [Test]
    public void WithoutTheOffsetTheArtLandsOutsideItsSlot()
    {
        // what the atlas did before: the slot came from the plain local bounds, so the art was one tile (32 pixels) above it,
        // on top of the neighbouring slot
        var offset = new Vector2(0f, 1f);
        var art = ArtInSlot(OneTile, OneTile, offset, out var slot);
        Assert.That(Inside(art, slot), Is.False);
        Assert.That(art.Bottom, Is.LessThanOrEqualTo(0.01f), "the art sits wholly above the slot");
    }

    [TestCase(0f, 1f)]
    [TestCase(0f, -1f)]
    [TestCase(0.5f, 0f)]
    [TestCase(-0.75f, 0.25f)]
    public void ArtIsInsideItsSlotForAnyOffset(float x, float y)
    {
        var offset = new Vector2(x, y);
        var art = ArtInSlot(AtlasBounds.Unrotated(OneTile, offset), OneTile, offset, out var slot);
        Assert.That(Inside(art, slot), Is.True, $"offset {offset}: art {art} slot {slot}");
    }

    [TestCase(0)]
    [TestCase(90)]
    [TestCase(180)]
    [TestCase(270)]
    [TestCase(30)]
    public void AFlatSpriteIsInsideItsSlotAtAnyRotation(int degrees)
    {
        var offset = new Vector2(0f, 1f);
        var rot = Angle.FromDegrees(degrees);
        var slotBounds = AtlasBounds.Rotated(OneTile, offset, rot);

        // the art as drawn: the sprite is turned by rot about the entity origin, then the (turned) offset is added
        var turned = new Box2Rotated(OneTile, rot, Vector2.Zero).CalcBoundingBox().Translated(rot.RotateVec(offset));
        var atlas = new BillboardAtlas(1024);
        atlas.BeginFrame();
        Assert.That(atlas.TryAllocate(slotBounds, out var slot, out var origin), Is.True);
        var px = BillboardAtlas.PixelsPerTile;
        var art = new UIBox2(origin.X + turned.Left * px, origin.Y - turned.Top * px, origin.X + turned.Right * px, origin.Y - turned.Bottom * px);
        Assert.That(Inside(art, slot), Is.True, $"rotation {degrees}: art {art} slot {slot}");
    }

    [Test]
    public void OrdinarySpritesFitASlot()
    {
        Assert.That(BillboardAtlas.Fits(OneTile), Is.True);
        Assert.That(BillboardAtlas.Fits(new Box2(-3f, -3f, 3f, 3f)), Is.True);
        Assert.That(BillboardAtlas.Fits(AtlasBounds.Unrotated(OneTile, new Vector2(0f, 1f))), Is.True);
    }

    [Test]
    public void ArtBiggerThanASlotIsLeftOutInsteadOfSpillingOntoItsNeighbours()
    {
        // eight tiles is 256 pixels, the most a slot can hold
        Assert.That(BillboardAtlas.Fits(new Box2(-4f, -4f, 4f, 4f)), Is.True);
        Assert.That(BillboardAtlas.Fits(new Box2(-4.1f, -4f, 4.1f, 4f)), Is.False);
        Assert.That(BillboardAtlas.Fits(new Box2(-1f, -5f, 1f, 5f)), Is.False);
    }
}
