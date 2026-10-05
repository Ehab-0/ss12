using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class WallDecalSideTest
{
    [Test]
    public void AWallBehindIsTheUsualCase()
    {
        Assert.That(WallDecalSide.Choose(true, false, false), Is.EqualTo(DecalSide.Behind));
        Assert.That(WallDecalSide.Choose(true, false, true), Is.EqualTo(DecalSide.Behind), "behind wins over ahead");
    }

    [Test]
    public void AnEntityInsideAWallTileHangsOnThatTile()
    {
        Assert.That(WallDecalSide.Choose(false, true, false), Is.EqualTo(DecalSide.Here));
        Assert.That(WallDecalSide.Choose(true, true, true), Is.EqualTo(DecalSide.Here));
    }

    [Test]
    public void ACameraPointingAtItsWallHangsOnTheWallAhead()
    {
        Assert.That(WallDecalSide.Choose(false, false, true), Is.EqualTo(DecalSide.Ahead));
    }

    [Test]
    public void NoWallAtAllIsNone()
    {
        Assert.That(WallDecalSide.Choose(false, false, false), Is.EqualTo(DecalSide.None));
    }
}
