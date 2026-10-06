using Content.Shared.CCVar;
using Robust.Shared.Console;

namespace Content.Client.Render3D;

/// <summary>The list of what the crosshair points at, at the right edge of the 3D view (see <see cref="Render3DPointList"/>).</summary>
public sealed partial class Render3DController
{
    private Render3DPointList? _pointList;
    private readonly List<(string Text, bool InReach)> _pointLines = new();

    private void InitializePointList()
    {
        _console.RegisterCommand("render3d_pointlist", "Show or hide the list of what the crosshair points at", "render3d_pointlist [on|off]",
            (shell, _, args) =>
            {
                var on = args.Length > 0 ? args[0] == "on" : !_cfg.GetCVar(CCVars.Render3DPointList);
                _cfg.SetCVar(CCVars.Render3DPointList, on);
                shell.WriteLine($"pointlist: {(on ? "on" : "off")}");
            });
    }

    private void TogglePointList() => _cfg.SetCVar(CCVars.Render3DPointList, !_cfg.GetCVar(CCVars.Render3DPointList));

    private void AddPointList(Render3DViewportControl view)
    {
        _pointList = new Render3DPointList { Visible = false };
        view.AddChild(_pointList);
    }

    private void UpdatePointList()
    {
        if (_pointList == null || _control == null)
            return;

        if (!Active || !_cfg.GetCVar(CCVars.Render3DPointList) || !_control.RelativeMouse)
        {
            _pointList.Visible = false;
            return;
        }

        _control.DescribePointed(_pointLines, 8, out var hidden);
        _pointList.SetEntries(_pointLines, hidden);
        _pointList.Visible = true;
    }
}
