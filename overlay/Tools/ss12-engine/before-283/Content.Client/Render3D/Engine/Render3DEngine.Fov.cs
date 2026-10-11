using Robust.Client.Graphics;

namespace Content.Client.Render3D;

// Engines before 283 (installed by the SS12 installer instead of Content.Client/Render3D/Engine/Render3DEngine.Fov.cs).
// IClydeViewport.FovRenderTarget is from 283; GroundLayer captures what the eye cannot see as a mask instead.
public static partial class Render3DEngine
{
    /// <summary>Whether a viewport's field of view (the shadow map of what the eye can see) can be read. Without it <see cref="GroundLayer"/> captures a mask instead.</summary>
    public static bool HasViewportFov => false;

    public static IRenderTexture? FovTarget(IClydeViewport viewport)
    {
        return null;
    }
}
