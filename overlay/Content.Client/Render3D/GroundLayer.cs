using System.Numerics;
using Content.Shared.CCVar;
using DrawDepthContent = Content.Shared.DrawDepth.DrawDepth;
using Robust.Client.Graphics;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

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
///     <para>
///         What the eye can see comes from the viewport's FOV shadow map on engine 283 and newer. Older engines do not
///         expose it, so a mask is captured instead (<see cref="FovMaskFillOverlay"/>): the engine paints the FOV colour
///         over everything the eye cannot see between the <see cref="OverlaySpace.WorldSpaceBelowFOV"/> and
///         <see cref="OverlaySpace.WorldSpace"/> overlays, which is exactly the 2D view's FOV.
///     </para>
/// </remarks>
public sealed class GroundLayer : IDisposable
{
    public const int PixelsPerTile = 32;

    private readonly IClyde _clyde;
    private readonly IOverlayManager _overlays;
    private readonly IConfigurationManager _cfg;

    private IClydeViewport? _viewport;
    private IRenderTexture? _groundTexture;
    private GroundCaptureOverlay? _overlay;

    private IRenderTexture? _fovMask;
    private FovMaskFillOverlay? _fovFill;
    private FovMaskCaptureOverlay? _fovCapture;
    private bool _fovMaskCaptured;
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
    public IRenderTexture? FovTarget => _viewport == null ? null : Render3DEngine.FovTarget(_viewport);

    /// <summary>Where <see cref="EntityPass"/> reads what the eye can see from, for the last <see cref="Render"/>.</summary>
    public GroundFov FovMode { get; private set; }

    /// <summary>
    ///     The FOV mask (same mapping as <see cref="GroundTexture"/>): <see cref="FovMaskKey"/> where the eye can see,
    ///     the engine's FOV colour where it cannot. Null unless <see cref="FovMode"/> is <see cref="GroundFov.Mask"/>.
    /// </summary>
    public Texture? FovMask => FovMode == GroundFov.Mask ? _fovMask?.Texture : null;

    public Color FovMaskKey { get; private set; } = Color.White;

    private bool _captured;

    public GroundLayer(IClyde clyde, IOverlayManager overlays)
    {
        _clyde = clyde;
        _overlays = overlays;
        _cfg = IoCManager.Resolve<IConfigurationManager>();
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

        var useMask = !Render3DEngine.HasViewportFov || _cfg.GetCVar(CCVars.Render3DDevFovMask);
        if (useMask && _fovMask == null)
            CreateFovMask();
        else if (!useMask && _fovMask != null)
            DisposeFovMask();

        if (_fovFill != null && _fovCapture != null)
        {
            FovMaskKey = MaskKey(_cfg.GetCVar(CVars.RenderFOVColor));
            _fovFill.Key = FovMaskKey;
            _fovCapture.Captured = false;
        }

        _overlay.Captured = false;
        _viewport.Render();
        _captured = _overlay.Captured;
        _fovMaskCaptured = _fovCapture?.Captured ?? false;

        // A frame without a copy shows everything, like a missing shadow map does.
        FovMode = !FovEnabled ? GroundFov.Off
            : _fovMask != null ? (_fovMaskCaptured ? GroundFov.Mask : GroundFov.Off)
            : FovTarget != null ? GroundFov.ShadowMap
            : GroundFov.Off;
    }

    /// <summary>
    ///     The colour the mask is filled with before the engine paints <paramref name="fovColor"/> (render.fov_color) over
    ///     what the eye cannot see: in every channel the opposite end from the FOV colour, so the two differ by at least
    ///     half in each channel whatever the server picks.
    /// </summary>
    public static Color MaskKey(string fovColor)
    {
        var fov = Color.TryParse(fovColor, out var parsed) ? parsed : Color.Black;
        return new Color(fov.R < 0.5f ? 1f : 0f, fov.G < 0.5f ? 1f : 0f, fov.B < 0.5f ? 1f : 0f, 1f);
    }

