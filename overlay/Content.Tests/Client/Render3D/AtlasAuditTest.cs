using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class AtlasAuditTest
{
    private static readonly Box2 OneTile = new(-0.5f, -0.5f, 0.5f, 0.5f);

    [Test]
    public void TheSlotIsCentredOnTheOriginOfItsCell()
    {
        var slot = AtlasAudit.SlotInCell(OneTile, 0, 0, 32, 32);
        Assert.That(slot, Is.EqualTo(UIBox2i.FromDimensions(128, 128, 32, 32)));

        var next = AtlasAudit.SlotInCell(OneTile, 2, 1, 32, 32);
        Assert.That(next.Left, Is.EqualTo(2 * AtlasAudit.CellPixels + 128));
        Assert.That(next.Top, Is.EqualTo(AtlasAudit.CellPixels + 128));
    }

    [Test]
    public void ABoundsAboveTheOriginPutsTheSlotHigherInTheCell()
    {
        // a lamp: one tile up, so its slot's top is 1.5 tiles above the origin (screen y grows downwards)
        var slot = AtlasAudit.SlotInCell(new Box2(-0.5f, 0.5f, 0.5f, 1.5f), 0, 0, 32, 32);
        Assert.That(slot.Top, Is.EqualTo(AtlasAudit.CellPixels / 2 - 48));
    }

    [Test]
    public void ATargetHoldsWholeCellsOnly()
    {
        Assert.That(AtlasAudit.CellsPerRow(4096), Is.EqualTo(4096 / AtlasAudit.CellPixels));
        Assert.That(AtlasAudit.CellsPerRow(100), Is.EqualTo(1));
    }
}
