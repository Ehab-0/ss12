using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.Render3D;

/// <summary>
///     A render target that holds, for the current frame, the composited 2D sprite of every entity drawn as a 3D
///     quad. Slots are packed with a simple shelf allocator and redrawn each frame (sprite layers, clothing,
///     in-hand items, damage and animations are all baked in by <c>DrawEntity</c>).
/// </summary>
public sealed class BillboardAtlas : IDisposable
{
    public const int PixelsPerTile = GroundLayer.PixelsPerTile;
    private const int Padding = 2;
    private const int MaxSlotPixels = 256;

    private readonly IClyde _clyde;
    private IRenderTexture? _target;
    private IRenderTexture? _glow;
    private int _size;

    // shelf allocator state
    private int _cursorX;
    private int _cursorY;
    private int _rowHeight;

    public Texture? Texture => _target?.Texture;

    /// <summary>The atlas render target and its glow twin (a developer aid: <c>r3d_atlas</c> saves them).</summary>
    public IRenderTexture? Target => _target;
    public IRenderTexture? GlowTarget => _glow;

    /// <summary>Same layout as <see cref="Texture"/>, but holds only the unshaded layers (transparent elsewhere).</summary>
    public Texture? GlowTexture => _glow?.Texture;
    public int Size => _size;

    public BillboardAtlas(IClyde clyde)
    {
        _clyde = clyde;
    }

    /// <summary>An allocator only, with no render targets, of the given size in pixels (for tests of the slot layout).</summary>
    public BillboardAtlas(int size)
    {
        _clyde = null!;
        _size = size;
    }

    public void EnsureSize(int size)
    {
        size = Math.Clamp(size, 512, 4096);
        if (_target != null && _size == size)
            return;

        _target?.Dispose();
        _glow?.Dispose();
        _size = size;
        _target = _clyde.CreateRenderTarget(
            new Vector2i(size, size),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            new TextureSampleParameters { Filter = true },
            "render3d-billboard-atlas");
        _glow = _clyde.CreateRenderTarget(
            new Vector2i(size, size),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            new TextureSampleParameters { Filter = true },
            "render3d-billboard-glow");
    }

    public void BeginFrame()
    {
        _cursorX = 0;
        _cursorY = 0;
        _rowHeight = 0;
    }

    /// <summary>
    ///     False for art too big for one slot (more than <see cref="MaxSlotPixels"/> pixels either way). Such a sprite would be
    ///     drawn past the edge of its slot onto its neighbours, so it is left out instead.
    /// </summary>
    public static bool Fits(Box2 bounds)
    {
        return bounds.Width * PixelsPerTile <= MaxSlotPixels && bounds.Height * PixelsPerTile <= MaxSlotPixels;
    }

    /// <summary>Reserves a slot for sprite bounds <paramref name="bounds"/> (tile units). False when the atlas is full.</summary>
    public bool TryAllocate(Box2 bounds, out UIBox2i slot, out Vector2 originInSlot)
    {
        var w = Math.Clamp((int) MathF.Ceiling(bounds.Width * PixelsPerTile), 1, MaxSlotPixels);
        var h = Math.Clamp((int) MathF.Ceiling(bounds.Height * PixelsPerTile), 1, MaxSlotPixels);
        var pw = w + Padding;
        var ph = h + Padding;

        if (_cursorX + pw > _size)
        {
            _cursorX = 0;
            _cursorY += _rowHeight;
            _rowHeight = 0;
        }

        if (_cursorY + ph > _size)
        {
            slot = default;
            originInSlot = default;
            return false;
        }

        slot = UIBox2i.FromDimensions(_cursorX, _cursorY, w, h);
        // Pixel position of the entity origin inside the slot (screen y grows downwards).
        originInSlot = new Vector2(-bounds.Left * PixelsPerTile, bounds.Top * PixelsPerTile);

        _cursorX += pw;
        _rowHeight = Math.Max(_rowHeight, ph);
        return true;
    }

