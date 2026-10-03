using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.Render3D;

/// <summary>
///     In 3D the character must always face the crosshair (like a shooter), not only in combat mode. Upstream adds
///     <see cref="MouseRotatorComponent"/> + <see cref="NoRotateOnMoveComponent"/> when combat mode is toggled on and
///     removes them again when it's toggled off, so while <see cref="CCVars.Render3DEnforced"/> is on we make sure
///     player-controlled mobs keep them.
/// </summary>
public sealed partial class Render3DFacingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;

    private bool _enforced;
    private TimeSpan _nextCheck;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.Render3DEnforced, v => _enforced = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_enforced || _timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            // Only mobs that can aim: those that have combat mode (humanoids, borgs, etc.) and a mover.
            if (!HasComp<CombatModeComponent>(ent) || !HasComp<InputMoverComponent>(ent))
                continue;

            if (!HasComp<MouseRotatorComponent>(ent))
                EnsureComp<MouseRotatorComponent>(ent);

            if (!HasComp<NoRotateOnMoveComponent>(ent))
                EnsureComp<NoRotateOnMoveComponent>(ent);
        }
    }
}
