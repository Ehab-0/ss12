using System.Numerics;

namespace Content.Shared.Render3D;

/// <summary>
///     Pure math for the 3D camera yaw and how it maps onto <c>InputMoverComponent.RelativeRotation</c>.
///     Conventions (assumption A6, pinned by tests in Content.Tests/Shared/Render3D):
///     <list type="bullet">
///     <item><c>Angle.ToWorldVec()</c> treats angle zero as SOUTH (0,-1) (that is the "facing the viewer" sprite
///     direction); positive angles are counter-clockwise, so 90 degrees is East (1,0).</item>
///     <item><c>Vector2.RotateVec</c> (what the mover uses) rotates counter-clockwise, so rotating "up" (0,1) by
///     theta gives (-sin theta, cos theta): zero is NORTH there.</item>
///     <item><c>yaw</c> is defined as the mover's total rotation <c>gridWorldRotation + RelativeRotation</c>: the
///     camera looks along <c>yaw.RotateVec((0,1))</c> (yaw 0 = looking north, +90 degrees = looking west) and
///     "up" input walks that way.</item>
///     <item>The eye rotation the engine derives is <c>-yaw</c> (<c>EyeLerpingSystem.GetRotation</c>).</item>
///     </list>
/// </summary>
public static class CameraYawMath
{
    /// <summary>The relative rotation a mover on a grid rotated by <paramref name="gridWorldRotation"/> needs so that "up" walks along <paramref name="yaw"/>.</summary>
    public static Angle YawToRelativeRotation(Angle yaw, Angle gridWorldRotation)
    {
        return (yaw - gridWorldRotation).Reduced();
    }

    /// <summary>Inverse of <see cref="YawToRelativeRotation"/>.</summary>
    public static Angle RelativeRotationToYaw(Angle relativeRotation, Angle gridWorldRotation)
    {
        return (relativeRotation + gridWorldRotation).Reduced();
    }

    /// <summary>World direction (unit vector) the camera looks along on the ground plane.</summary>
    public static Vector2 Forward(Angle yaw) => yaw.RotateVec(new Vector2(0, 1));

    /// <summary>World direction of the camera's right-hand side on the ground plane.</summary>
    public static Vector2 Right(Angle yaw)
    {
        return yaw.RotateVec(new Vector2(1, 0));
    }

    /// <summary>The world direction a held "up/down/left/right" input (as a screen-space vector, +Y = up) walks in.</summary>
    public static Vector2 InputToWorld(Vector2 input, Angle gridWorldRotation, Angle relativeRotation)
    {
        return (gridWorldRotation + relativeRotation).RotateVec(input);
    }

    /// <summary>
    ///     The yaw (see class remarks) of the ray from the camera towards an entity: how a 2D camera would have to be
    ///     rotated to have the entity straight "up" on screen.
    /// </summary>
    public static Angle ViewAngle(Vector2 cameraPos, Vector2 entityPos)
    {
        var delta = entityPos - cameraPos;
        if (delta.LengthSquared() < 1e-8f)
            return Angle.Zero;
        return new Angle(Math.Atan2(-delta.X, delta.Y));
    }

    /// <summary>
    ///     Picks the cardinal sprite direction to display for an entity rotated by <paramref name="entityWorldRotation"/>
    ///     seen by a camera looking from <paramref name="cameraPos"/> to <paramref name="entityPos"/>.
    ///     With a standard SS14 sprite (South = facing the viewer on screen), an entity facing away from the camera
    ///     shows its North sprite; facing the camera shows South; facing the viewer's right shows East.
    /// </summary>
    public static Direction SpriteDirection(Angle entityWorldRotation, Vector2 cameraPos, Vector2 entityPos)
    {
        // In the 2D renderer the on-screen angle of an entity is (entityRotation + eyeRotation) and the RSI
        // direction is picked from it (angle zero = South = facing the viewer). For a 3D camera the "eye rotation"
        // for a given entity is the one that turns the camera->entity ray into "up on screen": -viewAngle.
        var viewAngle = ViewAngle(cameraPos, entityPos);
        return (entityWorldRotation - viewAngle).GetDir();
    }
}
