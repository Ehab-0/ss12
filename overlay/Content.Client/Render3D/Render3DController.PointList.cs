using Content.Shared.CCVar;
using Content.Shared.IdentityManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.Input;
using Robust.Shared.Map;

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
        _console.RegisterCommand("render3d_pointlist_rows", "Print where the rows of the list are on the screen (pixels)", "render3d_pointlist_rows",
            (shell, _, _) =>
            {
                var i = 0;
                foreach (var c in PointListRowCentres())
                    shell.WriteLine($"row {i++}: {c.X:F0},{c.Y:F0}");
            });

        _console.RegisterCommand("render3d_pointlist", "Show or hide the list of what the crosshair points at", "render3d_pointlist [on|off]",
            (shell, _, args) =>
            {
                var on = args.Length > 0 ? args[0] == "on" : !_cfg.GetCVar(CCVars.Render3DPointList);
                _cfg.SetCVar(CCVars.Render3DPointList, on);
                shell.WriteLine($"pointlist: {(on ? "on" : "off")}");
            });
    }

    /// <summary>Developer aid: where the rows of the list are on the screen, for a test that clicks them with a real mouse.</summary>
    public IEnumerable<System.Numerics.Vector2> PointListRowCentres() => _pointList?.RowCentres() ?? Array.Empty<System.Numerics.Vector2>();

    private bool PointListVisible => _pointList is { Visible: true };

    private void TogglePointList() => _cfg.SetCVar(CCVars.Render3DPointList, !_cfg.GetCVar(CCVars.Render3DPointList));

    private void MovePointList(int delta)
    {
        if (!PointListVisible)
            return;

        _pointState.Move(delta);
        RefreshPointList();
    }

    /// <summary>The select key: the first press selects the highlighted row, the second acts on it as a click would.</summary>
    private void SelectInPointList()
    {
        if (!PointListVisible)
            return;

        switch (_pointState.Press())
        {
            case PointListPress.Selected:
                ApplyPointTarget();
                break;
            case PointListPress.Act:
                ActOnPointTarget();
                break;
        }
    }

    /// <summary>
    ///     Does what the left mouse button does at the crosshair, which now is the chosen target: use what is in the hand on it, or
    ///     pick it up, open it and so on. It goes through the viewport input path, like a click.
    /// </summary>
    private void ActOnPointTarget()
    {
        if (_control == null)
            return;

        var centre = _control.GlobalPixelPosition + new System.Numerics.Vector2(_control.PixelSize.X, _control.PixelSize.Y) / 2f;
        var coords = new ScreenCoordinates(centre, _clyde.MainWindow.Id);
        _input.ViewportKeyEvent(_control, new BoundKeyEventArgs(EngineKeyFunctions.Use, BoundKeyState.Down, coords, false));
        _input.ViewportKeyEvent(_control, new BoundKeyEventArgs(EngineKeyFunctions.Use, BoundKeyState.Up, coords, false));
        // the release of the click lets the target go (see Render3DController.OnStateEntered)
    }

    private void OnPointRowClicked(EntityUid uid)
    {
        _pointState.Toggle(uid);
        ApplyPointTarget();
    }

    private void ApplyPointTarget()
    {
        if (_control != null)
        {
            _control.PinnedTarget = _pointState.Target;
            if (_pointState.Target != null)
            {
                _pinnedAt = _timing.RealTime;
                _pinnedYaw = _control.Camera.Yaw;
                _pinnedPitch = _control.Camera.Pitch;
            }
        }

        RefreshPointList();
    }

    // A chosen target is meant for the next thing you do to it, not for good: it is let go when you have used it, when you look away
    // from it, or after a few seconds.
    private TimeSpan _pinnedAt;
    private double _pinnedYaw, _pinnedPitch;
    private const float PinnedLookAway = 0.30f;
    private static readonly TimeSpan PinnedLifetime = TimeSpan.FromSeconds(8);

    private void ReleasePointTarget()
    {
        if (_pointState.Target == null)
            return;

        _pointState.ClearTarget();
        if (_control != null)
            _control.PinnedTarget = null;

        RefreshPointList();
    }

    private void ExpirePointTarget()
    {
        if (_pointState.Target == null || _control == null)
            return;

        var yaw = MathF.Abs(MathF.IEEERemainder((float) _control.Camera.Yaw - (float) _pinnedYaw, MathF.Tau));
        var pitch = MathF.Abs((float) _control.Camera.Pitch - (float) _pinnedPitch);
        if (_timing.RealTime - _pinnedAt > PinnedLifetime || yaw > PinnedLookAway || pitch > PinnedLookAway)
            ReleasePointTarget();
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

        ExpirePointTarget();

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
        _pointList.SetChooseHint(
            _input.TryGetKeyBinding(Render3DKeys.PointListSelect, out var selectBinding) ? selectBinding.GetKeyString() : "Space",
            _input.TryGetKeyBinding(Render3DKeys.FreeCursor, out var freeBinding) ? freeBinding.GetKeyString() : "Alt");

        // nothing to show, nothing to choose and nothing chosen: the panel stays out of the way
        _pointList.Visible = _pointState.Entries.Count > 0 || _pointState.Target != null;
    }
}
