using System.Numerics;
using Content.Client.StatusIcon;
using Content.Shared.Render3D;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

// Status icons (sec/med HUD, job icons...) normally come from a world-space overlay that only draws into the 2D
// ground viewport. In 3D we redraw them in screen space above every visible entity, using the same
// StatusIconSystem queries (docs/ss12/PLAN.md Phase 5): no rules are duplicated.
public sealed partial class Render3DViewportControl
{
    private StatusIconSystem? _statusIcons;
    private SpriteSystem? _spriteSys;
    private EntityQuery<StatusIconComponent> _statusQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private bool _queriesReady;

    private const float IconPixelsPerTile = GroundLayer.PixelsPerTile;
    private const int MaxIconsPerEntity = 6;

    private void DrawStatusIcons(DrawingHandleScreen screen)
    {
        if (_entityPass == null || _tileWorld == null)
            return;

        _statusIcons ??= _entMan.System<StatusIconSystem>();
        _spriteSys ??= _entMan.System<SpriteSystem>();
        if (!_queriesReady)
        {
            _queriesReady = true;
            _statusQuery = _entMan.GetEntityQuery<StatusIconComponent>();
            _metaQuery = _entMan.GetEntityQuery<MetaDataComponent>();
        }

        var cam = Camera;
        var wallHeight = _cfg.GetCVar(Content.Shared.CCVar.CCVars.Render3DWallHeight);
        var sceneToControl = new Vector2(PixelSize.X, PixelSize.Y) / cam.Size;
        var now = _timing.RealTime;
        var draws = _entityPass.DrawArray;

        for (var i = 0; i < _entityPass.DrawCount; i++)
        {
            ref readonly var e = ref draws[i];
            if (!e.Placed || !_statusQuery.HasComp(e.Uid) || !_metaQuery.TryComp(e.Uid, out var meta))
                continue;

            var top = e.Mode == Render3DMode.Billboard ? e.Bounds.Height : 0.3f;
            var anchor = new Vector3(e.Pos, e.Z + top + 0.05f);
            if (!cam.Project(anchor, out var scenePx, out var depth))
                continue;

            // Hide icons of entities behind walls so the HUD can't be used to see through them.
            var toTarget = anchor - cam.Position;
            if (_lastMap != Robust.Shared.Map.MapId.Nullspace
                && _tileWorld.RayCastWalls(_lastMap, cam.Position, toTarget, 0.97f, wallHeight, out _, out _, out _, out _))
            {
                continue;
            }

            var ppt0 = PixelSize.Y * 0.5f / (cam.TanHalfFov * depth);
            DrawHealthBar(screen, e.Uid, scenePx * sceneToControl, ppt0);

            var icons = _statusIcons.GetStatusIcons(e.Uid, meta);
            if (icons.Count == 0)
                continue;

            icons.Sort();

            // pixels per tile at this depth, then icon sizes keep their 2D proportions
            var ppt = PixelSize.Y * 0.5f / (cam.TanHalfFov * depth);
            var scale = ppt / IconPixelsPerTile;
            if (scale > MaxIconScale)
                scale = MaxIconScale; // the own character in third person is only ~1.5 tiles away
            if (scale < 0.15f || depth > IconFadeEnd)
                continue;

            var fade = Math.Clamp((IconFadeEnd - depth) / (IconFadeEnd - IconFadeStart), 0f, 1f);
            var tint = new Color(1f, 1f, 1f, fade);

            var textures = new List<Texture>(MaxIconsPerEntity);
            foreach (var proto in icons)
            {
                if (textures.Count >= MaxIconsPerEntity)
                    break;
                if (!_statusIcons.IsVisible((e.Uid, meta), proto))
                    continue;
                textures.Add(_spriteSys.GetFrame(proto.Icon, now));
            }

            if (textures.Count == 0)
                continue;

            var total = 0f;
            foreach (var t in textures)
                total += t.Width * scale;

            var pos = scenePx * sceneToControl;
            var x = pos.X - total / 2f;
            var y = pos.Y - 4f;
            foreach (var t in textures)
            {
                var w = t.Width * scale;
                var h = t.Height * scale;
                screen.DrawTextureRect(t, UIBox2.FromDimensions(x, y - h, w, h), tint);
                x += w;
            }
        }
    }

    private const float MaxIconScale = 1.3f;

    /// <summary>Icons fade out between these distances (tiles) so a crowd far away does not fill the screen with them.</summary>
    private const float IconFadeStart = 7f;
    private const float IconFadeEnd = 12f;

    private IRender3DHealthBar? _healthBars;

    /// <summary>
    ///     Redraws the world-space health bar (<c>showhealthbars</c>, med HUDs) under the same rules, in screen space.
    ///     Needs the optional installer edit that makes <c>EntityHealthBarOverlay</c> implement
    ///     <see cref="IRender3DHealthBar"/>; without it no bars are drawn in 3D.
    /// </summary>
    private void DrawHealthBar(DrawingHandleScreen screen, EntityUid uid, Vector2 headPx, float pixelsPerTile)
    {
        if (_healthBars == null)
        {
            foreach (var overlay in _overlays.AllOverlays)
            {
                if (overlay is IRender3DHealthBar bar)
                {
                    _healthBars = bar;
                    break;
                }
            }

            if (_healthBars == null)
                return;
        }

        // The overlay is removed when the player turns health bars off; forget it and look again next frame.
        if (_healthBars is Overlay held && !_overlays.HasOverlay(held.GetType()))
        {
            _healthBars = null;
            return;
        }

        if (!_healthBars.TryGetBar(uid, out var ratio, out var color))
            return;

        var width = Math.Clamp(0.5f * pixelsPerTile, 12f, 110f);
        var height = Math.Clamp(0.045f * pixelsPerTile, 2f, 7f);
        var left = headPx.X - width / 2f;
        var top = headPx.Y + 2f;
        screen.DrawRect(UIBox2.FromDimensions(left, top, width, height), new Color(0, 0, 0, 0.75f));
        screen.DrawRect(UIBox2.FromDimensions(left, top, width * Math.Clamp(ratio, 0f, 1f), height), color);
    }
}
