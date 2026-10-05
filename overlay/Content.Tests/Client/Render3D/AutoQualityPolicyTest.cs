using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class AutoQualityPolicyTest
{
    [Test]
    public void AnAutomaticLoweringIsUndoneOnJoin()
    {
        Assert.That(AutoQualityPolicy.ShouldRestoreOnJoin(true, true), Is.True);
    }

    [Test]
    public void APlayersOwnChoiceIsKept()
    {
        Assert.That(AutoQualityPolicy.ShouldRestoreOnJoin(true, false), Is.False);
    }

    [Test]
    public void NothingIsRestoredWhenAutomaticQualityIsOff()
    {
        Assert.That(AutoQualityPolicy.ShouldRestoreOnJoin(false, true), Is.False);
        Assert.That(AutoQualityPolicy.ShouldRestoreOnJoin(false, false), Is.False);
    }

    [Test]
    public void MeasuringIsAnnouncedOnlyOnTheHighestPresetWithAutomaticQualityOn()
    {
        Assert.That(AutoQualityPolicy.ShouldAnnounceMeasuring(true, 2, 2), Is.True);
        Assert.That(AutoQualityPolicy.ShouldAnnounceMeasuring(true, 1, 2), Is.False);
        Assert.That(AutoQualityPolicy.ShouldAnnounceMeasuring(true, 3, 2), Is.False, "custom settings are never lowered");
        Assert.That(AutoQualityPolicy.ShouldAnnounceMeasuring(false, 2, 2), Is.False);
    }
}
