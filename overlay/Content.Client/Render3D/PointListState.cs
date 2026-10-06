using Robust.Shared.GameObjects;

namespace Content.Client.Render3D;

/// <summary>
///     The state of the list of what the crosshair points at: the entries, which one is highlighted (moved with the up and down
///     keys) and which one is the target the player has chosen. Free of any control, so it can be tested. The highlight follows an
///     entity, not a row, because the list changes as the view moves.
/// </summary>
public sealed class PointListState
{
    public readonly List<(EntityUid Uid, string Text, bool InReach)> Entries = new();

    /// <summary>The highlighted row, or -1.</summary>
    public int Highlight { get; private set; } = -1;

    /// <summary>The entity the player chose from the list; what the crosshair acts on until it is cleared.</summary>
    public EntityUid? Target { get; private set; }

    public void SetEntries(IEnumerable<(EntityUid Uid, string Text, bool InReach)> entries)
    {
        EntityUid? keep = Highlight >= 0 && Highlight < Entries.Count ? Entries[Highlight].Uid : null;
        Entries.Clear();
        Entries.AddRange(entries);

        Highlight = -1;
        if (keep is { } uid)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Uid != uid)
                    continue;

                Highlight = i;
                break;
            }
        }
    }

    /// <summary>Moves the highlight by one row, wrapping round; from nothing the first move down is the first row, up the last.</summary>
    public void Move(int delta)
    {
        var n = Entries.Count;
        if (n == 0)
        {
            Highlight = -1;
            return;
        }

        if (Highlight < 0)
        {
            Highlight = delta >= 0 ? 0 : n - 1;
            return;
        }

        Highlight = ((Highlight + delta) % n + n) % n;
    }

    /// <summary>Chooses the highlighted entity as the target, or clears the target when it already is. False when nothing is highlighted.</summary>
    public bool SelectHighlighted()
    {
        if (Highlight < 0 || Highlight >= Entries.Count)
            return false;

        Toggle(Entries[Highlight].Uid);
        return true;
    }

    /// <summary>Chooses this entity as the target, or clears the target when it already is.</summary>
    public void Toggle(EntityUid uid)
    {
        Target = Target == uid ? null : uid;
    }

    public void ClearTarget() => Target = null;

    public void Clear()
    {
        Entries.Clear();
        Highlight = -1;
        Target = null;
    }
}
