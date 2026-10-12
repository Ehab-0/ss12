using System.Numerics;
using Robust.Client.GameObjects;

namespace Content.Client.Render3D;

// Engines 292 and newer (installed by the SS12 installer instead of Content.Client/Render3D/Engine/Render3DEngine.Drawn.cs).
// From 292 the transform holds the simulated position and the client keeps the lerped one apart (the "render transform");
// the 2D view draws sprites there and the eye follows it, so the 3D view must too, or moving things step at the tick rate.
public static partial class Render3DEngine
{
    /// <summary>
    ///     Where an entity is drawn: its world position and rotation as the 2D view shows them this frame. Up to engine 291 the
    ///     client lerps the transform itself, so that is the transform.
    /// </summary>
    public static (Vector2 Position, Angle Rotation) DrawnWorldPositionRotation(SharedTransformSystem xforms, EntityUid uid, TransformComponent xform)
    {
        return ((TransformSystem) xforms).GetRenderWorldPositionRotation((uid, xform));
    }

    public static Vector2 DrawnWorldPosition(SharedTransformSystem xforms, EntityUid uid)
    {
        return ((TransformSystem) xforms).GetRenderWorldPosition(uid);
    }
}
