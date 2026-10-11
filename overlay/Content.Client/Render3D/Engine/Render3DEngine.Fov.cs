using Robust.Client.Graphics;

namespace Content.Client.Render3D;

// Engine 283 and newer. The version for older engines is Tools/ss12-engine/before-283/Content.Client/Render3D/Engine/Render3DEngine.Fov.cs.
public static partial class Render3DEngine
{
    /// <summary>Whether a viewport's field of view (the shadow map of what the eye can see) can be read. Without it <see cref="GroundLayer"/> captures a mask instead.</summary>
    public static bool HasViewportFov => true;

    public static IRenderTexture? FovTarget(IClydeViewport viewport)
    {
        return viewport.FovRenderTarget;
    }
}
