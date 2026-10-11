using Robust.Client.Graphics;

namespace Content.Client.Render3D;

// Engine 286 and newer. The version for older engines is Tools/ss12-engine/before-286/Content.Client/Render3D/Engine/Render3DEngine.Mouse.cs.
public static partial class Render3DEngine
{
    /// <summary>Whether the window can capture the mouse for mouse-look. Without it the view is steered with the cursor (<see cref="MouseSteer"/>).</summary>
    public static bool HasRelativeMouse => true;

    public static void SetRelativeMouseMode(IClydeWindow window, bool relative)
    {
        window.SetRelativeMouseMode(relative);
    }
}
