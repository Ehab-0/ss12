using Content.Shared.Movement.Components;
using Content.Shared.Render3D;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>
///     Sends the 3D camera yaw to the server (predicted) so that WASD is camera-relative. The 3D camera is the
///     authority for yaw: whenever the yaw implied by the mover (grid snapping, other systems rotating the camera,
///     a state reset) drifts from it, we re-assert it.
/// </summary>
public sealed partial class CameraYawSystem : SharedCameraYawSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Angle SendThreshold = Angle.FromDegrees(1);
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1.0 / 30.0);

    private TimeSpan _lastSent;

    /// <summary>The yaw the 3D camera wants (set by the controller each frame while 3D is active).</summary>
    public Angle? DesiredYaw;

    /// <summary>
    ///     The yaw implied by the local entity's mover right now, or null if there isn't one.
    /// </summary>
    public Angle? CurrentYaw()
    {
        if (_player.LocalEntity is not { } uid || !HasComp<InputMoverComponent>(uid))
            return null;
        return GetYaw(uid);
    }

    /// <summary>Sends a yaw request for the local entity (or <paramref name="user"/>) right now, ignoring the throttle.</summary>
    public void SubmitYaw(Angle yaw, EntityUid? user = null)
    {
        user ??= _player.LocalEntity;
        if (user == null)
            return;

        RaisePredictiveEvent(new RequestCameraYawEvent
        {
            Yaw = yaw,
            User = GetNetEntity(user.Value),
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted || DesiredYaw is not { } desired)
            return;

        if (_player.LocalEntity is not { } uid || !TryComp<InputMoverComponent>(uid, out var mover))
            return;

        var implied = GetYaw(uid, mover);
        if (Math.Abs(Angle.ShortestDistance(desired, implied).Theta) <= SendThreshold.Theta)
            return;

        var now = _timing.RealTime;
        if (now - _lastSent < MinInterval)
            return;

        _lastSent = now;
        RaisePredictiveEvent(new RequestCameraYawEvent
        {
            Yaw = desired,
            User = GetNetEntity(uid),
        });
    }
}
