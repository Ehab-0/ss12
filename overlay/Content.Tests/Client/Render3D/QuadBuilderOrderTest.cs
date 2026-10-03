using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class QuadBuilderOrderTest
{
    private static Camera3D MakeCamera()
    {
        var cam = new Camera3D
        {
            Yaw = 0,
            Pitch = 0,
            Position = new Vector3(0, -3, 0.8f),
            Size = new Vector2(1280, 720),
        };
        cam.UpdateBasis();
        return cam;
    }

    /// <summary>The marker (uv x of the first corner, times depth) of every quad in the order the builder emitted them.</summary>
    private static int[] EmitOrder(QuadBuilder builder, Camera3D cam)
    {
        builder.Build(cam, out var vertices, out _);
        var span = vertices.Span;
        var order = new int[span.Length / 4];
        for (var i = 0; i < order.Length; i++)
        {
            // UV = uv / w and UV2.x = 1 / w, so uv = UV / UV2.x
            var v = span[i * 4];
            order[i] = (int) MathF.Round(v.UV.X / v.UV2.X);
        }

        return order;
    }

    [Test]
    public void QuadsAtTheSameDepthKeepTheOrderTheyWereAddedIn()
    {
        var cam = MakeCamera();
        var builder = new QuadBuilder();
        const int count = 200;
        for (var i = 0; i < count; i++)
        {
            // the same plane for every quad (a window and the grille in its tile), only the marker uv differs
            builder.Add(new Vector3(-0.5f, 0, 1f), new Vector3(0.5f, 0, 1f), new Vector3(0.5f, 0, 0f), new Vector3(-0.5f, 0, 0f),
                new Vector2(i, 0), new Vector2(i, 0), new Vector2(i, 0), new Vector2(i, 0), Vector2.Zero);
        }

        var order = EmitOrder(builder, cam);
        Assert.That(order, Has.Length.EqualTo(count));
        for (var i = 0; i < count; i++)
            Assert.That(order[i], Is.EqualTo(i), "same depth must keep insertion order");
    }

    [Test]
    public void SortBiasDecidesBetweenQuadsOnTheSamePlane()
    {
        var cam = MakeCamera();
        var builder = new QuadBuilder();

        // added first but with the bigger bias (= further away): must be emitted first (painter's order, far first)
        builder.Add(new Vector3(-0.5f, 0, 1f), new Vector3(0.5f, 0, 1f), new Vector3(0.5f, 0, 0f), new Vector3(-0.5f, 0, 0f),
            new Vector2(7, 0), new Vector2(7, 0), new Vector2(7, 0), new Vector2(7, 0), Vector2.Zero, sortBias: 0.001f);
        builder.Add(new Vector3(-0.5f, 0, 1f), new Vector3(0.5f, 0, 1f), new Vector3(0.5f, 0, 0f), new Vector3(-0.5f, 0, 0f),
            new Vector2(3, 0), new Vector2(3, 0), new Vector2(3, 0), new Vector2(3, 0), Vector2.Zero, sortBias: -0.001f);

        Assert.That(EmitOrder(builder, cam), Is.EqualTo(new[] { 7, 3 }));
    }

    [Test]
    public void FartherQuadsAreEmittedBeforeNearerOnes()
    {
        var cam = MakeCamera();
        var builder = new QuadBuilder();
        builder.Add(new Vector3(-0.5f, 0, 1f), new Vector3(0.5f, 0, 1f), new Vector3(0.5f, 0, 0f), new Vector3(-0.5f, 0, 0f),
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), Vector2.Zero);
        builder.Add(new Vector3(-0.5f, 4, 1f), new Vector3(0.5f, 4, 1f), new Vector3(0.5f, 4, 0f), new Vector3(-0.5f, 4, 0f),
            new Vector2(2, 0), new Vector2(2, 0), new Vector2(2, 0), new Vector2(2, 0), Vector2.Zero);

        Assert.That(EmitOrder(builder, cam), Is.EqualTo(new[] { 2, 1 }));
    }
}
