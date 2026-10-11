using Content.Shared.CCVar;

namespace Content.Client.Render3D;

/// <summary>
///     Mouse steering (<see cref="MouseSteer"/>) instead of the captured mouse: always on engines that cannot capture the
///     mouse (before 286), and on newer ones when the player picks it (<see cref="CCVars.Render3DMouseSteer"/>).
/// </summary>
public sealed partial class Render3DController
{
    /// <summary>Whether the hidden cursor steers the view instead of a captured mouse.</summary>
    private bool WantSteer => !Render3DEngine.HasRelativeMouse || _cfg.GetCVar(CCVars.Render3DMouseSteer);

    /// <summary>Asks the window to capture the mouse, or to let it go; never captures while steering.</summary>
    private void RequestRelativeMouse(bool relative)
    {
        Render3DEngine.SetRelativeMouseMode(_clyde.MainWindow, relative && !(_control?.Steering ?? false));
    }

    private void UpdateSteering(float dt)
    {
        if (_control == null)
            return;

        var steer = WantSteer;
        if (steer != _control.Steering)
        {
            _control.Steering = steer;
            // capture or let go of the mouse for the new way of looking
            SetRelative(_relative, force: true);
        }

        _control.SteerTick(dt);
    }
}
