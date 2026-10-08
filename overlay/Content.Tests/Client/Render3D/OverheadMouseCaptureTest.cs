using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

/// <summary>The overhead view never captures the mouse: the cursor is free and points at the world.</summary>
[TestFixture]
public sealed class OverheadMouseCaptureTest
{
    private static MouseCaptureReason Decide(bool cursorMode, bool shown = true, bool focused = true, bool freeKey = false,
        bool uiFocus = false, bool windowOpen = false, bool player = true)
        => MouseCapturePolicy.Decide(shown, focused, freeKey, uiFocus, windowOpen, player, cursorMode);

    [Test]
    public void TheCursorStaysFreeWhereTheOtherCamerasWouldCapture()
    {
        Assert.That(Decide(cursorMode: false), Is.EqualTo(MouseCaptureReason.Capture));
        Assert.That(Decide(cursorMode: true), Is.EqualTo(MouseCaptureReason.CursorMode));
    }

    [Test]
    public void NotShownAndUnfocusedStillComeFirst()
    {
        Assert.That(Decide(cursorMode: true, shown: false), Is.EqualTo(MouseCaptureReason.NotShown));
        Assert.That(Decide(cursorMode: true, focused: false), Is.EqualTo(MouseCaptureReason.Unfocused));
    }

    [Test]
    public void ItIsNeverCapturedWhateverElseIsGoingOn()
    {
        foreach (var freeKey in new[] { false, true })
        foreach (var uiFocus in new[] { false, true })
        foreach (var windowOpen in new[] { false, true })
        foreach (var player in new[] { false, true })
        {
            Assert.That(Decide(true, freeKey: freeKey, uiFocus: uiFocus, windowOpen: windowOpen, player: player),
                Is.Not.EqualTo(MouseCaptureReason.Capture));
        }
    }
}
