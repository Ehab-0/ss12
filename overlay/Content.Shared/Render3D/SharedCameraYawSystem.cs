using Content.Shared.Movement.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Render3D;

/// <summary>
///     Raised (predicted) by the 3D client whenever the camera yaw changed. <see cref="Yaw"/> is the world angle of
///     the camera's forward direction (see <see cref="CameraYawMath"/>).
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestCameraYawEvent : EntityEventArgs
{
    public Angle Yaw;
    public NetEntity? User;
}

/// <summary>
///     Makes WASD camera-relative in 3D by writing the camera yaw into the mover's relative rotation. Everything
///     else (eye rotation, audio listener orientation, the 2D fallback view) follows from that automatically.
/// </summary>
public abstract partial class SharedCameraYawSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<InputMoverComponent> _moverQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _moverQuery = GetEntityQuery<InputMoverComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeAllEvent<RequestCameraYawEvent>(OnRequestYaw);
    }

    private void OnRequestYaw(RequestCameraYawEvent msg, EntitySessionEventArgs args)
    {
        // Only the entity the sender is currently controlling may be turned.
        if (args.SenderSession.AttachedEntity is not { } ent || ent != GetEntity(msg.User))
            return;

        if (!double.IsFinite(msg.Yaw.Theta))
            return;

        // Deliberately NOT checked against shuttle.camera_rotation_locked: that cvar only disables the 90 degree rotate
        // keys, and the preset sets it. Only a 3D client sends this event, and without it the camera turns while WASD
        // keeps walking along the grid axes.
        SetYaw(ent, msg.Yaw);
    }

    /// <summary>
    ///     Points the mover's "up" input along <paramref name="yaw"/> (a world angle).
    /// </summary>
    public bool SetYaw(EntityUid uid, Angle yaw)
    {
        if (!double.IsFinite(yaw.Theta) || !_moverQuery.TryComp(uid, out var mover))
            return false;

        var gridRot = Angle.Zero;
        if (_xformQuery.TryComp(mover.RelativeEntity, out var relativeXform))
            gridRot = _transform.GetWorldRotation(relativeXform);

        var relative = CameraYawMath.YawToRelativeRotation(yaw, gridRot);

        // Write both so SharedMoverController.LerpRotation doesn't smooth the mouse-look.
        if (mover.RelativeRotation.Equals(relative) && mover.TargetRelativeRotation.Equals(relative))
            return true;

        mover.RelativeRotation = relative;
        mover.TargetRelativeRotation = relative;
        Dirty(uid, mover);
        return true;
    }

    /// <summary>
    ///     The camera yaw implied by the mover's current relative rotation (the inverse of <see cref="SetYaw"/>).
    /// </summary>
    public Angle GetYaw(EntityUid uid, InputMoverComponent? mover = null)
    {
        if (!Resolve(uid, ref mover, false))
            return Angle.Zero;

        var gridRot = Angle.Zero;
        if (_xformQuery.TryComp(mover.RelativeEntity, out var relativeXform))
            gridRot = _transform.GetWorldRotation(relativeXform);

        return CameraYawMath.RelativeRotationToYaw(mover.RelativeRotation, gridRot);
    }
}
