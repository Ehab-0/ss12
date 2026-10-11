using Robust.Client.Graphics;

namespace Content.Client.Render3D;

// Engines before 286 (installed by the SS12 installer instead of Content.Client/Render3D/Engine/Render3DEngine.Mouse.cs).
// The window cannot capture the mouse (IClydeWindow.SetRelativeMouseMode is from 286), so the view is steered with the
// hidden cursor instead (MouseSteer).
public static partial class Render3DEngine
{
    /// <summary>Whether the window can capture the mouse for mouse-look. Without it the view is steered with the cursor (<see cref="MouseSteer"/>).</summary>
    public static bool HasRelativeMouse => false;

    public static void SetRelativeMouseMode(IClydeWindow window, bool relative)
    {
    }
}
