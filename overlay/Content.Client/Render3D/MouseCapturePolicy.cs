namespace Content.Client.Render3D;

/// <summary>Why the mouse is, or is not, captured for mouse-look.</summary>
public enum MouseCaptureReason : byte
{
    Capture = 0,
    NotShown,
    Unfocused,
    FreeKey,
    UiFocus,
    WindowOpen,
    NoPlayer,
}

/// <summary>
///     The rule for capturing the mouse (the cursor hidden and the camera following it), kept free of engine types so it can be
///     unit tested. The first reason that applies wins; <see cref="MouseCaptureReason.Capture"/> means the mouse is wanted.
/// </summary>
public static class MouseCapturePolicy
{
    public static MouseCaptureReason Decide(bool shown, bool windowFocused, bool freeKeyHeld, bool uiKeyboardFocused, bool windowOpen, bool hasPlayer)
    {
        if (!shown)
            return MouseCaptureReason.NotShown;
        if (!windowFocused)
            return MouseCaptureReason.Unfocused;
        if (freeKeyHeld)
            return MouseCaptureReason.FreeKey;
        if (uiKeyboardFocused)
            return MouseCaptureReason.UiFocus;
        if (windowOpen)
            return MouseCaptureReason.WindowOpen;
        if (!hasPlayer)
            return MouseCaptureReason.NoPlayer;

        return MouseCaptureReason.Capture;
    }

    /// <summary>How long (seconds) after the window gets focus the request for the relative mouse mode is repeated.</summary>
    public const float ReassertAfterFocus = 1.5f;

    /// <summary>
    ///     Seconds between repeats of the request while the mouse should be captured. The window system cannot be asked whether
    ///     it took the request, and it can ignore one that arrives in the instant a window gets focus, so a request that failed
    ///     was never repeated and the cursor stayed free until the next alt+tab. Repeating an accepted request changes nothing.
    /// </summary>
    public static float ReassertInterval(float secondsSinceFocus) => secondsSinceFocus < ReassertAfterFocus ? 0.1f : 1.0f;
}
