using System.Numerics;
using Content.Shared.Pinpointer;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Render3D;

/// <summary>
///     A small map of the station in a corner of the 3D view, to find the way: floors, walls and doors from the grid's navigation
///     map data (the game sends it to every client for the station map on the wall), centred on the player. It shows nothing
///     else, in particular no other players, no items and nothing the server did not already tell every client. A large mode shows
///     more of the station and the names of its areas (the beacons).
/// </summary>
public sealed partial class Render3DMinimap : Control
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _resCache = default!;

    private static readonly Color Background = new(0.02f, 0.04f, 0.05f, 0.62f);
    private static readonly Color Border = new(0.55f, 0.65f, 0.7f, 0.9f);
    private static readonly Color FloorColor = new(0.17f, 0.25f, 0.27f);
    private static readonly Color WallColor = new(0.86f, 0.9f, 0.93f);
    private static readonly Color ThinWallColor = new(0.55f, 0.62f, 0.66f);
    private static readonly Color AirlockColor = new(0.95f, 0.65f, 0.2f);
    private static readonly Color SelfColor = new(0.3f, 0.9f, 1f);

    private const float Overlap = 0.03f;

    /// <summary>The grid the player is on (null: nothing to show).</summary>
    public EntityUid? Grid;

    /// <summary>The player in the local space of the grid, in tiles.</summary>
    public Vector2 PlayerLocal;

    /// <summary>The direction that is up on the screen, in the local space of the grid (unit length).</summary>
    public Vector2 Up = Vector2.UnitY;

    /// <summary>The way the player faces, in the local space of the grid (unit length).</summary>
    public Vector2 Facing = Vector2.UnitY;

    /// <summary>North in the local space of the grid (unit length), for the "N" mark when the map turns.</summary>
    public Vector2 North = Vector2.UnitY;

    /// <summary>Tiles from the player to the edge of the map.</summary>
    public float Radius = 20f;

    /// <summary>The large map: names of the areas are shown too.</summary>
    public bool ShowLabels;

    /// <summary>Show an "N" where north is (only meaningful when the map turns with the camera).</summary>
    public bool ShowNorth = true;

    private Font? _font;
    private readonly Vector2[] _arrow = new Vector2[3];

    public Render3DMinimap()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Background);

        if (Grid is { } grid && _entMan.TryGetComponent<NavMapComponent>(grid, out var nav))
            DrawMap(handle, nav, new Vector2(size.X, size.Y));

        DrawArrowAndNorth(handle, new Vector2(size.X, size.Y));
        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), Border, false);
    }

    private void DrawMap(DrawingHandleScreen handle, NavMapComponent nav, Vector2 size)
    {
        var centre = size / 2f;
        var scale = MinimapView.PixelsPerTile(MathF.Min(size.X, size.Y), Radius);
        // a control draws with its own position as the transform: the map transform goes on top of that, and it is put back
        // (not reset to nothing, which would draw the rest at the corner of the window) afterwards
        var controlOrigin = Matrix3x2.CreateTranslation(new Vector2(GlobalPixelPosition.X, GlobalPixelPosition.Y));
        handle.SetTransform(MinimapView.Matrix(PlayerLocal, Up, centre, scale) * controlOrigin);

        const int chunk = SharedNavMapSystem.ChunkSize;
        foreach (var data in nav.Chunks.Values)
        {
            var origin = data.Origin * chunk;
            var chunkCentre = new Vector2(origin.X + chunk / 2f, origin.Y + chunk / 2f);
            if (!MinimapView.InRange((chunkCentre - PlayerLocal).Length(), chunk, Radius))
                continue;

            for (var i = 0; i < SharedNavMapSystem.ArraySize; i++)
            {
                var bits = data.TileData[i];
                if (bits == 0)
                    continue;

                Color color;
                if ((bits & SharedNavMapSystem.AirlockMask) != 0)
                    color = AirlockColor;
                else if ((bits & SharedNavMapSystem.WallMask) == SharedNavMapSystem.WallMask)
                    color = WallColor;
                else if ((bits & SharedNavMapSystem.WallMask) != 0)
                    color = ThinWallColor;
                else if ((bits & SharedNavMapSystem.FloorMask) != 0)
                    color = FloorColor;
                else
                    continue;

                var tile = origin + SharedNavMapSystem.GetTileFromIndex(i);
                handle.DrawRect(new UIBox2(tile.X - Overlap, tile.Y - Overlap, tile.X + 1 + Overlap, tile.Y + 1 + Overlap), color);
            }
        }

        handle.SetTransform(controlOrigin);

        if (!ShowLabels)
            return;

        _font ??= new VectorFont(_resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
        var matrix = MinimapView.Matrix(PlayerLocal, Up, centre, scale);
        foreach (var beacon in nav.Beacons.Values)
        {
            if (string.IsNullOrEmpty(beacon.Text))
                continue;

            var screen = Vector2.Transform(beacon.Position, matrix);
            if (screen.X < 6 || screen.Y < 6 || screen.X > size.X - 6 || screen.Y > size.Y - 6)
                continue;

            var dims = handle.GetDimensions(_font, beacon.Text, 1f);
            var at = screen - dims / 2f;
            handle.DrawRect(UIBox2.FromDimensions(at - new Vector2(2, 1), dims + new Vector2(4, 2)), new Color(0f, 0f, 0f, 0.55f));
            handle.DrawString(_font, at, beacon.Text, Color.White);
        }
    }

    private void DrawArrowAndNorth(DrawingHandleScreen handle, Vector2 size)
    {
        var centre = size / 2f;
        var dir = MinimapView.ScreenDirection(Facing, Up);
        if (dir.LengthSquared() < 0.0001f)
            dir = -Vector2.UnitY;

        dir = Vector2.Normalize(dir);
        var side = new Vector2(-dir.Y, dir.X);
        _arrow[0] = centre + dir * 9f;
        _arrow[1] = centre - dir * 6f + side * 5.5f;
        _arrow[2] = centre - dir * 6f - side * 5.5f;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _arrow, SelfColor);

        if (!ShowNorth)
            return;

        var north = MinimapView.ScreenDirection(North, Up);
        if (north.LengthSquared() < 0.0001f)
            return;

        north = Vector2.Normalize(north);
        _font ??= new VectorFont(_resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);
        var dims = handle.GetDimensions(_font, "N", 1f);
        var reach = MathF.Min(size.X, size.Y) / 2f - 10f;
        handle.DrawString(_font, centre + north * reach - dims / 2f, "N", new Color(1f, 0.55f, 0.45f));
    }
}
