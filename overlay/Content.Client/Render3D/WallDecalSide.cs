namespace Content.Client.Render3D;

/// <summary>Which side of a wall mounted entity its wall is on.</summary>
public enum DecalSide
{
    /// <summary>The wall is behind the entity (the way posters, buttons and lamps are rotated).</summary>
    Behind,

    /// <summary>The entity stands in a wall tile itself.</summary>
    Here,

    /// <summary>The wall is in front of the entity, in the direction it is rotated (security cameras are rotated this way).</summary>
    Ahead,

    /// <summary>No wall next to it (it hangs on a window, a grille or a door, which are not in the wall map).</summary>
    None,
}

/// <summary>
///     Decides where a wall mounted entity hangs. Things were only ever looked for a wall behind them; a security camera is
///     rotated to point at its wall, so its wall was never found and it was drawn at the open edge of its tile, floating a tile
///     away from the wall it belongs to.
/// </summary>
public static class WallDecalSide
{
    public static DecalSide Choose(bool wallBehind, bool wallHere, bool wallAhead)
    {
        if (wallBehind && !wallHere)
            return DecalSide.Behind;

        if (wallHere)
            return DecalSide.Here;

        return wallAhead ? DecalSide.Ahead : DecalSide.None;
    }
}
