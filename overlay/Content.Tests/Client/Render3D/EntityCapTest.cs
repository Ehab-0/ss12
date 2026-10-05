using System.Collections.Generic;
using System.Linq;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class EntityCapTest
{
    private static EntityDraw3D[] Entities(params float[] distances)
    {
        var list = new EntityDraw3D[distances.Length];
        for (var i = 0; i < distances.Length; i++)
        {
            list[i] = default;
            list[i].Uid = new EntityUid(i + 1);
            list[i].Distance2 = distances[i] * distances[i];
        }

        return list;
    }

    private static HashSet<int> Kept(EntityDraw3D[] draws, int count) => draws.Take(count).Select(d => d.Uid.Id).ToHashSet();

    [Test]
    public void KeepsTheNearestOnesWhenOverTheCap()
    {
        var draws = Entities(9, 1, 5, 3, 7, 2); // ids 1..6
        var count = EntityPass.ApplyCap(draws, draws.Length, 3, new HashSet<EntityUid>());

        Assert.That(count, Is.EqualTo(3));
        Assert.That(Kept(draws, count), Is.EquivalentTo(new[] { 2, 6, 4 })); // distances 1, 2, 3
    }

    [Test]
    public void LeavesEverythingAloneUnderTheCap()
    {
        var draws = Entities(9, 1, 5);
        var count = EntityPass.ApplyCap(draws, draws.Length, 10, new HashSet<EntityUid>());

        Assert.That(count, Is.EqualTo(3));
        Assert.That(draws.Select(d => d.Uid.Id), Is.EqualTo(new[] { 1, 2, 3 }), "no reordering when nothing is cut");
    }

    /// <summary>
    ///     Two entities at nearly the same distance, one drawn last frame: as the camera moves a little their distances swap
    ///     places around the cutoff. The one that was drawn must stay drawn instead of the two trading places every frame.
    /// </summary>
    [Test]
    public void AnEntityAtTheCutKeepsItsPlaceWhileTheDistancesJitter()
    {
        var wasDrawn = new HashSet<EntityUid>();

        // 10 entities, room for 5; the 5th and 6th (ids 5 and 6) are almost the same distance from the camera
        var first = Entities(1, 2, 3, 4, 10.0f, 10.4f, 11, 12, 13, 14);
        var count = EntityPass.ApplyCap(first, first.Length, 5, wasDrawn);
        Assert.That(Kept(first, count), Does.Contain(5), "the nearer of the two is drawn first");
        Assert.That(Kept(first, count), Does.Not.Contain(6));

        // the camera moves: now id 6 is slightly nearer than id 5
        var second = Entities(1, 2, 3, 4, 10.2f, 10.0f, 11, 12, 13, 14);
        count = EntityPass.ApplyCap(second, second.Length, 5, wasDrawn);
        Assert.That(Kept(second, count), Does.Contain(5), "it was drawn last frame, so it stays");
        Assert.That(Kept(second, count), Does.Not.Contain(6));
    }

    [Test]
    public void AnEntityClearlyFartherThanTheCutIsStillDropped()
    {
        var wasDrawn = new HashSet<EntityUid>();
        var first = Entities(1, 2, 3, 4, 10.0f, 20f);
        EntityPass.ApplyCap(first, first.Length, 5, wasDrawn);

        // id 5 was drawn but is now far away, while id 6 came much closer: the head start does not outweigh that
        var second = Entities(1, 2, 3, 4, 20f, 10.0f);
        var count = EntityPass.ApplyCap(second, second.Length, 5, wasDrawn);
        Assert.That(Kept(second, count), Does.Contain(6));
        Assert.That(Kept(second, count), Does.Not.Contain(5));
    }
}
