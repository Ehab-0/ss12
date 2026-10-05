using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class EntityShapeNearCameraTest
{
    [Test]
    public void ThingsRightAtTheCameraGetNoLayers()
    {
        Assert.That(EntityShape.TooCloseForLayers(0f), Is.True);
        Assert.That(EntityShape.TooCloseForLayers(0.5f * 0.5f), Is.True);
    }

    [Test]
    public void ThingsAtAnOrdinaryDistanceKeepTheirLayers()
    {
        Assert.That(EntityShape.TooCloseForLayers(2f * 2f), Is.False);
        Assert.That(EntityShape.TooCloseForLayers(EntityShape.NearLayerDistance * EntityShape.NearLayerDistance), Is.False);
    }
}
