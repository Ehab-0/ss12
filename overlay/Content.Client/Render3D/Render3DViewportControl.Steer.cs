using System.Numerics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Render3D;

/// <summary>
///     Mouse steering (<see cref="MouseSteer"/>): mouse-look for engines that cannot capture the mouse (before 286), or
///     when the player asks for it (<see cref="CCVars.Render3DMouseSteer"/>). The view stays in the captured state
///     (<see cref="RelativeMouse"/>: crosshair, picking and the free-cursor key work as usual), but the OS cursor is not
///     held: it is hidden, and how far it is from the centre turns the view.
/// </summary>
public sealed partial class Render3DViewportControl
{
    /// <summary>Set by the controller: the mouse is not captured, the hidden cursor steers the view.</summary>
    public bool Steering;

    private bool _steerArmed;
    private bool _steerWasLive;
    private bool _steerWasDragging;
    private Vector2 _steerOffset;
    private ICursor? _hiddenCursor;

    /// <summary>Developer aid (r3d_steer): steer as if the cursor were at this offset (joystick units) instead of where it is.</summary>
    public Vector2? DevSteerCursor;

    /// <summary>True while the hidden cursor steers: <see cref="Steering"/> and the view has the mouse.</summary>
    private bool SteerLive => Steering && RelativeMouse;

    /// <summary>Turns the camera for one frame from where the hidden cursor is. Called by the controller after the capture state is decided.</summary>
    public void SteerTick(float dt)
    {
        var live = SteerLive;
        if (live != (CustomCursorShape != null))
            CustomCursorShape = live ? _hiddenCursor ??= _clyde.CreateCursor(new SixLabors.ImageSharp.Image<Rgba32>(1, 1), Vector2i.Zero) : null;

        // Anything that let go of the mouse (a window, the free-cursor key, the focus, a turn drag) moved the cursor:
        // wait until it is back in the middle before turning again.
        if (live != _steerWasLive || _steerWasDragging && !_turnDragging)
            _steerArmed = false;

        _steerWasLive = live;
        _steerWasDragging = _turnDragging;

        if (!live || _turnDragging)
            return;

        var cursor = _input.MouseScreenPosition.Position;
        var deadZone = _cfg.GetCVar(CCVars.Render3DSteerDeadZone);
        _steerOffset = DevSteerCursor ?? MouseSteer.Offset(cursor - GlobalPixelPosition, PixelSize);
        _steerArmed = MouseSteer.Arm(_steerArmed, _steerOffset, deadZone);
        if (!_steerArmed || dt <= 0f)
            return;

        // a slow frame (a hitch, a window in the background) turns as far as a tenth of a second would
        dt = MathF.Min(dt, 0.1f);

        // In a window the cursor can leave, and then it stays where it left: at the window's edge.
        if (DevSteerCursor == null && _cfg.GetCVar(CVars.DisplayWindowMode) == 0 && MouseSteer.AtWindowEdge(cursor, _clyde.MainWindow.Size))
            return;

        var (yaw, pitch) = MouseSteer.Turn(_steerOffset, deadZone, _cfg.GetCVar(CCVars.Render3DSteerSpeed) * Sensitivity, dt,
            _cfg.GetCVar(CCVars.Render3DSteerPitch), InvertY);
        Camera.AddLook(yaw, pitch);
    }

    /// <summary>The middle mouse drag turns the walking cameras directly while steering. True when it did.</summary>
    private bool SteerDragTurn(Vector2 relative)
    {
        if (!SteerLive)
            return false;

        var k = RadiansPerPixel * Sensitivity;
        Camera.AddLook(-relative.X * k, (InvertY ? relative.Y : -relative.Y) * k);
        return true;
    }

    /// <summary>The dead zone around the crosshair and the cursor in it, and a hint while steering waits for the cursor.</summary>
    private void DrawSteer(DrawingHandleScreen screen, Vector2i pixelSize)
    {
        if (!SteerLive || !_cfg.GetCVar(CCVars.Render3DSteerIndicator))
            return;

        var size = (Vector2) pixelSize;
        var centre = size * 0.5f;
        var radius = MouseSteer.Radius(size);
        var deadZone = Math.Clamp(_cfg.GetCVar(CCVars.Render3DSteerDeadZone), 0f, 0.9f);
        var faint = new Color(1f, 1f, 1f, _steerArmed ? 0.18f : 0.45f);
        if (deadZone > 0f)
            screen.DrawCircle(centre, deadZone * radius, faint, false);

        var offset = _steerOffset;
        if (offset.Length() > 1f)
            offset /= offset.Length();
        var dot = centre + offset * radius;
        screen.DrawCircle(dot, 3f, new Color(0f, 0f, 0f, 0.5f));
        screen.DrawCircle(dot, 2f, new Color(1f, 1f, 1f, _steerArmed ? 0.6f : 0.9f));

        if (_steerArmed)
            return;

        _labelFont ??= new VectorFont(_resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 12);
        var text = Loc.GetString("render3d-steer-centre");
        var dims = screen.GetDimensions(_labelFont, text, 1f);
        var pos = new Vector2(centre.X - dims.X / 2f, centre.Y + MathF.Max(deadZone * radius, 12f) + 10f);
        screen.DrawRect(UIBox2.FromDimensions(pos - new Vector2(6, 3), dims + new Vector2(12, 6)), new Color(0f, 0f, 0f, 0.5f));
        screen.DrawString(_labelFont, pos, text, Color.White);
    }

    private void DisposeSteer()
    {
        if (CustomCursorShape != null)
            CustomCursorShape = null;
        _hiddenCursor?.Dispose();
        _hiddenCursor = null;
    }
}
