using Robust.Client.Graphics;

namespace Content.Client.Render3D;

// Engines before 265 (installed by the SS12 installer instead of Content.Client/Render3D/Engine/Render3DEngine.Uniforms.cs).
public static partial class Render3DEngine
{
    // Shader parameters of System.Numerics vector types. Before 265 the engine only has overloads for its own vector types
    // (Robust.Shared.Maths.Vector3 and Vector4), and nothing converts to them, so the 3D code's calls land here. The Color
    // overload is no substitute: the engine converts colours from sRGB to linear.

    public static void SetParameter(this ShaderInstance shader, string name, System.Numerics.Vector3 value)
    {
        shader.SetParameter(name, new Robust.Shared.Maths.Vector3(value.X, value.Y, value.Z));
    }

    public static void SetParameter(this ShaderInstance shader, string name, System.Numerics.Vector4 value)
    {
        shader.SetParameter(name, new Robust.Shared.Maths.Vector4(value.X, value.Y, value.Z, value.W));
    }
}
