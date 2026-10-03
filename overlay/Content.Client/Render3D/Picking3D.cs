using System.Numerics;

namespace Content.Client.Render3D;

/// <summary>
///     CPU ray picking maths for the 3D view: ray vs. the parallelogram quads entities are drawn as.
///     Everything is pure so it can be unit tested without a client.
/// </summary>
public static class Picking3D
{
    /// <summary>The fraction of a sprite quad (per axis, centred) that counts as a hit: sprite bounds shrunk by 10%.</summary>
    public const float ShrinkFraction = 0.9f;

    /// <summary>
    ///     Intersects the ray <c>origin + dir * t</c> (t &gt; 0) with the parallelogram spanned by
    ///     <paramref name="p0"/>, <paramref name="p1"/> (one edge) and <paramref name="p3"/> (the other edge from p0).
    ///     <paramref name="shrink"/> is the fraction of each axis, centred, that is accepted (1 = whole quad).
    ///     Double sided.
    /// </summary>
    public static bool RayQuad(Vector3 origin, Vector3 dir, Vector3 p0, Vector3 p1, Vector3 p3, float shrink, out float t)
    {
        t = 0;
        var e1 = p1 - p0;
        var e2 = p3 - p0;
        var n = Vector3.Cross(e1, e2);
        var denom = Vector3.Dot(n, dir);
        if (MathF.Abs(denom) < 1e-9f)
            return false;

        t = Vector3.Dot(n, p0 - origin) / denom;
        if (t <= 0)
            return false;

        var p = origin + dir * t - p0;
        var l1 = e1.LengthSquared();
        var l2 = e2.LengthSquared();
        if (l1 < 1e-12f || l2 < 1e-12f)
            return false;

        var s = Vector3.Dot(p, e1) / l1;
        var u = Vector3.Dot(p, e2) / l2;
        var lo = 0.5f - shrink * 0.5f;
        var hi = 0.5f + shrink * 0.5f;
        return s >= lo && s <= hi && u >= lo && u <= hi;
    }

    /// <summary>
    ///     Ray against the ground plane z = <paramref name="z"/>. Returns false when the ray doesn't descend to it.
    /// </summary>
    public static bool RayPlaneZ(Vector3 origin, Vector3 dir, float z, out float t, out Vector2 point)
    {
        point = default;
        t = 0;
        if (MathF.Abs(dir.Z) < 1e-6f)
            return false;

        t = (z - origin.Z) / dir.Z;
        if (t <= 0)
            return false;

        point = new Vector2(origin.X + dir.X * t, origin.Y + dir.Y * t);
        return true;
    }

    /// <summary>
    ///     Decides between an entity hit, a wall hit and the floor: an entity wins when it is within
    ///     <paramref name="preferEntityWithin"/> of the nearest other hit (plan Phase 4).
    /// </summary>
    public static bool EntityBeatsWorld(float entityT, float worldT, float preferEntityWithin = 0.15f)
    {
        return entityT <= worldT + preferEntityWithin;
    }
}
