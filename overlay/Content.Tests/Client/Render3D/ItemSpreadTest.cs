using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class ItemSpreadTest
{
    private static Vector2[] Run(Vector2[] positions, int[] keys)
    {
        var offsets = new Vector2[positions.Length];
        ItemSpread.Compute(positions, keys, offsets);
        var result = new Vector2[positions.Length];
        for (var i = 0; i < positions.Length; i++)
            result[i] = positions[i] + offsets[i];

        return result;
    }

    [Test]
    public void ALoneItemStaysWhereItIs()
    {
        var offsets = new Vector2[1];
        ItemSpread.Compute(new[] { new Vector2(3.5f, 4.5f) }, new[] { 7 }, offsets);
        Assert.That(offsets[0], Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void ItemsThatAreFarEnoughApartAreNotMoved()
    {
        var offsets = new Vector2[2];
        ItemSpread.Compute(new[] { new Vector2(3.2f, 4.5f), new Vector2(3.8f, 4.5f) }, new[] { 1, 2 }, offsets);
        Assert.That(offsets[0], Is.EqualTo(Vector2.Zero));
        Assert.That(offsets[1], Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void ItemsOnTheSameSpotAreSpreadApart()
    {
        var spot = new Vector2(3.5f, 4.5f);
        var result = Run(new[] { spot, spot, spot, spot }, new[] { 11, 12, 13, 14 });
        for (var i = 0; i < result.Length; i++)
        {
            for (var j = i + 1; j < result.Length; j++)
                Assert.That(Vector2.Distance(result[i], result[j]), Is.GreaterThanOrEqualTo(ItemSpread.MinSeparation - 1e-4f), $"{i} and {j}");
        }
    }

    [Test]
    public void NoItemLeavesItsTile()
    {
        var spot = new Vector2(3.95f, 4.02f); // close to a corner
        var positions = new Vector2[6];
        var keys = new int[6];
        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] = spot;
            keys[i] = 100 + i;
        }

        foreach (var p in Run(positions, keys))
        {
            Assert.That(p.X, Is.InRange(3f + ItemSpread.EdgeMargin - 1e-4f, 4f - ItemSpread.EdgeMargin + 1e-4f).Or.EqualTo(spot.X).Within(1e-4f));
            Assert.That(p.Y, Is.InRange(4f + ItemSpread.EdgeMargin - 1e-4f, 5f - ItemSpread.EdgeMargin + 1e-4f).Or.EqualTo(spot.Y).Within(1e-4f));
        }
    }

    [Test]
    public void TheSameInputAlwaysGivesTheSameResult()
    {
        var spot = new Vector2(8.5f, 2.5f);
        var positions = new[] { spot, spot, spot };
        var keys = new[] { 5, 9, 2 };
        var a = Run(positions, keys);
        var b = Run(positions, keys);
        for (var i = 0; i < a.Length; i++)
            Assert.That(a[i], Is.EqualTo(b[i]));
    }

    [Test]
    public void TheOrderOfTheListDoesNotChangeWhereAnItemEndsUp()
    {
        var spot = new Vector2(8.5f, 2.5f);
        var first = Run(new[] { spot, spot, spot }, new[] { 5, 9, 2 });
        var second = Run(new[] { spot, spot, spot }, new[] { 2, 5, 9 });
        // item with key 9 is the third in the second list and the second in the first
        Assert.That(first[1], Is.EqualTo(second[2]));
        Assert.That(first[0], Is.EqualTo(second[1]));
        Assert.That(first[2], Is.EqualTo(second[0]));
    }
}
