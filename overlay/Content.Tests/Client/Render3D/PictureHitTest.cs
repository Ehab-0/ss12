using System.Numerics;
using Vector3 = System.Numerics.Vector3;
using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>Where on a quad's picture a ray hit lands, so a see-through pixel does not catch a click meant for what is under it.</summary>
[TestFixture]
public sealed class PictureHitTest
{
    [Test]
    public void ARayGivesWhereOnTheQuadItHit()
    {
        // a quad of 2 x 2 tiles lying flat, top left corner at (-1, 1)
        var p0 = new Vector3(-1f, 1f, 0f);
        var p1 = new Vector3(1f, 1f, 0f);
        var p3 = new Vector3(-1f, -1f, 0f);

        Assert.That(Picking3D.RayQuad(new Vector3(0.5f, -0.5f, 5f), new Vector3(0f, 0f, -1f), p0, p1, p3, 1f, out var t, out var s, out var u), Is.True);
        Assert.That(t, Is.EqualTo(5f).Within(1e-4f));
        Assert.That(s, Is.EqualTo(0.75f).Within(1e-4f));
        Assert.That(u, Is.EqualTo(0.75f).Within(1e-4f));

        // the short form agrees
        Assert.That(Picking3D.RayQuad(new Vector3(0.5f, -0.5f, 5f), new Vector3(0f, 0f, -1f), p0, p1, p3, 1f, out var t2), Is.True);
        Assert.That(t2, Is.EqualTo(t));
    }

    [Test]
    public void TheOffsetOfAHitPixelIsMeasuredFromWhereTheEntitySitsInThePicture()
    {
        var quad = new EntityPass.PickQuad
        {
            // a 100 pixel atlas; the quad shows the pixels (10, 10) to (30, 30)
            Uv0 = new Vector2(0.10f, 0.90f),
            Uv1 = new Vector2(0.30f, 0.90f),
            Uv3 = new Vector2(0.10f, 0.70f),
            AtlasSize = 100f,
            OriginPx = new Vector2(20f, 20f),
        };

        // the middle of the quad is the pixel the entity sits on
        var middle = EntityPass.PictureOffset(quad, 0.5f, 0.5f);
        Assert.That(middle.X, Is.EqualTo(0f).Within(1e-4f));
        Assert.That(middle.Y, Is.EqualTo(0f).Within(1e-4f));

        // the top right corner is 10 pixels (a third of a tile) to the right and above
        var corner = EntityPass.PictureOffset(quad, 1f, 0f);
        Assert.That(corner.X, Is.EqualTo(10f / 32f).Within(1e-4f));
        Assert.That(corner.Y, Is.EqualTo(10f / 32f).Within(1e-4f));

        // the bottom left corner is below and to the left
        var low = EntityPass.PictureOffset(quad, 0f, 1f);
        Assert.That(low.X, Is.EqualTo(-10f / 32f).Within(1e-4f));
        Assert.That(low.Y, Is.EqualTo(-10f / 32f).Within(1e-4f));
    }
}
