using System.Numerics;
using Content.Client.Render3D;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Render3D;

/// <summary>
///     The preset the installer writes sets <c>shuttle.camera_rotation_locked = true</c> (the 90 degree rotate keys make
///     no sense in 3D) and, without <c>--enforce</c>, <c>render3d.enforced = false</c>. Mouse-look must still turn
///     WASD in that configuration: the lock is about the rotate keys, not about the 3D camera. It used to drop every yaw
///     request, so the camera turned but W kept walking along the grid axes ("sometimes inverted, sometimes not").
/// </summary>
public sealed class CameraYawLockedCameraTest : MovementTest
{
    protected override int Tiles => 6;

    [Test]
    public async Task MouseLookStillTurnsWasdWhenTheShuttleCameraIsLockedAndTheViewIsNotEnforced()
    {
        await Server.WaitPost(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            cfg.SetCVar(CCVars.CameraRotationLocked, true);
            cfg.SetCVar(CCVars.Render3DEnforced, false);
        });
        await RunTicks(5);

        await Client.WaitPost(() => CEntMan.System<CameraYawSystem>().SubmitYaw(Angle.FromDegrees(90)));
        await RunTicks(5);

        var mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
        Assert.That(mover.RelativeRotation.Degrees, Is.EqualTo(90).Within(0.01), "server mover rotation");

        // Looking west (yaw +90): "up" must walk towards -X, not +Y.
        var start = Transform.GetWorldPosition(SPlayer);
        await Move(DirectionFlag.North, 0.6f);
        var delta = Transform.GetWorldPosition(SPlayer) - start;
        Assert.That(delta.X, Is.LessThan(-0.15f), $"walked {delta}");
        Assert.That(MathF.Abs(delta.Y), Is.LessThan(0.1f), $"walked {delta}");
    }
}
