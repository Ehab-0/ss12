using Robust.Shared.Input;

namespace Content.Shared.Render3D;

/// <summary>
///     Key functions of the 3D view. The engine builds its function-id map (which is also how input is networked) by
///     scanning for classes with <see cref="KeyFunctionsAttribute"/>, so this class in its own file is enough:
///     <c>ContentKeyFunctions</c> does not need an edit. It has to live in a shared assembly so the client and the
///     server compute the same ids. Default keys and the gameplay input context are set up at runtime by
///     <c>Content.Client.Render3D.Render3DKeys</c>.
/// </summary>
[KeyFunctions]
public static class Render3DKeyFunctions
{
    public static readonly BoundKeyFunction Toggle3DView = "Toggle3DView";
    public static readonly BoundKeyFunction Toggle3DCameraMode = "Toggle3DCameraMode";
    public static readonly BoundKeyFunction Render3DFreeCursor = "Render3DFreeCursor";
    public static readonly BoundKeyFunction Render3DSettings = "Render3DSettings";
    public static readonly BoundKeyFunction Render3DMinimap = "Render3DMinimap";
    public static readonly BoundKeyFunction Render3DMinimapSize = "Render3DMinimapSize";
}
