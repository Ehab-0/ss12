using System.Numerics;
using DrawDepthContent = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Render3D;

/// <summary>
///     Renders the normal 2D world around the camera into an offscreen <see cref="IClydeViewport"/> (north up, zoom 1,
///     32 px per tile) and keeps a copy of the "floor layer" of it: tiles, decals, puddles, pipes, cables and other
///     floor-level entities, already lit. The raymarcher uses that copy as the floor texture and the viewport's light
///     and FOV targets for walls, entities and visibility.
/// </summary>
/// <remarks>
///     The copy is taken by <see cref="GroundCaptureOverlay"/>: a world-space overlay whose ZIndex sits just above
///     <see cref="DrawDepthContent.HighFloorObjects"/>, so the engine draws it after every floor-level sprite and before
///     walls, mobs and objects (assumption A1).
/// </remarks>
public sealed class GroundLayer : IDisposable
{
    public const int PixelsPerTile = 32;

    private readonly IClyde _clyde;
    private readonly IOverlayManager _overlays;

    private IClydeViewport? _viewport;
    private IRenderTexture? _groundTexture;
    private GroundCaptureOverlay? _overlay;
    private readonly Robust.Shared.Graphics.Eye _eye = new() { Zoom = Vector2.One };

    /// <summary>Radius in tiles that the ground texture covers around <see cref="Center"/>.</summary>
    public int RadiusTiles { get; private set; }

    /// <summary>World position the texture is centred on.</summary>
    public Vector2 Center { get; private set; }

    public Vector2i SizePixels => new(RadiusTiles * 2 * PixelsPerTile, RadiusTiles * 2 * PixelsPerTile);

    /// <summary>Whether the 2D renderer applies hard FOV for this eye (ghosts and some admin views don't).</summary>
    public bool FovEnabled { get; private set; } = true;

    public bool Valid => _viewport != null && _groundTexture != null;

    /// <summary>The captured floor layer (lit). Null until the first successful capture.</summary>
    public Texture? GroundTexture => _captured ? _groundTexture?.Texture : null;

    public IClydeViewport? Viewport => _viewport;
    public IRenderTexture? LightTarget => _viewport?.LightRenderTarget;
    public IRenderTexture? FovTarget => _viewport?.FovRenderTarget;

    private bool _captured;

    public GroundLayer(IClyde clyde, IOverlayManager overlays)
    {
        _clyde = clyde;
        _overlays = overlays;
    }

    public void SetRadius(int radiusTiles)
    {
        radiusTiles = Math.Clamp(radiusTiles, 8, 48);
        if (radiusTiles == RadiusTiles && _viewport != null)
            return;

        DisposeTargets();
        RadiusTiles = radiusTiles;

        var size = SizePixels;
        _viewport = _clyde.CreateViewport(size, new TextureSampleParameters { Filter = false }, "render3d-ground");
        _viewport.AutomaticRender = false;
        _viewport.RenderScale = Vector2.One;
        _viewport.Eye = _eye;
        _viewport.ClearWhenMissingEye = true;

        _groundTexture = _clyde.CreateRenderTarget(
            size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            new TextureSampleParameters { Filter = false },
            "render3d-ground-copy");

        _overlay = new GroundCaptureOverlay(_viewport, _groundTexture);
        _overlays.AddOverlay(_overlay);
        _captured = false;
    }

    /// <summary>
    ///     Points the ground eye at <paramref name="position"/> and renders. <paramref name="source"/> supplies the
    ///     fov/light flags of the player's real eye.
    /// </summary>
    public void Render(MapCoordinates position, IEye? source)
    {
        if (_viewport == null || _overlay == null)
            return;

        // Snap to whole pixels so the floor doesn't shimmer as the camera moves.
        var snapped = new Vector2(
            MathF.Round(position.X * PixelsPerTile) / PixelsPerTile,
            MathF.Round(position.Y * PixelsPerTile) / PixelsPerTile);

        _eye.Position = new MapCoordinates(snapped, position.MapId);
        _eye.Rotation = Angle.Zero;
        _eye.Zoom = Vector2.One;
        _eye.Offset = Vector2.Zero;
        _eye.DrawFov = source?.DrawFov ?? true;
        _eye.DrawLight = source?.DrawLight ?? true;
        FovEnabled = _eye.DrawFov && _eye.DrawLight;
        Center = snapped;

        _overlay.Captured = false;
        _viewport.Render();
        _captured = _overlay.Captured;
    }

    /// <summary>Maps a world XY position to uv in the ground texture (v grows downwards, like screen pixels).</summary>
    public Vector2 WorldToUv(Vector2 world)
    {
        var size = RadiusTiles * 2f;
        return new Vector2(
            (world.X - Center.X) / size + 0.5f,
            0.5f - (world.Y - Center.Y) / size);
    }

    /// <summary>World size of the ground texture in tiles per side.</summary>
    public float WorldSize => RadiusTiles * 2f;

    private void DisposeTargets()
    {
        if (_overlay != null)
        {
            _overlays.RemoveOverlay(_overlay);
            _overlay = null;
        }

        _viewport?.Dispose();
        _viewport = null;
        _groundTexture?.Dispose();
        _groundTexture = null;
        _captured = false;
    }

    public void Dispose()
    {
        DisposeTargets();
    }
}

/// <summary>
///     Copies the engine's screen texture (the ground viewport's render target at this point of the frame) into a
///     persistent texture. Does nothing for any other viewport.
/// </summary>
public sealed class GroundCaptureOverlay : Overlay
{
    private readonly IClydeViewport _viewport;
    private readonly IRenderTexture _target;

    public bool Captured;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public GroundCaptureOverlay(IClydeViewport viewport, IRenderTexture target)
    {
        _viewport = viewport;
        _target = target;
        ZIndex = (int) DrawDepthContent.HighFloorObjects + 1;
        RequestScreenTexture = true;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport == _viewport;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var screenTexture = ScreenTexture;
        if (screenTexture == null)
            return;

        var handle = args.RenderHandle.DrawingHandleScreen;
        handle.RenderInRenderTarget(_target, () =>
        {
            handle.SetTransform(Matrix3x2.Identity);
            handle.UseShader(null);
            // We are inside a render target of the same size as the screen texture, in screen (pixel) space.
            handle.DrawTextureRect(screenTexture, new UIBox2(Vector2.Zero, _target.Size), Color.White);
        }, Color.Transparent);

        Captured = true;
    }
}
