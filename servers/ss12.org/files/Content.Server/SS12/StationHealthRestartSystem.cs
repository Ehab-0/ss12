using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.SS12;

/// <summary>
///     ss12.org: restarts the round when the station has lost its lights for good (nobody is keeping the power going), so the next
///     player is not left in the dark. Off unless <c>ss12.station_health_restart</c> is set.
/// </summary>
/// <remarks>
///     Ten seconds into a round it counts the station's lights that can be on (those not switched off by hand) and how many have power;
///     that is what this station looks like when it is healthy. Every 30 seconds after that, once the round is a few minutes old and
///     somebody is connected, it counts again. If fewer than <c>ss12.station_health_min_lit</c> of the starting number have power for
///     <c>ss12.station_health_bad_seconds</c> in a row, it announces a restart, checks again after the warning, and ends the round.
///     Judging against the station's own start means a map that begins with many dark rooms is not mistaken for a dead one, and it never
///     acts in the first minutes of a round, so a mistake in the count cannot make rounds restart one after another.
/// </remarks>
public sealed partial class StationHealthRestartSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ServerGameTicker _ticker = default!;

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BaselineAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BaselineRetry = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EndToRestart = TimeSpan.FromSeconds(10);

    /// <summary>Fewer lights than this and the count means nothing (a tiny or unusual map).</summary>
    private const int MinLights = 10;

    private ISawmill _sawmill = default!;
    private TimeSpan _nextCheck;
    private TimeSpan _nextBaselineTry;
    private TimeSpan? _badSince;
    private int? _baselineLit;
    private bool _restarting;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("ss12.health");
        _sawmill.Info($"Station health watch: {(_cfg.GetCVar(SS12ServerCVars.StationHealthRestart) ? "on" : "off")}.");
        SubscribeLocalEvent<GameRunLevelChangedEvent>(_ =>
        {
            _badSince = null;
            _baselineLit = null;
            _restarting = false;
            _nextCheck = TimeSpan.Zero;
            _nextBaselineTry = TimeSpan.Zero;
        });
    }

    public override void Update(float frameTime)
    {
        if (_restarting || !_cfg.GetCVar(SS12ServerCVars.StationHealthRestart) || _ticker.RunLevel != GameRunLevel.InRound)
            return;

        var now = _timing.CurTime;
        var age = now - _ticker.RoundStartTimeSpan;

        // What a healthy station looks like, taken early, with or without players (an empty round is held still soon after).
        if (_baselineLit == null)
        {
            if (age >= BaselineAfter && now >= _nextBaselineTry)
            {
                _nextBaselineTry = now + BaselineRetry;
                if (TryMeasure(out var lit0, out var total0))
                {
                    _baselineLit = lit0;
                    _sawmill.Info($"Station lights at the start of the round: {lit0} of {total0} have power.");
                }
            }

            return;
        }

        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        if ((_cfg.GetCVar(SS12ServerCVars.StationHealthNeedPlayers) && _players.PlayerCount == 0) ||
            age < TimeSpan.FromSeconds(_cfg.GetCVar(SS12ServerCVars.StationHealthMinRoundSeconds)))
        {
            _badSince = null;
            return;
        }

        if (!TryMeasure(out var lit, out var total))
        {
            _badSince = null;
            return;
        }

        _sawmill.Debug($"Station lights with power: {lit} of {total} (at the start: {_baselineLit}).");

        if (IsHealthy(lit))
        {
            if (_badSince != null)
                _sawmill.Info($"The station lights are back: {lit} of {total} have power.");

            _badSince = null;
            return;
        }

        if (_badSince == null)
        {
            _badSince = now;
            _sawmill.Info($"Few station lights have power: {lit} of {total} (at the start: {_baselineLit}). Restarting the round if it stays like this.");
        }

        if (now - _badSince.Value < TimeSpan.FromSeconds(_cfg.GetCVar(SS12ServerCVars.StationHealthBadSeconds)))
            return;

        StartRestart();
    }

    private bool IsHealthy(int lit)
    {
        return lit >= _baselineLit!.Value * _cfg.GetCVar(SS12ServerCVars.StationHealthMinLit);
    }

    /// <summary>Counts the lights on the station's grids that are not switched off, and how many of them have power.</summary>
    private bool TryMeasure(out int lit, out int total)
    {
        lit = 0;
        total = 0;

        var grids = new HashSet<EntityUid>();
        var stations = EntityQueryEnumerator<StationDataComponent>();
        while (stations.MoveNext(out _, out var data))
        {
            foreach (var grid in data.Grids)
            {
                grids.Add(grid);
            }
        }

        if (grids.Count == 0)
            return false;

        var lights = EntityQueryEnumerator<PoweredLightComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (lights.MoveNext(out _, out _, out var receiver, out var xform))
        {
            if (xform.GridUid is not { } gridUid || !grids.Contains(gridUid) || receiver.PowerDisabled)
                continue;

            total++;
            if (receiver.Powered)
                lit++;
        }

        return total >= MinLights;
    }

    private void StartRestart()
    {
        _restarting = true;
        var warn = TimeSpan.FromSeconds(_cfg.GetCVar(SS12ServerCVars.StationHealthWarnSeconds));
        _sawmill.Info("The station has been without power for a while: restarting the round.");
        _chat.DispatchServerAnnouncement($"The station has lost its power. The round restarts in {(int) warn.TotalSeconds} seconds.");

        Timer.Spawn(warn, () =>
        {
            if (_ticker.RunLevel != GameRunLevel.InRound)
                return;

            // The lights may have come back during the warning.
            if (TryMeasure(out var lit, out var total) && IsHealthy(lit))
            {
                _sawmill.Info($"The station lights came back during the warning ({lit} of {total}): not restarting.");
                _chat.DispatchServerAnnouncement("The power is back. The round goes on.");
                _badSince = null;
                _restarting = false;
                return;
            }

            _ticker.EndRound("The station lost its power. A new round starts shortly.");
            Timer.Spawn(EndToRestart, () => _ticker.RestartRound());
        });
    }
}
