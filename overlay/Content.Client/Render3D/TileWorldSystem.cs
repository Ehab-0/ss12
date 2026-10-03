using System.Numerics;
using Content.Shared.Wall;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Render3D;

/// <summary>
///     Tracks every wall on every grid and packs the walls and floor tiles around the camera into a small lookup
///     texture (one window of cells per grid, up to <see cref="MaxGrids"/> grids) that the raymarch shader walks.
///     The same wall data backs the CPU raycasts used for picking and the third-person spring arm.
/// </summary>
public sealed partial class TileWorldSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    public const int MaxGrids = 4;

    /// <summary>Cells per window side. Must be at least 2 * ground radius + margin.</summary>
    public const int WindowSize = 64;

    private const int AtlasSide = 2 * WindowSize;

    /// <summary>The camera may drift this many cells from the window centre before the window is rebuilt.</summary>
    private const int RebuildDistance = 6;

    private readonly Dictionary<EntityUid, GridData> _grids = new();
    private OwnedTexture? _tileMapTexture;
    private WallAtlas? _wallAtlas;

    private readonly Rgba32[] _scratch = new Rgba32[WindowSize * WindowSize];
    private List<Entity<MapGridComponent>> _gridScratch = new();

    public WallAtlas WallAtlas => _wallAtlas ??= new WallAtlas(_clyde, _protos, _sprite);

    public Texture TileMapTexture => EnsureTileMap();

    private OwnedTexture EnsureTileMap()
    {
        {
            if (_tileMapTexture == null)
            {
                _tileMapTexture = _clyde.CreateBlankTexture<Rgba32>(
                    new Vector2i(AtlasSide, AtlasSide),
                    "render3d-tilemap",
                    new TextureLoadParameters
                    {
                        Srgb = false,
                        Preload = false,
                        SampleParameters = new TextureSampleParameters { Filter = false },
                    });
                // Unused window slots must read as "empty": upload zeros once.
                var zeros = new Rgba32[AtlasSide * AtlasSide];
                _tileMapTexture.SetSubImage(Vector2i.Zero, new Vector2i(AtlasSide, AtlasSide), (ReadOnlySpan<Rgba32>) zeros);
            }

            return _tileMapTexture;
        }
    }

    public int DebugWallCount
    {
        get
        {
            var n = 0;
            foreach (var g in _grids.Values)
                n += g.Walls.Count;
            return n;
        }
    }

    /// <summary>Incremented whenever walls change anywhere; consumers can use it to invalidate caches.</summary>
    public int WallVersion { get; private set; }

    /// <summary>Tiles (grid, grid-local tile) holding a closed door this frame; refilled by the entity pass.</summary>
    public readonly HashSet<(EntityUid Grid, Vector2i Tile)> ClosedDoors = new();

    /// <summary>Tiles with a full window block (filled by the entity pass each frame). Like closed doors they block the camera arm.</summary>
    public readonly HashSet<(EntityUid Grid, Vector2i Tile)> GlassTiles = new();

    /// <summary>The grids chosen for the current frame, with their shader parameters. Filled by <see cref="UpdateWindows"/>.</summary>
    public readonly List<GridShaderData> ActiveGrids = new();

    private sealed class GridData
    {
        public readonly Dictionary<Vector2i, (EntityUid Wall, int Slot)> Walls = new();
        public bool Dirty = true;

        /// <summary>Atlas slot (0..3) this grid's window occupies, or -1.</summary>
        public int AtlasSlot = -1;

        public Vector2i WindowOrigin;
        public bool WindowValid;
        public TimeSpan NextScan;
    }

    public struct GridShaderData
    {
        public EntityUid Grid;
        public Vector4 A;
        public Vector4 B;
        public Vector4 C;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _tileMapTexture?.Dispose();
        _tileMapTexture = null;
        _wallAtlas?.Dispose();
        _wallAtlas = null;
        _grids.Clear();
        ActiveGrids.Clear();
    }

    // ---- wall tracking ----
    //
    // Walls are found by scanning the anchored entities of the tiles around the camera a few times a second, not by
    // component events. The engine allows only one subscriber per component lifecycle event, and codebases differ in
    // what marks a wall (a Wall component, or just a tag), so scanning with Render3DCompat.IsWall is what keeps one
    // copy of this code working in all of them. The scan covers exactly the area the lookup texture covers.

    /// <summary>How often the walls around the camera are re-scanned for construction and destruction.</summary>
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(400);

    private Render3DCompat? _compat;
    private readonly HashSet<Vector2i> _scanSeen = new();
    private readonly List<Vector2i> _scanRemove = new();

    private bool IsWall(EntityUid uid) => (_compat ??= new Render3DCompat(EntityManager)).IsWall(uid);

    /// <summary>Refreshes <see cref="GridData.Walls"/> for the window at <paramref name="origin"/>. True when it changed.</summary>
    private bool ScanWalls(Entity<MapGridComponent> grid, GridData data, Vector2i origin)
    {
        var changed = false;
        _scanSeen.Clear();

        for (var y = 0; y < WindowSize; y++)
        {
            for (var x = 0; x < WindowSize; x++)
            {
                var tile = new Vector2i(origin.X + x, origin.Y + y);
                var anchored = _map.GetAnchoredEntitiesEnumerator(grid.Owner, grid.Comp, tile);
                while (anchored.MoveNext(out var found))
                {
                    var uid = found.Value;
                    if (!IsWall(uid))
                        continue;

                    var slot = WallAtlas.GetSlot(MetaData(uid).EntityPrototype?.ID);
                    _scanSeen.Add(tile);
                    if (!data.Walls.TryGetValue(tile, out var old) || old.Wall != uid || old.Slot != slot)
                    {
                        data.Walls[tile] = (uid, slot);
                        changed = true;
                    }

                    break;
                }
            }
        }

        // walls that were in the window and are gone, or that left the window, no longer count
        _scanRemove.Clear();
        foreach (var tile in data.Walls.Keys)
        {
            if (!_scanSeen.Contains(tile))
                _scanRemove.Add(tile);
        }

        foreach (var tile in _scanRemove)
        {
            data.Walls.Remove(tile);
            changed = true;
        }

        if (changed)
            WallVersion++;

        return changed;
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            if (_grids.TryGetValue(args.Entity, out var data))
                data.Dirty = true;
            break;
        }

        WallVersion++;
    }

    private void OnGridRemoved(GridRemovalEvent args)
    {
        _grids.Remove(args.EntityUid);
    }

    private GridData GetGridData(EntityUid grid)
    {
        if (!_grids.TryGetValue(grid, out var data))
            _grids[grid] = data = new GridData();
        return data;
    }

    // ---- per-frame windows ----

    /// <summary>
    ///     Chooses the grids to raymarch around <paramref name="camera"/> (map position) and refreshes their window
    ///     in the lookup texture when the camera moved far enough or something changed.
    /// </summary>
    public void UpdateWindows(MapId mapId, Vector2 camera, float radius)
    {
        ActiveGrids.Clear();
        if (mapId == MapId.Nullspace)
            return;

        var aabb = Box2.CenteredAround(camera, new Vector2(radius * 2f, radius * 2f));
        _gridScratch.Clear();
        _map.FindGridsIntersecting(mapId, aabb, ref _gridScratch);
        if (_gridScratch.Count == 0)
            return;

        // The grid under the camera first, then nearest first.
        _gridScratch.Sort((a, b) => DistanceToGrid(a, camera).CompareTo(DistanceToGrid(b, camera)));

        var count = Math.Min(_gridScratch.Count, MaxGrids);
        var used = new bool[MaxGrids];

        // Keep grids on their previous atlas slot when possible so windows don't have to be re-uploaded.
        for (var i = 0; i < count; i++)
        {
            var data = GetGridData(_gridScratch[i].Owner);
            if (data.AtlasSlot >= 0 && !used[data.AtlasSlot])
                used[data.AtlasSlot] = true;
            else
                data.AtlasSlot = -1;
        }

        for (var i = 0; i < count; i++)
        {
            var gridEnt = _gridScratch[i];
            var data = GetGridData(gridEnt.Owner);

            if (data.AtlasSlot < 0)
            {
                for (var s = 0; s < MaxGrids; s++)
                {
                    if (used[s])
                        continue;
                    data.AtlasSlot = s;
                    used[s] = true;
                    data.WindowValid = false;
                    break;
                }
            }

            var inv = _xform.GetInvWorldMatrix(gridEnt.Owner);
            var local = Vector2.Transform(camera, inv);
            var centerCell = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));

            var moved = !data.WindowValid
                || Math.Abs(centerCell.X - (data.WindowOrigin.X + WindowSize / 2)) > RebuildDistance
                || Math.Abs(centerCell.Y - (data.WindowOrigin.Y + WindowSize / 2)) > RebuildDistance;

            if (moved)
                data.WindowOrigin = centerCell - new Vector2i(WindowSize / 2, WindowSize / 2);

            // construction and destruction of walls is noticed by a periodic scan (and always before a new window is built)
            var now = _timing.RealTime;
            if (moved || now >= data.NextScan)
            {
                data.NextScan = now + ScanInterval;
                if (ScanWalls(gridEnt, data, data.WindowOrigin))
                    data.Dirty = true;
            }

            var rebuild = data.Dirty || moved;

            if (rebuild)
            {
                RebuildWindow(gridEnt, data);
                data.Dirty = false;
                data.WindowValid = true;
            }

            var slotX = (data.AtlasSlot % 2) * WindowSize;
            var slotY = (data.AtlasSlot / 2) * WindowSize;

            ActiveGrids.Add(new GridShaderData
            {
                Grid = gridEnt.Owner,
                // Matrix3x2 row-vector convention: x' = x*M11 + y*M21 + M31, y' = x*M12 + y*M22 + M32
                A = new Vector4(inv.M11, inv.M12, inv.M21, inv.M22),
                B = new Vector4(inv.M31, inv.M32, data.WindowOrigin.X, data.WindowOrigin.Y),
                C = new Vector4(slotX, slotY, WindowSize, 0f),
            });
        }
    }

    private float DistanceToGrid(Entity<MapGridComponent> grid, Vector2 camera)
    {
        var inv = _xform.GetInvWorldMatrix(grid.Owner);
        var local = Vector2.Transform(camera, inv);
        var box = grid.Comp.LocalAABB;
        var dx = MathF.Max(MathF.Max(box.Left - local.X, 0f), local.X - box.Right);
        var dy = MathF.Max(MathF.Max(box.Bottom - local.Y, 0f), local.Y - box.Top);
        return dx * dx + dy * dy;
    }

    private void RebuildWindow(Entity<MapGridComponent> gridEnt, GridData data)
    {
        var origin = data.WindowOrigin;
        var span = _scratch.AsSpan();

        for (var y = 0; y < WindowSize; y++)
        {
            for (var x = 0; x < WindowSize; x++)
            {
                var tile = new Vector2i(origin.X + x, origin.Y + y);
                byte wall = 0;
                byte exists = 0;

                if (_map.TryGetTileRef(gridEnt.Owner, gridEnt.Comp, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
                    exists = 255;

                if (data.Walls.TryGetValue(tile, out var w))
                {
                    wall = (byte) w.Slot;
                    // A wall tile always has floor beneath it for ceiling purposes.
                    exists = 255;
                }

                span[y * WindowSize + x] = new Rgba32(wall, exists, 0, 255);
            }
        }

        var slotX = (data.AtlasSlot % 2) * WindowSize;
        var slotY = (data.AtlasSlot / 2) * WindowSize;
        EnsureTileMap().SetSubImage(new Vector2i(slotX, slotY), new Vector2i(WindowSize, WindowSize), (ReadOnlySpan<Rgba32>) _scratch);
    }

    // ---- CPU queries ----

    /// <summary>True if the (map-space) point lies inside a wall cell of any grid.</summary>
    public bool IsWallAt(MapId mapId, Vector2 point)
    {
        foreach (var (grid, data) in _grids)
        {
            if (data.Walls.Count == 0 || !TryComp(grid, out TransformComponent? xform) || xform.MapID != mapId)
                continue;

            var local = Vector2.Transform(point, _xform.GetInvWorldMatrix(grid));
            if (data.Walls.ContainsKey(new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y))))
                return true;
        }

        return false;
    }

    public bool TryGetWallEntity(EntityUid grid, Vector2i tile, out EntityUid wall)
    {
        if (_grids.TryGetValue(grid, out var data) && data.Walls.TryGetValue(tile, out var entry))
        {
            wall = entry.Wall;
            return true;
        }

        wall = default;
        return false;
    }

    public bool TryGetWall(EntityUid grid, Vector2i tile)
    {
        return _grids.TryGetValue(grid, out var data) && data.Walls.ContainsKey(tile);
    }

    /// <summary>
    ///     Casts a ray (map space, 3D) against wall blocks of height <paramref name="wallHeight"/> on every grid and
    ///     returns the nearest hit distance along <paramref name="dir"/> (which must be un-normalised only if the
    ///     caller wants t in those units).
    /// </summary>
    public bool RayCastWalls(MapId mapId, Vector3 origin, Vector3 dir, float maxT, float wallHeight,
        out float hitT, out Vector2 hitNormal, out EntityUid hitGrid, out Vector2i hitTile, bool includeClosedDoors = false)
    {
        hitT = maxT;
        hitNormal = default;
        hitGrid = default;
        hitTile = default;
        var found = false;

        foreach (var (grid, data) in _grids)
        {
            if (data.Walls.Count == 0 || !TryComp(grid, out TransformComponent? xform) || xform.MapID != mapId)
                continue;

            var inv = _xform.GetInvWorldMatrix(grid);
            var o = Vector2.Transform(new Vector2(origin.X, origin.Y), inv);
            var d = Vector2.TransformNormal(new Vector2(dir.X, dir.Y), inv);

            if (DdaWalls(grid, data, includeClosedDoors, o, d, origin.Z, dir.Z, hitT, wallHeight, out var t, out var nLocal, out var tile))
            {
                if (t < hitT)
                {
                    hitT = t;
                    hitGrid = grid;
                    hitTile = tile;
                    // local normal -> world normal
                    hitNormal = Vector2.TransformNormal(nLocal, _xform.GetWorldMatrix(grid));
                    if (hitNormal.LengthSquared() > 1e-8f)
                        hitNormal = Vector2.Normalize(hitNormal);
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>
    ///     Grid-local 2D DDA through wall cells (<paramref name="isWall"/>), tracking the ray height so rays that pass
    ///     above/below a wall block of height <paramref name="wallHeight"/> don't hit it. Pure and unit tested.
    /// </summary>
    public static bool Dda(Func<Vector2i, bool> isWall, Vector2 o, Vector2 d, float z0, float dz, float tMax, float wallHeight,
        out float tHit, out Vector2 normal, out Vector2i hitTile)
    {
        tHit = 0;
        normal = default;
        hitTile = default;

        var cell = new Vector2i((int) MathF.Floor(o.X), (int) MathF.Floor(o.Y));
        var stepX = d.X >= 0 ? 1 : -1;
        var stepY = d.Y >= 0 ? 1 : -1;
        var adx = MathF.Max(MathF.Abs(d.X), 1e-6f);
        var ady = MathF.Max(MathF.Abs(d.Y), 1e-6f);
        var tDeltaX = 1f / adx;
        var tDeltaY = 1f / ady;
        var nextX = (stepX > 0 ? (cell.X + 1 - o.X) : (o.X - cell.X)) * tDeltaX;
        var nextY = (stepY > 0 ? (cell.Y + 1 - o.Y) : (o.Y - cell.Y)) * tDeltaY;

        // The starting cell counts too if it's a wall (camera pushed inside).
        if (isWall(cell) && z0 >= 0 && z0 <= wallHeight)
        {
            tHit = 0;
            hitTile = cell;
            normal = Vector2.Zero;
            return true;
        }

        for (var i = 0; i < 256; i++)
        {
            float t;
            Vector2 n;
            if (nextX < nextY)
            {
                t = nextX;
                nextX += tDeltaX;
                cell.X += stepX;
                n = new Vector2(-stepX, 0);
            }
            else
            {
                t = nextY;
                nextY += tDeltaY;
                cell.Y += stepY;
                n = new Vector2(0, -stepY);
            }

            if (t > tMax)
                return false;

            if (isWall(cell))
            {
                var z = z0 + dz * t;
                if (z >= 0 && z <= wallHeight)
                {
                    tHit = t;
                    normal = n;
                    hitTile = cell;
                    return true;
                }
            }
        }

        return false;
    }

    private bool DdaWalls(EntityUid grid, GridData data, bool doors, Vector2 o, Vector2 d, float z0, float dz, float tMax, float wallHeight,
        out float tHit, out Vector2 normal, out Vector2i hitTile)
    {
        if (!doors || (ClosedDoors.Count == 0 && GlassTiles.Count == 0))
            return Dda(data.Walls.ContainsKey, o, d, z0, dz, tMax, wallHeight, out tHit, out normal, out hitTile);

        return Dda(t => data.Walls.ContainsKey(t) || ClosedDoors.Contains((grid, t)) || GlassTiles.Contains((grid, t)),
            o, d, z0, dz, tMax, wallHeight, out tHit, out normal, out hitTile);
    }
}
