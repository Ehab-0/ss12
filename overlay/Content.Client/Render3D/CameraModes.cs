namespace Content.Client.Render3D;

/// <summary>
///     Which camera is on, and how the settings that store it (<c>render3d.first_person</c>, <c>render3d.overhead</c> and
///     <c>render3d.camera_extra</c>) map to it. Kept free of engine state so it can be unit tested.
/// </summary>
public static class CameraModes
{
    /// <summary>The order the camera key goes through.</summary>
    public static readonly CameraMode[] Cycle =
    {
        CameraMode.ThirdPerson,
        CameraMode.FirstPerson,
        CameraMode.Overhead,
        CameraMode.DynamicView,
    };

    /// <summary>The next camera after <paramref name="mode"/> in <see cref="Cycle"/>.</summary>
    public static CameraMode Next(CameraMode mode)
    {
        var index = Array.IndexOf(Cycle, mode);
        return Cycle[(index + 1) % Cycle.Length];
    }

    /// <summary>The camera the three settings ask for: the dynamic view if the "extra" setting is 4, else overhead (the old isometric and orbit values, 1 and 2, also mean overhead), else first person, else third person (the old shoulder value, 3, means third person).</summary>
    public static CameraMode FromSettings(bool firstPerson, bool overhead, int extra)
    {
        return extra switch
        {
            4 => CameraMode.DynamicView,
            1 or 2 => CameraMode.Overhead,
            _ => overhead ? CameraMode.Overhead : firstPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson,
        };
    }

    /// <summary>
    ///     The three settings for a camera. First person is also the remembered walking camera: the high cameras
    ///     leave it as it was, so leaving them goes back to the walking camera the player had.
    /// </summary>
    public static (bool FirstPerson, bool Overhead, int Extra) ToSettings(CameraMode mode, bool currentFirstPerson)
    {
        return mode switch
        {
            CameraMode.ThirdPerson => (false, false, 0),
            CameraMode.FirstPerson => (true, false, 0),
            CameraMode.Overhead => (currentFirstPerson, true, 0),
            CameraMode.DynamicView => (currentFirstPerson, false, 4),
            _ => (currentFirstPerson, false, 0),
        };
    }

    /// <summary>The camera the key for first / third person switches to: the other of the two, and from any other camera the walking one.</summary>
    public static CameraMode ToggleWalking(CameraMode mode, bool rememberedFirstPerson)
    {
        return mode switch
        {
            CameraMode.ThirdPerson => CameraMode.FirstPerson,
            CameraMode.FirstPerson => CameraMode.ThirdPerson,
            _ => rememberedFirstPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson,
        };
    }

    /// <summary>The locale id of the name of a camera.</summary>
    public static string LocId(CameraMode mode) => mode switch
    {
        CameraMode.FirstPerson => "render3d-camera-first-person",
        CameraMode.Overhead => "render3d-camera-overhead",
        CameraMode.DynamicView => "render3d-camera-dynamic-view",
        _ => "render3d-camera-third-person",
    };
}
