using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Render3D;

// Engine 278 and newer. The version for older engines is Tools/ss12-engine/before-278/Content.Client/Render3D/Engine/Render3DEngine.Grids.cs.
public static partial class Render3DEngine
{
    public static void FindGrids(SharedMapSystem map, MapId mapId, Box2 aabb, ref List<Entity<MapGridComponent>> grids)
    {
        map.FindGridsIntersecting(mapId, aabb, ref grids);
    }

    public static bool TryFindGrid(SharedMapSystem map, MapId mapId, Vector2 point, out EntityUid uid, [NotNullWhen(true)] out MapGridComponent? grid)
    {
        return map.TryFindGridAt(mapId, point, out uid, out grid);
    }
}