    /// <summary>Draws the given entries' sprites into their slots. Call while drawing (inside a control's Draw).</summary>
    public void Draw(DrawingHandleScreen handle, EntityDraw3D[] entries, int count, ShaderInstance glowShader, ShaderInstance glassShader)
    {
        if (_target == null)
            return;

        handle.RenderInRenderTarget(_target, () =>
        {
            for (var i = 0; i < count; i++)
            {
                ref var e = ref entries[i];
                if (!e.Placed)
                    continue;

                var pos = new Vector2(e.Slot.Left, e.Slot.Top) + e.SlotOrigin;
                if (e.Impostor != null)
                {
                    handle.DrawTextureRect(e.Impostor, UIBox2.FromDimensions(e.Slot.Left, e.Slot.Top, e.Slot.Width, e.Slot.Height));
                    continue;
                }

                try
                {
                    if (e.Glass is { } glass)
                    {
                        DrawGlass(handle, ref e, glass, glassShader);
                        continue;
                    }

                    handle.DrawEntity(
                        e.Uid,
                        pos,
                        Vector2.One,
                        e.DrawRotated ? null : Angle.Zero,
                        Angle.Zero,
                        e.DrawDirection,
                        e.Sprite);
                }
                catch (Exception)
                {
                    // An entity can be deleted between gathering and drawing; skip it.
                    e.Placed = false;
                }
            }
        }, Color.Transparent);

        if (_glow == null)
            return;

        // Second pass: only the unshaded layers (the glow shader discards the rest), same slots.
        handle.RenderInRenderTarget(_glow, () =>
        {
            handle.UseShader(glowShader);
            for (var i = 0; i < count; i++)
            {
                ref var e = ref entries[i];
                if (!e.Placed || !e.Glow || e.Impostor != null)
                    continue;

                var pos = new Vector2(e.Slot.Left, e.Slot.Top) + e.SlotOrigin;
                try
                {
                    handle.DrawEntity(e.Uid, pos, Vector2.One, e.DrawRotated ? null : Angle.Zero, Angle.Zero, e.DrawDirection, e.Sprite);
                }
                catch (Exception)
                {
                    // deleted mid-frame; the main pass already skipped or drew it
                }
            }

            handle.UseShader(null);
        }, Color.Transparent);
    }

    private readonly List<(UIBox2 Rect, GlassPart Part)> _glassParts = new();

    /// <summary>
    ///     Draws a window, window door or grille as glass: the rectangles of its <see cref="GlassLook"/> written straight
    ///     into the slot (no blending, so the pane keeps its low alpha as it is), then the layers of the sprite above the
    ///     first one, which carry the cracks of a damaged window.
    /// </summary>
    private void DrawGlass(DrawingHandleScreen handle, ref EntityDraw3D e, GlassLook glass, ShaderInstance glassShader)
    {
        var slot = e.Slot;
        glass.Layout(slot.Width, slot.Height, _glassParts);

        var pane = glass.Tint.WithAlpha(glass.Alpha);
        var shine = Color.InterpolateBetween(glass.Tint, Color.White, 0.65f).WithAlpha(Math.Min(1f, glass.Alpha + 0.3f));
        handle.UseShader(glassShader);
        foreach (var (rect, part) in _glassParts)
        {
            var color = part switch
            {
                GlassPart.Pane => pane,
                GlassPart.Frame => glass.Frame,
                GlassPart.Mesh => glass.MeshColor,
                _ => shine,
            };
            handle.DrawRect(new UIBox2(slot.Left + rect.Left, slot.Top + rect.Top, slot.Left + rect.Right, slot.Top + rect.Bottom), color);
        }

        handle.UseShader(null);

        var whole = new UIBox2(slot.Left, slot.Top, slot.Right, slot.Bottom);
        var first = true;
        foreach (var layer in e.Sprite.AllLayers)
        {
            if (first)
            {
                first = false;
                continue;
            }

            if (!layer.Visible)
                continue;

            var texture = layer.Texture;
            if (texture == null && layer.ActualRsi is { } rsi && rsi.TryGetState(layer.RsiState, out var state))
                texture = state.GetFrame(RsiDirection.South, layer.AnimationFrame);

            if (texture != null)
                handle.DrawTextureRect(texture, whole, layer.Color);
        }
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
        _glow?.Dispose();
        _glow = null;
    }
}
