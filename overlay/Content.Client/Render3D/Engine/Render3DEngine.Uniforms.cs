using Robust.Client.Graphics;

namespace Content.Client.Render3D;

// Engine 265 and newer. The version for older engines is Tools/ss12-engine/before-265/Content.Client/Render3D/Engine/Render3DEngine.Uniforms.cs.
public static partial class Render3DEngine
{
    // Shader parameters of System.Numerics vector types. The engine's own overloads take them from 265 on, and an
    // instance method always wins over an extension method, so these are never called here. Before 265 the engine only
    // has overloads for its own vector types, and the 3D code's calls land in the version that converts.

    public static void SetParameter(this ShaderInstance shader, string name, System.Numerics.Vector3 value)
    {
        shader.SetParameter(name, value);
    }

    public static void SetParameter(this ShaderInstance shader, string name, System.Numerics.Vector4 value)
    {
        shader.SetParameter(name, value);
    }
}
