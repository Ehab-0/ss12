using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.Render3D;

/// <summary>
///     Atlas of wall textures for the raymarcher: one slot per wall prototype, filled from the prototype's icon.
///     Slot 0 is reserved for "no wall"; slots are numbered 1..255 left-to-right, top-to-bottom.
/// </summary>
public sealed class WallAtlas : IDisposable
{
    public const int SlotsPerRow = 16;
    public const int SlotPixels = 32;
    public const int MaxSlots = SlotsPerRow * SlotsPerRow - 1;

    private readonly IClyde _clyde;
    private readonly IPrototypeManager _protos;
    private readonly SpriteSystem _sprite;

    private IRenderTexture? _target;
    private readonly Dictionary<string, int> _slots = new();
    private readonly List<(string Proto, int Slot)> _pending = new();
    private int _next = 1;

    public Texture? Texture => _target?.Texture;

    public WallAtlas(IClyde clyde, IPrototypeManager protos, SpriteSystem sprite)
    {
        _clyde = clyde;
        _protos = protos;
        _sprite = sprite;
    }

    /// <summary>Slot for a wall prototype, allocating (and queueing its icon for drawing) on first use.</summary>
    public int GetSlot(string? protoId)
    {
        if (string.IsNullOrEmpty(protoId))
            return 1;

        if (_slots.TryGetValue(protoId, out var slot))
            return slot;

        if (_next > MaxSlots)
        {
            // Out of slots: reuse the first one rather than crash. Wall variety above 255 prototypes doesn't occur.
            _slots[protoId] = 1;
            return 1;
        }

        slot = _next++;
        _slots[protoId] = slot;
        _pending.Add((protoId, slot));
        return slot;
    }

    /// <summary>Draws queued icons into the atlas. Must be called while drawing (inside a control's Draw).</summary>
    public void Flush(DrawingHandleScreen handle)
    {
        if (_target == null)
        {
            var size = SlotsPerRow * SlotPixels;
            _target = _clyde.CreateRenderTarget(
                new Vector2i(size, size),
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = false },
                "render3d-wall-atlas");

            // Everything pending is (re)drawn into a fresh target, including slots from before it was created.
            _pending.Clear();
            foreach (var (proto, slot) in _slots)
            {
                _pending.Add((proto, slot));
            }
        }

        if (_pending.Count == 0)
            return;

        var pending = _pending.ToArray();
        _pending.Clear();

        // The atlas is persistent: draw only the new slots on top of the existing contents (no clear).
        handle.RenderInRenderTarget(_target, () =>
        {
            foreach (var (protoId, slot) in pending)
            {
                if (!_protos.TryIndex<EntityPrototype>(protoId, out var proto))
                    continue;

                Texture tex;
                try
                {
                    tex = _sprite.Frame0(proto);
                }
                catch (Exception)
                {
                    continue;
                }

                var col = slot % SlotsPerRow;
                var row = slot / SlotsPerRow;
                var dest = UIBox2.FromDimensions(new Vector2(col * SlotPixels, row * SlotPixels), new Vector2(SlotPixels, SlotPixels));
                handle.DrawTextureRect(tex, dest, Color.White);
            }
        }, null);
    }

    public void Dispose()
    {
        _target?.Dispose();
        _target = null;
        _slots.Clear();
        _pending.Clear();
        _next = 1;
    }
}