    private void CreateFovMask()
    {
        if (_viewport == null)
            return;

        _fovMask = _clyde.CreateRenderTarget(
            SizePixels,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            new TextureSampleParameters { Filter = false },
            "render3d-fov-mask");
        _fovFill = new FovMaskFillOverlay(_viewport, IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("unshaded").Instance());
        _fovCapture = new FovMaskCaptureOverlay(_viewport, _fovMask);
        _overlays.AddOverlay(_fovFill);
        _overlays.AddOverlay(_fovCapture);
        _fovMaskCaptured = false;
    }

    private void DisposeFovMask()
    {
        if (_fovFill != null)
            _overlays.RemoveOverlay(_fovFill);
        if (_fovCapture != null)
            _overlays.RemoveOverlay(_fovCapture);

        _fovFill = null;
        _fovCapture = null;
        _fovMask?.Dispose();
        _fovMask = null;
        _fovMaskCaptured = false;
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
        DisposeFovMask();
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
/// <remarks>Not sealed: the overlay manager keeps one overlay per type, so the FOV mask copy is a type of its own.</remarks>
public class GroundCaptureOverlay : Overlay
{
    private readonly IClydeViewport _viewport;
    private readonly IRenderTexture _target;

    public bool Captured;

    public override OverlaySpace Space { get; }

    public GroundCaptureOverlay(IClydeViewport viewport, IRenderTexture target)
        : this(viewport, target, OverlaySpace.WorldSpaceEntities, (int) DrawDepthContent.HighFloorObjects + 1)
    {
    }

    protected GroundCaptureOverlay(IClydeViewport viewport, IRenderTexture target, OverlaySpace space, int zIndex)
    {
        _viewport = viewport;
        _target = target;
        Space = space;
        ZIndex = zIndex;
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

/// <summary>
///     Copies the ground viewport after the engine applied FOV to it (the first <see cref="OverlaySpace.WorldSpace"/>
///     overlay), over the key colour of <see cref="FovMaskFillOverlay"/>: the FOV mask.
/// </summary>
public sealed class FovMaskCaptureOverlay : GroundCaptureOverlay
{
    public FovMaskCaptureOverlay(IClydeViewport viewport, IRenderTexture target)
        : base(viewport, target, OverlaySpace.WorldSpace, int.MinValue)
    {
    }
}

/// <summary>Where the 3D view reads what the eye can see from.</summary>
public enum GroundFov
{
    /// <summary>Everything is shown (no FOV for this eye, or nothing was captured).</summary>
    Off,

    /// <summary>The viewport's FOV shadow map (engine 283 and newer).</summary>
    ShadowMap,

    /// <summary>The mask captured by <see cref="FovMaskFillOverlay"/> (older engines).</summary>
    Mask,
}

/// <summary>
///     Fills the ground viewport with <see cref="Key"/> just before the engine applies FOV to it, so what the
///     <see cref="GroundCaptureOverlay"/> in <see cref="OverlaySpace.WorldSpace"/> copies afterwards is a mask: the key
///     where the eye can see, the FOV colour where it cannot. Draws over the ground viewport only, after the floor copy.
/// </summary>
public sealed class FovMaskFillOverlay : Overlay
{
    private readonly IClydeViewport _viewport;
    private readonly ShaderInstance _unshaded;

    public Color Key = Color.White;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    /// <param name="unshaded">The engine's "unshaded" shader: the default one multiplies by the light, and the key must stay exact.</param>
    public FovMaskFillOverlay(IClydeViewport viewport, ShaderInstance unshaded)
    {
        _viewport = viewport;
        _unshaded = unshaded;
        ZIndex = int.MaxValue;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport == _viewport;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.WorldHandle.UseShader(_unshaded);
        args.WorldHandle.DrawRect(args.WorldAABB, Key);
        args.WorldHandle.UseShader(null);
    }
}
