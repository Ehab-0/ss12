using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Render3D;

// Engines before 278 (installed by the SS12 installer instead of Content.Client/Render3D/Engine/Render3DEngine.Grids.cs).
// SharedMapSystem.FindGridsIntersecting and TryFindGridAt are from 278; before it the same queries are on IMapManager
// (which 280 removed, so it cannot be named in the version for newer engines).
public static partial class Render3DEngine
{
    public static void FindGrids(SharedMapSystem map, MapId mapId, Box2 aabb, ref List<Entity<MapGridComponent>> grids)
    {
        IoCManager.Resolve<IMapManager>().FindGridsIntersecting(mapId, aabb, ref grids);
    }

    public static bool TryFindGrid(SharedMapSystem map, MapId mapId, Vector2 point, out EntityUid uid, [NotNullWhen(true)] out MapGridComponent? grid)
    {
        return IoCManager.Resolve<IMapManager>().TryFindGridAt(mapId, point, out uid, out grid);
    }
}
