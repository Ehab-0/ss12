using Content.Shared.Render3D;
using Robust.Client.Input;
using Robust.Shared.GameObjects;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Input;

namespace Content.Client.Render3D;

/// <summary>
///     Default bindings and input context for the 3D key functions (declared in <see cref="Render3DKeyFunctions"/>).
///     Done at runtime instead of through <c>ContentContexts</c> and <c>keybinds.yml</c>, so installing 3D into another
///     server codebase does not touch any of those files. A binding the player already saved is never overwritten.
/// </summary>
public static class Render3DKeys
{
    public static readonly BoundKeyFunction ToggleView = Render3DKeyFunctions.Toggle3DView;
    public static readonly BoundKeyFunction ToggleCameraMode = Render3DKeyFunctions.Toggle3DCameraMode;
    public static readonly BoundKeyFunction FreeCursor = Render3DKeyFunctions.Render3DFreeCursor;
    public static readonly BoundKeyFunction OpenSettings = Render3DKeyFunctions.Render3DSettings;
    public static readonly BoundKeyFunction Minimap = Render3DKeyFunctions.Render3DMinimap;
    public static readonly BoundKeyFunction MinimapSize = Render3DKeyFunctions.Render3DMinimapSize;
    public static readonly BoundKeyFunction PointList = Render3DKeyFunctions.Render3DPointList;
    public static readonly BoundKeyFunction PointListUp = Render3DKeyFunctions.Render3DPointListUp;
    public static readonly BoundKeyFunction PointListDown = Render3DKeyFunctions.Render3DPointListDown;
    public static readonly BoundKeyFunction PointListSelect = Render3DKeyFunctions.Render3DPointListSelect;

    /// <summary>Every function with its default key and the locale id of its name, in the order the settings window lists them.</summary>
    public static readonly (BoundKeyFunction Function, Keyboard.Key Key, string LocId)[] All =
    {
        (OpenSettings, Keyboard.Key.F11, "render3d-key-open-settings"),
        (FreeCursor, Keyboard.Key.Alt, "render3d-key-free-cursor"),
        (ToggleView, Keyboard.Key.F12, "render3d-key-toggle-view"),
        (ToggleCameraMode, Keyboard.Key.N, "render3d-key-toggle-camera"),
        (Minimap, Keyboard.Key.M, "render3d-key-minimap"),
        (MinimapSize, Keyboard.Key.Minus, "render3d-key-minimap-size"),
        (PointList, Keyboard.Key.L, "render3d-key-point-list"),
        (PointListUp, Keyboard.Key.Up, "render3d-key-point-list-up"),
        (PointListDown, Keyboard.Key.Down, "render3d-key-point-list-down"),
        (PointListSelect, Keyboard.Key.Space, "render3d-key-point-list-select"),
    };

    private static bool _registered;

    /// <summary>Adds the functions to the gameplay input context and registers default bindings. Safe to call repeatedly.</summary>
    public static void Register(IInputManager input)
    {
        if (_registered)
            return;

        _registered = true;

        if (input.Contexts.TryGetContext("common", out var ctx))
        {
            foreach (var (function, _, _) in All)
                ctx.AddFunction(function);
        }

        foreach (var (function, key, _) in All)
        {
            if (input.TryGetKeyBinding(function, out _))
                continue; // saved by the player (or already registered)

            input.RegisterBinding(new KeyBindingRegistration
            {
                Function = function,
                BaseKey = key,
                Type = KeyBindingType.State,
                CanFocus = false,
                CanRepeat = false,
            }, markModified: false);
        }
    }
}

/// <summary>
///     An input handler that runs its action once per key press. Presses that a handler does not consume are sent to the
///     server, and the client's prediction then replays every unacknowledged one through all handlers for the next few
///     frames; with a plain <c>InputCmdHandler.FromDelegate</c> a toggle or a step (zoom) would fire several times per
///     press. This remembers the last message it acted on and ignores the replays.
/// </summary>
public sealed class Render3DInputHandler : InputCmdHandler
{
    private readonly Action<bool> _action;
    private readonly Func<bool> _consume;
    private (GameTick Tick, ushort SubTick, BoundKeyState State)? _last;

    /// <param name="action">Called with true on key down and false on key up.</param>
    /// <param name="consume">True to stop the press reaching other handlers and the server (right for pure UI keys).</param>
    public Render3DInputHandler(Action<bool> action, bool consume)
        : this(action, () => consume)
    {
    }

    /// <param name="consume">Asked per press; lets a key be consumed only while the 3D view is the one being shown.</param>
    public Render3DInputHandler(Action<bool> action, Func<bool> consume)
    {
        _action = action;
        _consume = consume;
    }

    public override bool FireOutsidePrediction => true;

    public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
    {
        if (message.State is not (BoundKeyState.Down or BoundKeyState.Up))
            return false;

        var key = (message.Tick, message.SubTick, message.State);
        if (_last == key)
            return _consume(); // a replay of a press that was already handled

        _last = key;
        _action(message.State == BoundKeyState.Down);
        return _consume();
    }
}
