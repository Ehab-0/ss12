using Content.Shared.Tag;
using Robust.Shared.GameObjects;

namespace Content.Client.Render3D;

/// <summary>
///     Questions about an entity that different SS14 codebases answer with different types. Components that were moved
///     between namespaces, or did not exist yet in older codebases, are looked up by their registered name at runtime
///     instead of being referenced at compile time, so one copy of the 3D code compiles against all of them.
/// </summary>
public sealed class Render3DCompat
{
    private static readonly string WallTagId = "Wall";

    private readonly IEntityManager _entMan;
    private TagSystem? _tags;
    private readonly Type? _wallType;
    private readonly Type? _ghostType;

    public Render3DCompat(IEntityManager entMan)
    {
        _entMan = entMan;
        var factory = entMan.ComponentFactory;
        _wallType = factory.TryGetRegistration("Wall", out var wall) ? wall.Type : null;
        _ghostType = factory.TryGetRegistration("Ghost", out var ghost) ? ghost.Type : null;
    }

    /// <summary>
    ///     A wall that becomes a solid block in the 3D tile map: the <c>Wall</c> component where the codebase has one,
    ///     otherwise the <c>Wall</c> tag (older codebases).
    /// </summary>
    public bool IsWall(EntityUid uid)
    {
        if (_wallType != null)
            return _entMan.HasComponent(uid, _wallType);

        _tags ??= _entMan.System<TagSystem>();
        return _tags.HasTag(uid, WallTagId);
    }

    /// <summary>An observer/ghost: always allowed to use the 2D view.</summary>
    public bool IsGhost(EntityUid uid) => _ghostType != null && _entMan.HasComponent(uid, _ghostType);
}
