using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server.SS12;

/// <summary>
///     ss12.org settings, in their own file with their own [CVarDefs] as the notice in CCVars.cs asks forks to do.
/// </summary>
[CVarDefs]
public sealed class SS12ServerCVars : CVars
{
    /// <summary>
    ///     After a restart with nobody connected the round is loaded by itself and then held still (the engine's pause for an empty server)
    ///     until a player connects. Off: the empty round keeps running instead.
    /// </summary>
    public static readonly CVarDef<bool> WarmStartFreeze =
        CVarDef.Create("ss12.warm_start_freeze", true, CVar.SERVERONLY);

    /// <summary>
    ///     When the last player leaves a round that has been going for <see cref="FreshRoundMinSeconds"/>, start a new round straight away
    ///     (it loads and is then held still), so the next player gets a fresh station and not whatever was done to the old one.
    /// </summary>
    public static readonly CVarDef<bool> FreshRoundWhenEmpty =
        CVarDef.Create("ss12.fresh_round_when_empty", false, CVar.SERVERONLY);

    /// <summary>
    ///     How long a round must have been played (the empty server is paused, so this is time with players) before its last player
    ///     leaving starts a new round. A shorter round is left as it is for the next player.
    /// </summary>
    public static readonly CVarDef<int> FreshRoundMinSeconds =
        CVarDef.Create("ss12.fresh_round_min_seconds", 1200, CVar.SERVERONLY);

    /// <summary>Restart the round when the station has lost its lights for good (see <see cref="StationHealthRestartSystem"/>).</summary>
    public static readonly CVarDef<bool> StationHealthRestart =
        CVarDef.Create("ss12.station_health_restart", false, CVar.SERVERONLY);

    /// <summary>
    ///     The share of the lights that had power when the round started that must still have power; below it the station counts as dark.
    /// </summary>
    public static readonly CVarDef<float> StationHealthMinLit =
        CVarDef.Create("ss12.station_health_min_lit", 0.2f, CVar.SERVERONLY);

    /// <summary>How long the station must stay dark before the round is restarted.</summary>
    public static readonly CVarDef<int> StationHealthBadSeconds =
        CVarDef.Create("ss12.station_health_bad_seconds", 180, CVar.SERVERONLY);

    /// <summary>The warning before the round ends.</summary>
    public static readonly CVarDef<int> StationHealthWarnSeconds =
        CVarDef.Create("ss12.station_health_warn_seconds", 60, CVar.SERVERONLY);

    /// <summary>A round younger than this is never restarted.</summary>
    public static readonly CVarDef<int> StationHealthMinRoundSeconds =
        CVarDef.Create("ss12.station_health_min_round_seconds", 300, CVar.SERVERONLY);

    /// <summary>Only look while somebody is connected (an empty server is paused anyway).</summary>
    public static readonly CVarDef<bool> StationHealthNeedPlayers =
        CVarDef.Create("ss12.station_health_need_players", true, CVar.SERVERONLY);
}
