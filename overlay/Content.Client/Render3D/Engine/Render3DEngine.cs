using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Client.Render3D;

/// <summary>
///     The engine (RobustToolbox) APIs that differ between the engine versions the 3D view supports (264 and newer).
///     <para>
///         This file has one version for every engine. The other parts (<c>Render3DEngine.*.cs</c>) are what the newest
///         engines need; the SS12 installer replaces a part by its version in <c>Tools/ss12-engine/before-X/</c> when the
///         codebase's engine is older than X. Every version of a part declares the same public members.
///     </para>
/// </summary>
public static partial class Render3DEngine
{
    /// <summary>
    ///     The sprites whose bounds intersect <paramref name="aabb"/>. The callback form of <c>QueryAabb</c> is the same from
    ///     engine 264 to now; the list form only exists from 268.
    /// </summary>
    public static void QuerySprites(SpriteTreeSystem tree, List<Entity<SpriteComponent, TransformComponent>> results, MapId mapId, Box2 aabb)
    {
        tree.QueryAabb(ref results,
            static (ref List<Entity<SpriteComponent, TransformComponent>> state, in ComponentTreeEntry<SpriteComponent> value) =>
            {
                state.Add(new Entity<SpriteComponent, TransformComponent>(value.Uid, value.Component, value.Transform));
                return true;
            },
            mapId,
            aabb);
    }
}
