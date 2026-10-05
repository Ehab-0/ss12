using System.Collections.Generic;
using System.Linq;
using Content.Client.Render3D;
using Content.Shared.Render3D;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class GlassLookTest
{
    private static List<(UIBox2 Rect, GlassPart Part)> Layout(Render3DGlassRule rule, int size = 32)
    {
        var parts = new List<(UIBox2, GlassPart)>();
        new GlassLook(rule).Layout(size, size, parts);
        return parts;
    }

    private static bool Covers(IEnumerable<(UIBox2 Rect, GlassPart Part)> parts, GlassPart kind, int x, int y)
    {
        return parts.Any(p => p.Part == kind && p.Rect.Left <= x && x < p.Rect.Right && p.Rect.Top <= y && y < p.Rect.Bottom);
    }

    [Test]
    public void ThePaneFillsTheWholeSlotAndComesFirst()
    {
        var parts = Layout(new Render3DGlassRule());
        Assert.That(parts[0].Part, Is.EqualTo(GlassPart.Pane));
        Assert.That(parts[0].Rect, Is.EqualTo(new UIBox2(0, 0, 32, 32)));
    }

    [Test]
    public void TheFrameCoversEveryEdgePixelAndNothingInside()
    {
        var parts = Layout(new Render3DGlassRule { FrameWidth = 2 });
        for (var i = 0; i < 32; i++)
        {
            Assert.That(Covers(parts, GlassPart.Frame, i, 0), Is.True, $"top {i}");
            Assert.That(Covers(parts, GlassPart.Frame, i, 31), Is.True, $"bottom {i}");
            Assert.That(Covers(parts, GlassPart.Frame, 0, i), Is.True, $"left {i}");
            Assert.That(Covers(parts, GlassPart.Frame, 31, i), Is.True, $"right {i}");
        }

        Assert.That(Covers(parts, GlassPart.Frame, 1, 10), Is.True);
        Assert.That(Covers(parts, GlassPart.Frame, 2, 10), Is.False);
        Assert.That(Covers(parts, GlassPart.Frame, 16, 16), Is.False);
    }

    [Test]
    public void AFrameWidthOfZeroHasNoFrame()
    {
        Assert.That(Layout(new Render3DGlassRule { FrameWidth = 0 }).Any(p => p.Part == GlassPart.Frame), Is.False);
    }

    [Test]
    public void TheFrameIsDrawnOnTopOfEverythingElse()
    {
        var parts = Layout(new Render3DGlassRule { FrameWidth = 1, Mesh = 4 });
        var firstFrame = parts.FindIndex(p => p.Part == GlassPart.Frame);
        Assert.That(parts.FindLastIndex(p => p.Part != GlassPart.Frame), Is.LessThan(firstFrame));
    }

    [Test]
    public void AMeshHasLinesEveryNPixelsAndNoShine()
    {
        var parts = Layout(new Render3DGlassRule { Mesh = 6, Shine = true });
        Assert.That(Covers(parts, GlassPart.Mesh, 6, 10), Is.True);
        Assert.That(Covers(parts, GlassPart.Mesh, 12, 10), Is.True);
        Assert.That(Covers(parts, GlassPart.Mesh, 10, 18), Is.True);
        Assert.That(Covers(parts, GlassPart.Mesh, 7, 7), Is.False);
        Assert.That(parts.Any(p => p.Part == GlassPart.Shine), Is.False, "a mesh does not get a highlight");
    }

    [Test]
    public void AClearPaneGetsAHighlightInsideTheFrame()
    {
        var parts = Layout(new Render3DGlassRule { FrameWidth = 2, Shine = true });
        var shine = parts.Where(p => p.Part == GlassPart.Shine).ToList();
        Assert.That(shine, Is.Not.Empty);
        foreach (var (rect, _) in shine)
        {
            Assert.That(rect.Left, Is.GreaterThanOrEqualTo(2));
            Assert.That(rect.Top, Is.GreaterThanOrEqualTo(2));
            Assert.That(rect.Right, Is.LessThanOrEqualTo(30));
            Assert.That(rect.Bottom, Is.LessThanOrEqualTo(30));
        }
    }

    [Test]
    public void ShineCanBeSwitchedOff()
    {
        Assert.That(Layout(new Render3DGlassRule { Shine = false }).Any(p => p.Part == GlassPart.Shine), Is.False);
    }

    [Test]
    public void NothingLiesOutsideTheSlot()
    {
        foreach (var size in new[] { 8, 16, 32, 48 })
        {
            foreach (var (rect, _) in Layout(new Render3DGlassRule { FrameWidth = 3, Mesh = 5 }, size))
            {
                Assert.That(rect.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(rect.Top, Is.GreaterThanOrEqualTo(0));
                Assert.That(rect.Right, Is.LessThanOrEqualTo(size));
                Assert.That(rect.Bottom, Is.LessThanOrEqualTo(size));
            }
        }
    }

    [Test]
    public void OutOfRangeValuesAreClamped()
    {
        var look = new GlassLook(new Render3DGlassRule { Alpha = 3f, FrameWidth = 99, Mesh = 1 });
        Assert.That(look.Alpha, Is.EqualTo(1f));
        Assert.That(look.FrameWidth, Is.EqualTo(6));
        Assert.That(look.Mesh, Is.EqualTo(2));
        Assert.That(new GlassLook(new Render3DGlassRule { Alpha = -1f, FrameWidth = -4 }).FrameWidth, Is.EqualTo(0));
    }
}
