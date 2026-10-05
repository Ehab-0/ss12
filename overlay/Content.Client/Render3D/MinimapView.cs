using System.Numerics;

namespace Content.Client.Render3D;

/// <summary>
///     The geometry of the minimap, without any drawing, so it can be tested. Everything is in the local space of the grid (one
///     unit = one tile, y up); the map is drawn with "up" on the screen being <c>up</c>, a direction in that space (the way the
///     camera looks when the map turns with it, or north when it does not).
/// </summary>
public static class MinimapView
{
    /// <summary>The direction on the grid that is "right" on the screen when <paramref name="up"/> is up (the up direction turned a quarter turn clockwise).</summary>
    public static Vector2 Right(Vector2 up) => new(up.Y, -up.X);

    /// <summary>
    ///     The matrix that takes a point of the grid (tile units) to the pixels of the map control: the player (<paramref name="player"/>)
    ///     lands on <paramref name="centre"/>, <paramref name="up"/> points up the screen, and one tile is <paramref name="pixelsPerTile"/> pixels.
    ///     Screen y grows downwards.
    /// </summary>
    public static Matrix3x2 Matrix(Vector2 player, Vector2 up, Vector2 centre, float pixelsPerTile)
    {
        var right = Right(up);
        return new Matrix3x2(
            right.X * pixelsPerTile, -up.X * pixelsPerTile,
            right.Y * pixelsPerTile, -up.Y * pixelsPerTile,
            centre.X - pixelsPerTile * Vector2.Dot(player, right),
            centre.Y + pixelsPerTile * Vector2.Dot(player, up));
    }

    /// <summary>A direction on the grid as a direction on the screen (unit length stays unit length).</summary>
    public static Vector2 ScreenDirection(Vector2 direction, Vector2 up)
    {
        var right = Right(up);
        return new Vector2(Vector2.Dot(direction, right), -Vector2.Dot(direction, up));
    }

    /// <summary>The window height the configured size of the small map is meant for.</summary>
    public const float ReferenceHeight = 720f;

    /// <summary>
    ///     The side in pixels of the small map. The configured side is for a view <see cref="ReferenceHeight"/> pixels tall; a
    ///     smaller or larger view scales it in step (never below 45% or above 160%), and the map never takes more than 38% of the
    ///     shorter side of the view, so in a small window it does not cover the picture.
    /// </summary>
    public static float SmallSide(float configuredSide, float viewShortSide)
    {
        var side = configuredSide * Math.Clamp(viewShortSide / ReferenceHeight, 0.45f, 1.6f);
        return Math.Clamp(side, 80f, MathF.Max(80f, viewShortSide * 0.38f));
    }

    /// <summary>Pixels per tile that make <paramref name="radiusTiles"/> tiles reach the edge of a square control of the given side.</summary>
    public static float PixelsPerTile(float controlSide, float radiusTiles)
    {
        return controlSide / 2f / MathF.Max(radiusTiles, 1f);
    }

    /// <summary>True when something at <paramref name="distance"/> tiles from the player (plus half its own <paramref name="extent"/>) can be seen.</summary>
    public static bool InRange(float distance, float extent, float radiusTiles)
    {
        return distance - extent <= radiusTiles * 1.5f;
    }
}
