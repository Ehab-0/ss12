using Content.Client.Render3D;
using NUnit.Framework;

namespace Content.Tests.Client.Render3D;

[TestFixture]
public sealed class MouseCapturePolicyTest
{
    private static MouseCaptureReason Decide(bool shown = true, bool focused = true, bool freeKey = false, bool uiFocus = false, bool windowOpen = false, bool player = true)
        => MouseCapturePolicy.Decide(shown, focused, freeKey, uiFocus, windowOpen, player);

    [Test]
    public void CapturesWhenNothingGetsInTheWay()
    {
        Assert.That(Decide(), Is.EqualTo(MouseCaptureReason.Capture));
    }

    [Test]
    public void EachBlockerIsNamed()
    {
        Assert.That(Decide(shown: false), Is.EqualTo(MouseCaptureReason.NotShown));
        Assert.That(Decide(focused: false), Is.EqualTo(MouseCaptureReason.Unfocused));
        Assert.That(Decide(freeKey: true), Is.EqualTo(MouseCaptureReason.FreeKey));
        Assert.That(Decide(uiFocus: true), Is.EqualTo(MouseCaptureReason.UiFocus));
        Assert.That(Decide(windowOpen: true), Is.EqualTo(MouseCaptureReason.WindowOpen));
        Assert.That(Decide(player: false), Is.EqualTo(MouseCaptureReason.NoPlayer));
    }

    [Test]
    public void AnUnfocusedWindowNeverCapturesWhateverElseIsTrue()
    {
        Assert.That(Decide(focused: false, freeKey: false, uiFocus: false, windowOpen: false), Is.Not.EqualTo(MouseCaptureReason.Capture));
    }

    [Test]
    public void TheRequestIsRepeatedOftenRightAfterTheFocusComesBackAndRarelyLater()
    {
        Assert.That(MouseCapturePolicy.ReassertInterval(0f), Is.LessThan(MouseCapturePolicy.ReassertInterval(10f)));
        Assert.That(MouseCapturePolicy.ReassertInterval(MouseCapturePolicy.ReassertAfterFocus + 0.01f), Is.EqualTo(1f));
    }
}
