using System.IO;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Console;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Render3D;

/// <summary>
///     Developer aid (off unless <c>render3d.dev_channel</c> is set): runs the lines of
///     <c>/render3d_dev.txt</c> in the client's user data folder as console commands and saves requested
///     screenshots to <c>/Screenshots/</c> there. Lets automated test runs drive the client without OS-level input.
///     Extra commands: <c>r3d_shot &lt;name&gt;</c>.
/// </summary>
public sealed partial class Render3DDevChannel : UIController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILogManager _logs = default!;
    [Dependency] private IInputManager _input = default!;

    private ResPath CommandFile => new($"/render3d_dev_{_cfg.GetCVar<string>("player.name")}.txt");
    private static readonly ResPath ShotDir = new("/Screenshots");
    private TimeSpan _next;
    private bool _loggedName;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logs.GetSawmill("render3d.dev");
        _console.RegisterCommand("r3d_shot", "Save a screenshot to user data /Screenshots/<name>.png", "r3d_shot <name>", ShotCommand);
        _console.RegisterCommand("r3d_look", "Override the 3D camera look direction (degrees), or 'off'", "r3d_look <yaw> <pitch> | r3d_look off", LookCommand);
        _console.RegisterCommand("r3d_dump", "Log the entities the 3D pass drew last frame", "r3d_dump", (shell, _, _) => UIManager.GetUIController<Render3DController>().Control?.DumpEntities());
        _console.RegisterCommand("r3d_press", "Feed a key function through the viewport input path (down|up|tap)", "r3d_press <function> [down|up|tap]", PressCommand);
        _console.RegisterCommand("r3d_pick", "Describe what the crosshair points at", "r3d_pick", (shell, _, _) => shell.WriteLine(UIManager.GetUIController<Render3DController>().Control?.DescribePick() ?? "no 3d view"));
        _console.RegisterCommand("r3d_aim", "Aim the crosshair at a floor point", "r3d_aim <x> <y>", (shell, _, args) =>
        {
            if (args.Length == 2
                && float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            {
                UIManager.GetUIController<Render3DController>().Control?.AimAtFloor(new System.Numerics.Vector2(x, y));
            }
        });
        _console.RegisterCommand("r3d_noself", "Exclude the own body from picks (as with a captured mouse)", "r3d_noself on|off", (shell, _, args) =>
        {
            if (UIManager.GetUIController<Render3DController>().Control is { } c && args.Length == 1)
                c.DevExcludeSelf = args[0] == "on";
        });
        _console.RegisterCommand("r3d_capture", "Force the captured-mouse (crosshair) mode without window focus", "r3d_capture on|off", (shell, _, args) =>
        {
            if (args.Length == 1)
                UIManager.GetUIController<Render3DController>().DevForceCapture = args[0] == "on";
        });
        _console.RegisterCommand("r3d_glow", "Toggle the glow (unshaded layer) atlas", "r3d_glow on|off", (shell, _, args) =>
        {
            if (UIManager.GetUIController<Render3DController>().Control is { } c && args.Length == 1)
                c.SetDebugNoGlow(args[0] == "off");
        });
        _console.RegisterCommand("r3d_cam", "Set the 3D camera mode", "r3d_cam fp|tp", CamCommand);
    }

    private void LookCommand(IConsoleShell shell, string argStr, string[] args)
    {
        var control = UIManager.GetUIController<Render3DController>().Control;
        if (control == null)
            return;

        if (args.Length == 1 && args[0] == "off")
        {
            control.LookOverride = null;
            return;
        }

        if (args.Length != 2
            || !double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var yaw)
            || !double.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pitch))
        {
            shell.WriteError("usage: r3d_look <yaw> <pitch> | off");
            return;
        }

        control.LookOverride = (yaw, pitch);
    }

    private void PressCommand(IConsoleShell shell, string argStr, string[] args)
    {
        var control = UIManager.GetUIController<Render3DController>().Control;
        if (control == null || args.Length < 1)
        {
            shell.WriteError("usage: r3d_press <function> [down|up|tap]");
            return;
        }

        var mode = args.Length > 1 ? args[1] : "tap";
        var center = control.GlobalPixelPosition + new System.Numerics.Vector2(control.PixelSize.X, control.PixelSize.Y) / 2f;
        var coords = new ScreenCoordinates(center, _clyde.MainWindow.Id);
        var function = new BoundKeyFunction(args[0]);

        if (mode is "down" or "tap")
            _input.ViewportKeyEvent(control, new BoundKeyEventArgs(function, BoundKeyState.Down, coords, false));
        if (mode is "up" or "tap")
            _input.ViewportKeyEvent(control, new BoundKeyEventArgs(function, BoundKeyState.Up, coords, false));
    }

    private void CamCommand(IConsoleShell shell, string argStr, string[] args)
    {
        var control = UIManager.GetUIController<Render3DController>().Control;
        if (control == null || args.Length != 1)
            return;

        control.Camera.Mode = args[0] == "fp" ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_cfg.GetCVar(CCVars.Render3DDevChannel))
            return;

        var now = IoCManager.Resolve<IGameTiming>().RealTime;
        if (now < _next)
            return;
        _next = now + TimeSpan.FromMilliseconds(400);
        if (!_loggedName)
        {
            _loggedName = true;
            _sawmill.Info($"dev channel polling {CommandFile}");
        }

        try
        {
            if (!_res.UserData.Exists(CommandFile))
                return;

            string text;
            using (var reader = _res.UserData.OpenText(CommandFile))
            {
                text = reader.ReadToEnd();
            }

            _res.UserData.Delete(CommandFile);

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                _sawmill.Info($"dev> {line}");
                _console.ExecuteCommand(line);
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"dev channel failed: {e}");
        }
    }

    private void ShotCommand(IConsoleShell shell, string argStr, string[] args)
    {
        var name = args.Length > 0 ? args[0] : "shot";
        _res.UserData.CreateDir(ShotDir);

        _clyde.Screenshot(ScreenshotType.Final, image =>
        {
            var path = ShotDir / $"{name}.png";
            using var file = _res.UserData.OpenWrite(path);
            image.SaveAsPng(file);
            _sawmill.Info($"saved screenshot {path}");
        });
    }
}
