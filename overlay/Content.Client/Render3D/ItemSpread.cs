using System.Numerics;

namespace Content.Client.Render3D;

/// <summary>
///     Spreads items that lie on top of each other apart, inside their tile, only in the picture. A pile of clothes, a tray of
///     tools or the contents of an emptied box all land on the same spot, so each hid the one below it. The positions are in the
///     local space of the grid (one unit is one tile), the result is an offset to add to each position. The same input always
///     gives the same output, so nothing shuffles from one frame to the next.
/// </summary>
public static class ItemSpread
{
    /// <summary>Items closer than this (tiles) are spread apart.</summary>
    public const float MinSeparation = 0.24f;

    /// <summary>An item is kept at least this far (tiles) from the edge of its tile.</summary>
    public const float EdgeMargin = 0.12f;

    private static readonly float[] Rings = { 0.14f, 0.26f, 0.36f };

    /// <summary>
    ///     Computes how far each of the items of ONE tile is moved. <paramref name="keys"/> orders the items (and picks the
    ///     direction each one is pushed in), so it has to be stable for an item, such as its entity id.
    /// </summary>
    public static void Compute(ReadOnlySpan<Vector2> positions, ReadOnlySpan<int> keys, Span<Vector2> offsets)
    {
        var n = positions.Length;
        for (var i = 0; i < n; i++)
            offsets[i] = Vector2.Zero;

        if (n < 2)
            return;

        var order = new int[n];
        for (var i = 0; i < n; i++)
            order[i] = i;

        var keyArray = keys.ToArray(); // a span cannot be captured by the comparison below
        Array.Sort(order, (a, b) => keyArray[a].CompareTo(keyArray[b]));

        var placed = new Vector2[n];
        var count = 0;
        foreach (var i in order)
        {
            var home = positions[i];
            var best = home;
            var bestScore = Nearest(placed, count, home);
            if (bestScore < MinSeparation)
            {
                var tile = new Vector2(MathF.Floor(home.X), MathF.Floor(home.Y));
                var baseAngle = (keys[i] * 0.6180339f % 1f) * MathF.Tau;
                var found = false;
                foreach (var ring in Rings)
                {
                    for (var k = 0; k < 8; k++)
                    {
                        var angle = baseAngle + k * (MathF.Tau / 8f);
                        var c = home + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ring;
                        c.X = Math.Clamp(c.X, tile.X + EdgeMargin, tile.X + 1f - EdgeMargin);
                        c.Y = Math.Clamp(c.Y, tile.Y + EdgeMargin, tile.Y + 1f - EdgeMargin);
                        var score = Nearest(placed, count, c);
                        if (score > bestScore)
                        {
                            best = c;
                            bestScore = score;
                        }

                        if (score >= MinSeparation)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found)
                        break;
                }
            }

            placed[count++] = best;
            offsets[i] = best - home;
        }
    }

    private static float Nearest(Vector2[] placed, int count, Vector2 p)
    {
        var best = float.MaxValue;
        for (var i = 0; i < count; i++)
            best = MathF.Min(best, Vector2.Distance(placed[i], p));

        return best;
    }
}
