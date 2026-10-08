using System;
using System.Numerics;
using Content.Client.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

/// <summary>
///     With the overhead camera quads are ordered by their place on the ground: the height of a flat quad counts for far more when the
///     camera looks down steeply, and would put an item lying on a locker behind the locker's own card.
/// </summary>
[TestFixture]
public sealed class OverheadQuadOrderTest
{
    private static Camera3D MakeCamera(CameraMode mode)
    {
        var cam = new Camera3D
        {
            Mode = mode,
            Yaw = 0,
            Pitch = -MathHelper.DegreesToRadians(55f),
            FovY = MathHelper.DegreesToRadians(45f),
            Size = new Vector2(1280, 720),
        };
        cam.UpdateBasis();
        cam.Position = OverheadCamera.Position(Vector2.Zero, cam.Forward, 9f);
        return cam;
    }

    private static int[] EmitOrder(QuadBuilder builder, Camera3D cam)
    {
        builder.Build(cam, out var vertices, out _);
        var span = vertices.Span;
        var order = new int[span.Length / 4];
        for (var i = 0; i < order.Length; i++)
        {
            var v = span[i * 4];
            order[i] = (int) MathF.Round(v.UV.X / v.UV2.X);
        }

        return order;
    }

    /// <summary>A standing card facing the camera, centred on (x, y) and <paramref name="height"/> tall.</summary>
    private static void AddCard(QuadBuilder builder, float x, float y, float height, int marker)
    {
        var uv = new Vector2(marker, 0);
        builder.Add(new Vector3(x - 0.5f, y, height), new Vector3(x + 0.5f, y, height), new Vector3(x + 0.5f, y, 0f), new Vector3(x - 0.5f, y, 0f),
            uv, uv, uv, uv, Vector2.Zero);
    }

    /// <summary>A flat card lying at height <paramref name="z"/>.</summary>
    private static void AddFlat(QuadBuilder builder, float x, float y, float z, int marker, float bias = 0f)
    {
        var uv = new Vector2(marker, 0);
        builder.Add(new Vector3(x - 0.3f, y + 0.3f, z), new Vector3(x + 0.3f, y + 0.3f, z), new Vector3(x + 0.3f, y - 0.3f, z), new Vector3(x - 0.3f, y - 0.3f, z),
            uv, uv, uv, uv, Vector2.Zero, sortBias: bias);
    }

    [Test]
    public void AnItemOnALockerIsDrawnAfterTheLocker()
    {
        var cam = MakeCamera(CameraMode.Overhead);
        var builder = new QuadBuilder();
        AddCard(builder, 0, 0, 1.1f, 1);
        AddFlat(builder, 0, 0, 1.1f, 2, bias: -0.3f); // the sort bias EntityPass gives an item that rests on a surface

        Assert.That(EmitOrder(builder, cam), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void TheOtherCamerasStillOrderAFlatQuadByTheDepthOfItsCentre()
    {
        // the same two quads seen by the third-person camera pose: the item is much higher than the camera's ground-plane
        // reference, so it sorts behind the card (this is the old behaviour, which the overhead rule must not change)
        var cam = MakeCamera(CameraMode.ThirdPerson);
        var builder = new QuadBuilder();
        AddCard(builder, 0, 0, 1.1f, 1);
        AddFlat(builder, 0, 0, 1.1f, 2, bias: -0.3f);

        Assert.That(EmitOrder(builder, cam), Is.EqualTo(new[] { 2, 1 }));
    }

    [Test]
    public void AnItemInFrontOfACardIsDrawnAfterItAndOneBehindItBefore()
    {
        var cam = MakeCamera(CameraMode.Overhead);

        var inFront = new QuadBuilder();
        AddCard(inFront, 0, 0, 1f, 1);
        AddFlat(inFront, 0, -0.5f, 0.01f, 2); // south: nearer to a camera that looks north
        Assert.That(EmitOrder(inFront, cam), Is.EqualTo(new[] { 1, 2 }));

        var behind = new QuadBuilder();
        AddCard(behind, 0, 0, 1f, 1);
        AddFlat(behind, 0, 0.5f, 0.01f, 2);
        Assert.That(EmitOrder(behind, cam), Is.EqualTo(new[] { 2, 1 }));
    }

    [Test]
    public void QuadsOnTheSameSpotKeepTheOrderTheyWereAddedIn()
    {
        // a door's top is added after its panel, and must stay on top of it
        var cam = MakeCamera(CameraMode.Overhead);
        var builder = new QuadBuilder();
        AddCard(builder, 0, 0, 1.1f, 1);
        AddFlat(builder, 0, 0, 1.1f, 2);

        Assert.That(EmitOrder(builder, cam), Is.EqualTo(new[] { 1, 2 }));
    }
}
