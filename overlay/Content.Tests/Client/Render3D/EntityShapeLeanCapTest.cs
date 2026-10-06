using System;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class EntityShapeLeanCapTest
{
    [Test]
    public void ABigSpriteTiltsLessSoItsFarEdgeStaysLow()
    {
        var lean = EntityShape.CapFlatLean(EntityShape.MaxFlatLean, 1f, 0.1f);
        Assert.That(lean, Is.LessThan(EntityShape.MaxFlatLean));
        Assert.That(1f * MathF.Sin(lean), Is.EqualTo(0.1f).Within(1e-4));
    }

    [Test]
    public void ASmallSpriteKeepsTheWholeTilt()
    {
        Assert.That(EntityShape.CapFlatLean(EntityShape.MaxFlatLean, 0.2f, 0.1f), Is.EqualTo(EntityShape.MaxFlatLean).Within(1e-6));
    }

    [Test]
    public void ZeroOrNegativeMeansNoLimit()
    {
        Assert.That(EntityShape.CapFlatLean(0.3f, 1f, 0f), Is.EqualTo(0.3f));
        Assert.That(EntityShape.CapFlatLean(0.3f, 1f, -1f), Is.EqualTo(0.3f));
    }

    [Test]
    public void ItNeverRaisesTheTilt()
    {
        Assert.That(EntityShape.CapFlatLean(0.05f, 1f, 0.5f), Is.EqualTo(0.05f).Within(1e-6));
        Assert.That(EntityShape.CapFlatLean(0.3f, 0f, 0.1f), Is.EqualTo(0.3f));
    }
}
