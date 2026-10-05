using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class MinimapViewTest
{
    private static readonly Vector2 Centre = new(100f, 100f);

    private static Vector2 Map(Vector2 point, Vector2 player, Vector2 up, float scale = 4f)
    {
        return Vector2.Transform(point, MinimapView.Matrix(player, up, Centre, scale));
    }

    [Test]
    public void ThePlayerIsInTheCentre()
    {
        var p = Map(new Vector2(37f, -12f), new Vector2(37f, -12f), Vector2.UnitY);
        Assert.That(p.X, Is.EqualTo(100f).Within(1e-4));
        Assert.That(p.Y, Is.EqualTo(100f).Within(1e-4));
    }

    [Test]
    public void NorthUpPutsNorthAboveAndEastToTheRight()
    {
        var player = new Vector2(10f, 10f);
        var north = Map(player + new Vector2(0f, 3f), player, Vector2.UnitY);
        Assert.That(north.X, Is.EqualTo(100f).Within(1e-4));
        Assert.That(north.Y, Is.EqualTo(88f).Within(1e-4), "three tiles north is 12 pixels up the screen");

        var east = Map(player + new Vector2(2f, 0f), player, Vector2.UnitY);
        Assert.That(east.X, Is.EqualTo(108f).Within(1e-4));
        Assert.That(east.Y, Is.EqualTo(100f).Within(1e-4));
    }

    [Test]
    public void WhateverIsAheadIsAboveTheCentre()
    {
        // facing east: a tile to the east is straight up, a tile to the south is to the right
        var player = new Vector2(5f, 5f);
        var ahead = Map(player + new Vector2(2f, 0f), player, Vector2.UnitX);
        Assert.That(ahead.X, Is.EqualTo(100f).Within(1e-4));
        Assert.That(ahead.Y, Is.EqualTo(92f).Within(1e-4));

        var south = Map(player + new Vector2(0f, -2f), player, Vector2.UnitX);
        Assert.That(south.X, Is.EqualTo(108f).Within(1e-4), "facing east, south is on the right hand");
        Assert.That(south.Y, Is.EqualTo(100f).Within(1e-4));
    }

    [Test]
    public void TheScaleIsPixelsPerTile()
    {
        var a = Map(new Vector2(0f, 0f), Vector2.Zero, Vector2.UnitY, 5f);
        var b = Map(new Vector2(1f, 0f), Vector2.Zero, Vector2.UnitY, 5f);
        Assert.That(b.X - a.X, Is.EqualTo(5f).Within(1e-4));
    }

    [Test]
    public void TheFacingDirectionIsUpWhenTheMapTurnsWithTheCamera()
    {
        var facing = Vector2.Normalize(new Vector2(-1f, 1f));
        var onScreen = MinimapView.ScreenDirection(facing, facing);
        Assert.That(onScreen.X, Is.EqualTo(0f).Within(1e-4));
        Assert.That(onScreen.Y, Is.EqualTo(-1f).Within(1e-4));
    }

    [Test]
    public void NorthIsAtTheTopWhenTheMapDoesNotTurn()
    {
        var onScreen = MinimapView.ScreenDirection(Vector2.UnitY, Vector2.UnitY);
        Assert.That(onScreen.X, Is.EqualTo(0f).Within(1e-4));
        Assert.That(onScreen.Y, Is.EqualTo(-1f).Within(1e-4));
    }

    [Test]
    public void TheEdgeOfTheMapIsTheRadius()
    {
        Assert.That(MinimapView.PixelsPerTile(200f, 20f), Is.EqualTo(5f).Within(1e-4));
        Assert.That(MinimapView.PixelsPerTile(200f, 0f), Is.EqualTo(100f).Within(1e-4), "a radius under one tile is treated as one");
    }

    [Test]
    public void FarAwayChunksAreSkipped()
    {
        Assert.That(MinimapView.InRange(10f, 8f, 20f), Is.True);
        Assert.That(MinimapView.InRange(60f, 8f, 20f), Is.False);
    }

    [Test]
    public void TheSmallMapKeepsItsConfiguredSideInTheReferenceWindow()
    {
        Assert.That(MinimapView.SmallSide(200f, MinimapView.ReferenceHeight), Is.EqualTo(200f).Within(1e-3));
    }

    [Test]
    public void TheSmallMapShrinksInASmallWindowAndGrowsInABigOne()
    {
        var small = MinimapView.SmallSide(200f, 480f);
        var big = MinimapView.SmallSide(200f, 1440f);
        Assert.That(small, Is.LessThan(200f));
        Assert.That(big, Is.GreaterThan(200f));
        Assert.That(small, Is.LessThanOrEqualTo(480f * 0.38f + 1e-3f));
    }

    [Test]
    public void TheSmallMapNeverTakesMoreThanAThirdOfATinyWindow()
    {
        Assert.That(MinimapView.SmallSide(360f, 300f), Is.LessThanOrEqualTo(300f * 0.38f + 1e-3f).Or.EqualTo(80f));
        Assert.That(MinimapView.SmallSide(120f, 100f), Is.GreaterThanOrEqualTo(80f));
    }
}
