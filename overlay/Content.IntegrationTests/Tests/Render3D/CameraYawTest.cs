using System.Numerics;
using Content.Client.Render3D;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Render3D;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The one networked part of the 3D view: the camera yaw event that makes WASD camera-relative
///     (docs/ss12/PLAN.md, Phase 3 and section 9).
/// </summary>
public sealed class CameraYawTest : MovementTest
{
    protected override int Tiles => 6;

    private async Task SubmitYaw(Angle yaw, NetEntity? user = null)
    {
        var uid = user == null ? null : (EntityUid?) CEntMan.GetEntity(user.Value);
        await Client.WaitPost(() => CEntMan.System<CameraYawSystem>().SubmitYaw(yaw, uid));
        await RunTicks(5);
    }

    [Test]
    public async Task YawEventSetsMoverRotationAndUpWalksAlongIt()
    {
        var mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
        Assert.That(mover.RelativeRotation.Theta, Is.EqualTo(0).Within(1e-6), "mover should start unrotated");

        await SubmitYaw(Angle.FromDegrees(90));

        mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
        Assert.That(mover.RelativeRotation.Degrees, Is.EqualTo(90).Within(0.01), "server mover rotation");
        Assert.That(mover.TargetRelativeRotation.Degrees, Is.EqualTo(90).Within(0.01), "target rotation is written too");

        // Looking west (yaw +90): "up" must walk towards -X, not +Y.
        var start = Transform.GetWorldPosition(SPlayer);
        await Move(DirectionFlag.North, 0.6f);
        var delta = Transform.GetWorldPosition(SPlayer) - start;
        Assert.That(delta.X, Is.LessThan(-0.15f), $"walked {delta}");
        Assert.That(MathF.Abs(delta.Y), Is.LessThan(0.1f), $"walked {delta}");
    }

    [Test]
    public async Task YawEventForAnEntityTheSenderDoesNotControlIsRejected()
    {
        var wall = WallLeft!.Value;
        await SubmitYaw(Angle.FromDegrees(45), wall);

        var mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
        Assert.That(mover.RelativeRotation.Theta, Is.EqualTo(0).Within(1e-6), "player must not have been rotated");
        Assert.That(SEntMan.HasComponent<InputMoverComponent>(SEntMan.GetEntity(wall)), Is.False);
    }

    [Test]
    public async Task NonFiniteYawIsIgnored()
    {
        await SubmitYaw(Angle.FromDegrees(30));

        var accepted = true;
        await Server.WaitPost(() => accepted = SEntMan.System<Content.Server.Render3D.CameraYawSystem>()
            .SetYaw(SPlayer, new Angle(double.NaN)));

        Assert.That(accepted, Is.False);
        var mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
        Assert.That(mover.RelativeRotation.Degrees, Is.EqualTo(30).Within(0.01));
    }

    [Test]
    public async Task EnforcedCVarReplicatesToTheClient()
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        var ccfg = Client.ResolveDependency<IConfigurationManager>();

        await Server.WaitPost(() => cfg.SetCVar(CCVars.Render3DEnforced, true));
        await RunTicks(5);
        Assert.That(ccfg.GetCVar(CCVars.Render3DEnforced), Is.True, "render3d.enforced is replicated");

        await Server.WaitPost(() => cfg.SetCVar(CCVars.Render3DEnforced, false));
        await RunTicks(5);
        Assert.That(ccfg.GetCVar(CCVars.Render3DEnforced), Is.False);
    }

    [Test]
    public void EnforcementRuleBlocksTheTwoDViewForLivingPlayersOnly()
    {
        // enforced = true: living, player-controlled mobs are locked to 3D
        Assert.That(Render3DController.CanUse2D(true, hasBody: true, isGhost: false, isActiveAdmin: false), Is.False);
        // ghosts, admins and the lobby (no body) may switch
        Assert.That(Render3DController.CanUse2D(true, hasBody: true, isGhost: true, isActiveAdmin: false), Is.True);
        Assert.That(Render3DController.CanUse2D(true, hasBody: true, isGhost: false, isActiveAdmin: true), Is.True);
        Assert.That(Render3DController.CanUse2D(true, hasBody: false, isGhost: false, isActiveAdmin: false), Is.True);
        // not enforced: everyone may
        Assert.That(Render3DController.CanUse2D(false, hasBody: true, isGhost: false, isActiveAdmin: false), Is.True);
    }
}
