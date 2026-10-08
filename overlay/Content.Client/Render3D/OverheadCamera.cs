using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>
///     The geometry of the overhead camera (<see cref="CameraMode.Overhead"/>), kept free of engine state so it can be unit
///     tested: which way it looks and where it hangs.
/// </summary>
public static class OverheadCamera
{
    /// <summary>Height of the point the camera looks at, above the floor under the character (about the middle of the body).</summary>
    public const float TargetHeight = 0.5f;

    public const float MinPitchDegrees = 15f;
    public const float MaxPitchDegrees = 85f;

    /// <summary>The extra turn of the isometric view from the grid's axes (radians): a diagonal.</summary>
    public const double IsometricTurn = Math.PI / 4;
    public const float MinDistance = 4f;
    public const float MaxDistance = 16f;

    /// <summary>
    ///     The camera yaw (see <see cref="Camera3D.Yaw"/>): the world rotation of the grid the character stands on, turned by
    ///     <paramref name="quarterTurns"/> quarter turns. Equal to the mover's <c>grid rotation + RelativeRotation</c> when its
    ///     relative rotation is that many quarter turns, so "up" walks towards the top of the screen.
    /// </summary>
    public static double Yaw(Angle gridRotation, int quarterTurns)
        => (gridRotation + quarterTurns * (Math.PI / 2)).Reduced().Theta;

    /// <summary>The isometric yaw: <see cref="Yaw"/> turned by a diagonal, so the grid's axes run along the screen's diagonals.</summary>
    public static double IsometricYaw(Angle gridRotation, int quarterTurns)
        => (gridRotation + quarterTurns * (Math.PI / 2) + IsometricTurn).Reduced().Theta;

    /// <summary>The next number of quarter turns after pressing the turn key (<paramref name="delta"/> 0 resets to north up).</summary>
    public static int Turn(int quarterTurns, int delta)
        => delta == 0 ? 0 : ((quarterTurns + delta) % 4 + 4) % 4;

    /// <summary>Where the camera hangs: <paramref name="distance"/> back along the line of sight from the point it looks at.</summary>
    public static Vector3 Position(Vector2 character, Vector3 forward, float distance)
        => new Vector3(character, TargetHeight) - forward * distance;
}
