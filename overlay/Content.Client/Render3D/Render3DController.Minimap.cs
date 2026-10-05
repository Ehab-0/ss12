using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>The minimap in a corner of the 3D view (see <see cref="Render3DMinimap"/>).</summary>
public sealed partial class Render3DController
{
    private Render3DMinimap? _minimap;
    private SharedTransformSystem? _xformSystem;

    private void InitializeMinimap()
    {
        _console.RegisterCommand("render3d_minimap", "Show or set the minimap in the 3D view", "render3d_minimap [off|small|large]",
            (shell, _, args) =>
            {
                if (args.Length == 0)
                {
                    shell.WriteLine($"minimap: {MinimapModeName(_cfg.GetCVar(CCVars.Render3DMinimapMode))}"
                        + (_minimap == null ? " (not created)" : $" visible={_minimap.Visible} at {_minimap.GlobalPixelPosition} size {_minimap.PixelSize} grid={_minimap.Grid} host {_host?.GlobalPixelPosition} {_host?.PixelSize} view {_control?.GlobalPixelPosition} {_control?.PixelSize}"));
                    return;
                }

                var mode = args[0] switch { "off" => 0, "small" => 1, "large" => 2, _ => -1 };
                if (mode < 0)
                {
                    shell.WriteError("usage: render3d_minimap [off|small|large]");
                    return;
                }

                _cfg.SetCVar(CCVars.Render3DMinimapMode, mode);
                shell.WriteLine($"minimap: {MinimapModeName(mode)}");
            });
    }

    private static string MinimapModeName(int mode) => mode switch { 1 => "small", 2 => "large", _ => "off" };

    /// <summary>The size (1 small, 2 large) the minimap had when it was last shown, which the on/off key brings back.</summary>
    private int _minimapLastShown = 1;

    /// <summary>The on/off key: hides the minimap, or shows it again at the size it had.</summary>
    private void ToggleMinimap()
    {
        var mode = _cfg.GetCVar(CCVars.Render3DMinimapMode);
        _cfg.SetCVar(CCVars.Render3DMinimapMode, mode > 0 ? 0 : _minimapLastShown);
    }

    /// <summary>The size key: small to large and back. A hidden minimap comes back small.</summary>
    private void SwitchMinimapSize()
    {
        var mode = _cfg.GetCVar(CCVars.Render3DMinimapMode);
        _cfg.SetCVar(CCVars.Render3DMinimapMode, mode == 1 ? 2 : 1);
    }

    /// <summary>
    ///     The minimap lives inside the 3D view control, not next to it: the gameplay screen's viewport widget is a box container,
    ///     where a second child takes its own share of the width and squeezes the 3D view.
    /// </summary>
    private void AddMinimap(Render3DViewportControl view)
    {
        _minimap = new Render3DMinimap { Visible = false };
        view.AddChild(_minimap);
    }

    private void UpdateMinimap()
    {
        if (_minimap == null || _control == null || _host == null)
            return;

        var mode = _cfg.GetCVar(CCVars.Render3DMinimapMode);
        var player = _player.LocalEntity;
        if (!Active || mode <= 0 || player is not { } body
            || !_entMan.TryGetComponent(body, out TransformComponent? xform) || xform.GridUid is not { } grid)
        {
            _minimap.Visible = false;
            return;
        }

        _minimapLastShown = mode >= 2 ? 2 : 1;
        var large = mode >= 2;
        var side = large
            ? Math.Clamp(MathF.Min(_host.Size.X, _host.Size.Y) * 0.7f, 300f, 640f)
            : MinimapView.SmallSide(Math.Clamp(_cfg.GetCVar(CCVars.Render3DMinimapSize), 120, 360), MathF.Min(_host.Size.X, _host.Size.Y));
        _minimap.SetSize = new Vector2(side, side);
        _minimap.HorizontalAlignment = large ? Robust.Client.UserInterface.Control.HAlignment.Center : Robust.Client.UserInterface.Control.HAlignment.Left;
        _minimap.VerticalAlignment = large ? Robust.Client.UserInterface.Control.VAlignment.Center : Robust.Client.UserInterface.Control.VAlignment.Top;
        _minimap.Margin = large ? new Thickness(0) : new Thickness(118, 122, 0, 0);

        var transforms = _xformSystem ??= _entMan.System<SharedTransformSystem>();
        var head = _control.Camera.Head;
        _minimap.Grid = grid;
        _minimap.PlayerLocal = Vector2.Transform(new Vector2(head.X, head.Y), transforms.GetInvWorldMatrix(grid));

        // the camera yaw is the direction the player looks: 0 north, turning counter-clockwise. The grid may be turned.
        var yaw = (float) _control.Camera.Yaw;
        var toGrid = new Angle(-transforms.GetWorldRotation(grid).Theta);
        var facing = toGrid.RotateVec(new Vector2(-MathF.Sin(yaw), MathF.Cos(yaw)));
        var north = toGrid.RotateVec(Vector2.UnitY);
        _minimap.Facing = facing.LengthSquared() > 0.0001f ? Vector2.Normalize(facing) : Vector2.UnitY;
        _minimap.North = north.LengthSquared() > 0.0001f ? Vector2.Normalize(north) : Vector2.UnitY;

        var rotate = _cfg.GetCVar(CCVars.Render3DMinimapRotate);
        _minimap.Up = rotate ? _minimap.Facing : _minimap.North;
        _minimap.ShowNorth = rotate;
        _minimap.ShowLabels = large;
        _minimap.Radius = Math.Clamp(_cfg.GetCVar(CCVars.Render3DMinimapRange), 8f, 60f) * (large ? 2.4f : 1f);
        _minimap.Visible = true;
    }
}
