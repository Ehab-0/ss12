using Content.Shared.CCVar;
using Content.Shared.IdentityManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.Render3D;

/// <summary>
///     The list of what the crosshair points at, at the right edge of the 3D view (see <see cref="Render3DPointList"/>). It follows the
///     crosshair while the mouse is captured and stays as it was while the free-mouse key is held, so a row can be clicked; the up and
///     down keys move a highlight and enter chooses the highlighted one as the target the crosshair acts on.
/// </summary>
public sealed partial class Render3DController
{
    private Render3DPointList? _pointList;
    private readonly PointListState _pointState = new();
    private readonly List<(EntityUid Uid, string Text, bool InReach)> _pointLines = new();

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

    private bool PointListVisible => _pointList is { Visible: true };

    private void TogglePointList() => _cfg.SetCVar(CCVars.Render3DPointList, !_cfg.GetCVar(CCVars.Render3DPointList));

    private void MovePointList(int delta)
    {
        if (!PointListVisible)
            return;

        _pointState.Move(delta);
        RefreshPointList();
    }

    private void SelectInPointList()
    {
        if (!PointListVisible || !_pointState.SelectHighlighted())
            return;

        ApplyPointTarget();
    }

    private void OnPointRowClicked(EntityUid uid)
    {
        _pointState.Toggle(uid);
        ApplyPointTarget();
    }

    private void ApplyPointTarget()
    {
        if (_control != null)
            _control.PinnedTarget = _pointState.Target;

        RefreshPointList();
    }

    private void AddPointList(Render3DViewportControl view)
    {
        _pointList = new Render3DPointList { Visible = false };
        _pointList.RowClicked += OnPointRowClicked;
        view.AddChild(_pointList);
    }

    private void RefreshPointList()
    {
        if (_pointList == null)
            return;

        string? targetName = null;
        if (_pointState.Target is { } target && _entMan.EntityExists(target))
            targetName = Identity.Name(target, _entMan);

        _pointList.SetState(_pointState, 0, targetName);
    }

    private void UpdatePointList()
    {
        if (_pointList == null || _control == null)
            return;

        if (!Active || !_cfg.GetCVar(CCVars.Render3DPointList))
        {
            _pointList.Visible = false;
            if (_pointState.Target != null)
            {
                _pointState.Clear();
                _control.PinnedTarget = null;
            }

            return;
        }

        // the chosen target can go away (picked up, destroyed, too far): the view lets it go and the list follows
        if (_pointState.Target != null && _control.PinnedTarget == null)
            _pointState.ClearTarget();

        if (_control.RelativeMouse)
        {
            // the crosshair moves the list; while the mouse is free the list stays as it was so a row can be clicked
            _control.DescribePointed(_pointLines, 8, out var hidden);
            _pointState.SetEntries(_pointLines);
            string? targetName = null;
            if (_pointState.Target is { } target && _entMan.EntityExists(target))
                targetName = Identity.Name(target, _entMan);

            _pointList.SetState(_pointState, hidden, targetName);
        }

        _pointList.SetToggleKey(_input.TryGetKeyBinding(Render3DKeys.PointList, out var binding) ? binding.GetKeyString() : "L");
        _pointList.Visible = true;
    }
}
