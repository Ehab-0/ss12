using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

/// <summary>
///     The overhead camera sees the tops of walls: a ray that starts above the walls and comes down onto a wall block hits its top.
///     Rays that start at or below the wall height must behave exactly as they always did.
/// </summary>
[TestFixture]
public sealed class OverheadWallTopTest
{
    private const float WallHeight = 1.1f;

    // a single pillar at (3, 1); everything else is open
    private static bool Pillar(Vector2i c) => c is { X: 3, Y: 1 };

    [Test]
    public void ARayComingDownOntoAWallHitsItsTop()
    {
        // 5 high, falling 2.5 per unit of travel along +x: reaches the wall height at t = 1.56, which is x = 3.06, inside the pillar's tile
        Assert.That(TileWorldSystem.Dda(Pillar, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 5f, -2.5f, 100f, WallHeight,
            out var t, out var normal, out var tile, out var top), Is.True);
        Assert.That(top, Is.True);
        Assert.That(t, Is.EqualTo(1.56f).Within(1e-3f));
        Assert.That(tile, Is.EqualTo(new Vector2i(3, 1)));
        Assert.That(normal, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void ARayStartingOverAWallHitsItsTop()
    {
        // the camera is straight above the pillar (and falls to the wall height before it leaves the tile)
        Assert.That(TileWorldSystem.Dda(Pillar, new Vector2(3.5f, 1.5f), new Vector2(0.5f, 0), 3f, -3f, 100f, WallHeight,
            out var t, out _, out var tile, out var top), Is.True);
        Assert.That(top, Is.True);
        Assert.That(t, Is.EqualTo(0.6333f).Within(1e-3f));
        Assert.That(tile, Is.EqualTo(new Vector2i(3, 1)));
    }

    [Test]
    public void ARayThatFallsTooSlowlyPassesOverTheWall()
    {
        Assert.That(TileWorldSystem.Dda(Pillar, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 5f, -0.1f, 100f, WallHeight,
            out _, out _, out _, out var top), Is.False);
        Assert.That(top, Is.False);
    }

    [Test]
    public void ARayThatReachesTheFloorBeforeTheWallHitsTheSideNotTheTop()
    {
        // above the wall height when it enters the pillar's tile it would be a top hit; this one is already below it
        Assert.That(TileWorldSystem.Dda(Pillar, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 3f, -1.5f, 100f, WallHeight,
            out var t, out var normal, out var tile, out var top), Is.True);
        Assert.That(top, Is.False);
        Assert.That(t, Is.EqualTo(1.5f).Within(1e-4f));
        Assert.That(normal, Is.EqualTo(new Vector2(-1, 0)));
        Assert.That(tile, Is.EqualTo(new Vector2i(3, 1)));
    }

    [Test]
    public void ARayThatStopsBeforeTheWallTopDoesNotHitIt()
    {
        // would reach the top at t = 1.56, but the ray is only 1.2 long
        Assert.That(TileWorldSystem.Dda(Pillar, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 5f, -2.5f, 1.2f, WallHeight,
            out _, out _, out _, out _), Is.False);
    }

    [Test]
    public void RaysThatStartLowNeverHitATop()
    {
        // level and rising rays at or below the wall height: the old behaviour (a side hit), and never a top
        foreach (var dz in new[] { 0f, 0.1f, -0.5f })
        {
            var hit = TileWorldSystem.Dda(Pillar, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 0.9f, dz, 100f, WallHeight,
                out _, out _, out _, out var top);
            Assert.That(top, Is.False, $"dz {dz}");
            Assert.That(hit, Is.True, $"dz {dz} should hit the side of the pillar");
        }

        // the same call without the top flag gives the same answer
        Assert.That(TileWorldSystem.Dda(Pillar, new Vector2(1.5f, 1.5f), new Vector2(1, 0), 0.9f, 0f, 100f, WallHeight,
            out var t, out _, out _), Is.True);
        Assert.That(t, Is.EqualTo(1.5f).Within(1e-4f));
    }
}
