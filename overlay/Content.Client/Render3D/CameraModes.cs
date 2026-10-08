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
        CameraMode.Shoulder,
        CameraMode.Overhead,
        CameraMode.Isometric,
        CameraMode.Orbit,
        CameraMode.DynamicView,
    };

    /// <summary>The next camera after <paramref name="mode"/> in <see cref="Cycle"/>.</summary>
    public static CameraMode Next(CameraMode mode)
    {
        var index = Array.IndexOf(Cycle, mode);
        return Cycle[(index + 1) % Cycle.Length];
    }

    /// <summary>The camera the three settings ask for: the "extra" one if set (1 isometric, 2 orbit, 3 shoulder, 4 dynamic view), else overhead, else first person, else third person.</summary>
    public static CameraMode FromSettings(bool firstPerson, bool overhead, int extra)
    {
        return extra switch
        {
            1 => CameraMode.Isometric,
            2 => CameraMode.Orbit,
            3 => CameraMode.Shoulder,
            4 => CameraMode.DynamicView,
            _ => overhead ? CameraMode.Overhead : firstPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson,
        };
    }

    /// <summary>
    ///     The three settings for a camera. First person is also the remembered walking camera: the high and the shoulder cameras
    ///     leave it as it was, so leaving them goes back to the walking camera the player had.
    /// </summary>
    public static (bool FirstPerson, bool Overhead, int Extra) ToSettings(CameraMode mode, bool currentFirstPerson)
    {
        return mode switch
        {
            CameraMode.ThirdPerson => (false, false, 0),
            CameraMode.FirstPerson => (true, false, 0),
            CameraMode.Overhead => (currentFirstPerson, true, 0),
            CameraMode.Isometric => (currentFirstPerson, false, 1),
            CameraMode.Orbit => (currentFirstPerson, false, 2),
            CameraMode.Shoulder => (currentFirstPerson, false, 3),
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
        CameraMode.Shoulder => "render3d-camera-shoulder",
        CameraMode.Overhead => "render3d-camera-overhead",
        CameraMode.Isometric => "render3d-camera-isometric",
        CameraMode.Orbit => "render3d-camera-orbit",
        CameraMode.DynamicView => "render3d-camera-dynamic-view",
        _ => "render3d-camera-third-person",
    };
}
