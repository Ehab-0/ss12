using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class AdaptiveLayersTest
{
    private const float Pixel = 0.0074f; // a screen pixel at about four tiles with a 90 degree field of view on 1080 lines

    [Test]
    public void ASlabSeenAtAnAngleGetsMoreLayersThanOneSeenHeadOn()
    {
        var headOn = EntityShape.AdaptiveLayers(0.14f, 4f, 1f, Pixel, 40);
        var oblique = EntityShape.AdaptiveLayers(0.14f, 4f, 0.5f, Pixel, 40);
        Assert.That(oblique, Is.GreaterThan(headOn));
    }

    [Test]
    public void ASlabFurtherAwayNeedsFewerLayers()
    {
        var near = EntityShape.AdaptiveLayers(0.14f, 4f, 0.6f, Pixel, 40);
        var far = EntityShape.AdaptiveLayers(0.14f, 12f, 0.6f, Pixel * 3f, 40);
        Assert.That(far, Is.LessThan(near));
    }

    [Test]
    public void TheBudgetIsNeverExceeded()
    {
        Assert.That(EntityShape.AdaptiveLayers(0.14f, 4f, 0.25f, Pixel, 10), Is.EqualTo(10));
        Assert.That(EntityShape.AdaptiveLayers(0.14f, 4f, 0.25f, Pixel, 0), Is.EqualTo(0));
    }

    [Test]
    public void ANoThicknessGivesNoLayersAndAThickOneAtLeastTwo()
    {
        Assert.That(EntityShape.AdaptiveLayers(0f, 4f, 1f, Pixel, 24), Is.EqualTo(0));
        Assert.That(EntityShape.AdaptiveLayers(0.14f, 30f, 1f, 0.05f, 24), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void TheGapBetweenNeighbouringLayersStaysWithinAPixel()
    {
        const float thickness = 0.14f;
        const float cosView = 0.5f;
        var layers = EntityShape.AdaptiveLayers(thickness, 4f, cosView, Pixel, 40);
        var tan = System.MathF.Sqrt(1f - cosView * cosView) / cosView;
        Assert.That(thickness / layers * tan, Is.LessThanOrEqualTo(Pixel * 1.001f));
    }
}
