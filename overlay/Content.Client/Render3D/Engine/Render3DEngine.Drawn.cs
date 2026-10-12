using System.Numerics;

namespace Content.Client.Render3D;

// Engines up to 291. The version for newer engines is Tools/ss12-engine/from-292/Content.Client/Render3D/Engine/Render3DEngine.Drawn.cs.
public static partial class Render3DEngine
{
    /// <summary>
    ///     Where an entity is drawn: its world position and rotation as the 2D view shows them this frame. Up to engine 291 the
    ///     client lerps the transform itself, so that is the transform.
    /// </summary>
    public static (Vector2 Position, Angle Rotation) DrawnWorldPositionRotation(SharedTransformSystem xforms, EntityUid uid, TransformComponent xform)
    {
        return xforms.GetWorldPositionRotation(xform);
    }

    public static Vector2 DrawnWorldPosition(SharedTransformSystem xforms, EntityUid uid)
    {
        return xforms.GetWorldPosition(uid);
    }
}
