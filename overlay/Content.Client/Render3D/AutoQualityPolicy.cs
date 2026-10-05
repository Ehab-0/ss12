namespace Content.Client.Render3D;

/// <summary>The decisions of the automatic quality that do not need the game running, so they can be tested.</summary>
public static class AutoQualityPolicy
{
    /// <summary>
    ///     Whether joining a round puts the graphics back on the highest preset: automatic quality is on and the lowering that
    ///     was saved came from it, not from the player. A player who chose a lower preset (or any effect) themselves keeps it.
    /// </summary>
    public static bool ShouldRestoreOnJoin(bool autoQualityOn, bool lastLoweringWasAutomatic)
    {
        return autoQualityOn && lastLoweringWasAutomatic;
    }

    /// <summary>
    ///     Whether the player is told at the start of a round that the frame rate is being measured: only when automatic quality
    ///     is on and the preset is the highest one, the only state it can lower from by itself.
    /// </summary>
    public static bool ShouldAnnounceMeasuring(bool autoQualityOn, int level, int highest)
    {
        return autoQualityOn && level == highest;
    }
}
