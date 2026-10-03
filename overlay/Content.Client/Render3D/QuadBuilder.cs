using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client.Render3D;

/// <summary>
///     Collects 3D quads (entity sprites standing, lying or hanging in the world), clips them against the near
///     plane, projects them with the <see cref="Camera3D"/>, sorts them back to front and turns them into vertices for
///     <c>DrawPrimitives</c>.
/// </summary>
/// <remarks>
///     Perspective-correct texturing: each vertex carries uv/w and 1/w (<c>w</c> = planar view depth); the fragment
///     shader divides them again (see entity.swsl). Per-vertex data layout:
///     <list type="bullet">
///     <item>Position: pixel position in the scene target.</item>
///     <item>UV: atlas uv divided by w.</item>
///     <item>UV2: x = 1/w, y = shade multiplier.</item>
///     <item>Color: r,g = world position used for light and FOV sampling, b = alpha multiplier, a = flags (0 = alpha
///     tested, 1 = alpha blended).</item>
///     </list>
/// </remarks>
public sealed class QuadBuilder
{
    public const float NearPlane = 0.05f;

    private struct Quad
    {
        public Vector3 P0, P1, P2, P3; // top-left, top-right, bottom-right, bottom-left
        public Vector2 Uv0, Uv1, Uv2, Uv3;
        public Vector2 Foot;
        public float Shade;
        public float Alpha;
        public float Flags;
        public float SortDepth;
        public float SortBias;
        public int Seq;
    }

    private struct ClipVertex
    {
        public Vector3 Pos;
        public Vector2 Uv;
        public float Depth;
    }

    private Quad[] _quads = new Quad[512];
    private int _count;

    private readonly ClipVertex[] _poly = new ClipVertex[8];
    private readonly ClipVertex[] _clipped = new ClipVertex[8];

    private DrawVertexUV2DColor[] _vertices = new DrawVertexUV2DColor[4096];
    private ushort[] _indices = new ushort[8192];

    public int QuadCount => _count;

    public void Clear() => _count = 0;

    /// <summary>
    ///     Adds a quad given its corners (top-left, top-right, bottom-right, bottom-left as seen from the front)
    ///     and the matching atlas uvs.
    /// </summary>
    public void Add(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
        Vector2 foot, float shade = 1f, float alpha = 1f, bool blend = false, float flags = -1f, float sortBias = 0f)
    {
        if (_count == _quads.Length)
            Array.Resize(ref _quads, _quads.Length * 2);

        ref var q = ref _quads[_count++];
        q.P0 = p0; q.P1 = p1; q.P2 = p2; q.P3 = p3;
        q.Uv0 = uv0; q.Uv1 = uv1; q.Uv2 = uv2; q.Uv3 = uv3;
        q.Foot = foot;
        q.Shade = shade;
        q.Alpha = alpha;
        q.Flags = flags >= 0f ? flags : blend ? 1f : 0f;
        q.SortDepth = 0;
        q.SortBias = sortBias;
        q.Seq = _count - 1;
    }

    /// <summary>True for a quad that stands up (its left edge is mostly vertical), false for flat or nearly flat ones.</summary>
    private static bool IsUpright(in Quad q)
    {
        var edge = q.P0 - q.P3;
        var vertical = MathF.Abs(edge.Z);
        var horizontal = MathF.Sqrt(edge.X * edge.X + edge.Y * edge.Y);
        return vertical > 0.05f && horizontal < vertical * 0.6f;
    }

    /// <summary>
    ///     Builds vertex and index data for all collected quads, sorted far to back. Returns the number of vertices
    ///     and indices. Spans are valid until the next call.
    /// </summary>
    public void Build(Camera3D cam, out ReadOnlyMemory<DrawVertexUV2DColor> vertices, out ReadOnlyMemory<ushort> indices)
    {
        // sort by distance of the centre (far first)
        for (var i = 0; i < _count; i++)
        {
            ref var q = ref _quads[i];
            var c = (q.P0 + q.P1 + q.P2 + q.P3) * 0.25f;

            // Upright quads (wall lamps, windows, doors, posters, characters) are ordered by how far away they are on the
            // ground plane, not by the depth of their 3D centre. With the centre, two quads on the same wall plane but at
            // different heights (a lamp on a window) swapped places whenever the camera pitched or moved, because the
            // height difference times the pitch is bigger than the few hundredths of a tile that separate the planes.
            // That made lamps flicker and vanish behind the glass.
            if (IsUpright(in q))
                c.Z = cam.Position.Z;

            q.SortDepth = Vector3.Dot(c - cam.Position, cam.Forward) + q.SortBias;
        }

        Array.Sort(_quads, 0, _count, QuadDepthComparer.Instance);

        var vCount = 0;
        var iCount = 0;

        for (var i = 0; i < _count; i++)
        {
            ref var q = ref _quads[i];
            if (q.SortDepth < -4f)
                continue;

            // gather polygon in camera depth space
            _poly[0] = MakeVertex(cam, q.P0, q.Uv0);
            _poly[1] = MakeVertex(cam, q.P1, q.Uv1);
            _poly[2] = MakeVertex(cam, q.P2, q.Uv2);
            _poly[3] = MakeVertex(cam, q.P3, q.Uv3);

            var n = ClipNear(4);
            if (n < 3)
                continue;

            if (vCount + n > _vertices.Length - 8)
            {
                Array.Resize(ref _vertices, _vertices.Length * 2);
                Array.Resize(ref _indices, _indices.Length * 2);
            }

            // Vertex indices must fit a ushort; stop adding when we get close (the cap keeps this far away).
            if (vCount + n >= ushort.MaxValue)
                break;

            var baseIndex = vCount;
            var any = false;
            for (var k = 0; k < n; k++)
            {
                var v = _clipped[k];
                var invW = 1f / v.Depth;
                cam.Project(v.Pos, out var px, out _);
                if (px.X > -4096 && px.X < cam.Size.X + 4096 && px.Y > -4096 && px.Y < cam.Size.Y + 4096)
                    any = true;

                _vertices[vCount++] = new DrawVertexUV2DColor
                {
                    Position = px,
                    UV = v.Uv * invW,
                    UV2 = new Vector2(invW, q.Shade),
                    Color = new Color(q.Foot.X, q.Foot.Y, q.Alpha, q.Flags),
                };
            }

            if (!any)
            {
                vCount = baseIndex;
                continue;
            }

            for (var k = 1; k < n - 1; k++)
            {
                _indices[iCount++] = (ushort) baseIndex;
                _indices[iCount++] = (ushort) (baseIndex + k);
                _indices[iCount++] = (ushort) (baseIndex + k + 1);
            }
        }

        vertices = new ReadOnlyMemory<DrawVertexUV2DColor>(_vertices, 0, vCount);
        indices = new ReadOnlyMemory<ushort>(_indices, 0, iCount);
    }

    private static ClipVertex MakeVertex(Camera3D cam, Vector3 pos, Vector2 uv)
    {
        return new ClipVertex
        {
            Pos = pos,
            Uv = uv,
            Depth = Vector3.Dot(pos - cam.Position, cam.Forward),
        };
    }

    /// <summary>Clips <see cref="_poly"/> against the near plane into <see cref="_clipped"/>; returns the vertex count.</summary>
    private int ClipNear(int count)
    {
        var n = 0;
        for (var i = 0; i < count; i++)
        {
            var a = _poly[i];
            var b = _poly[(i + 1) % count];
            var aIn = a.Depth >= NearPlane;
            var bIn = b.Depth >= NearPlane;

            if (aIn)
                _clipped[n++] = a;

            if (aIn != bIn)
            {
                var t = (NearPlane - a.Depth) / (b.Depth - a.Depth);
                _clipped[n++] = new ClipVertex
                {
                    Pos = Vector3.Lerp(a.Pos, b.Pos, t),
                    Uv = Vector2.Lerp(a.Uv, b.Uv, t),
                    Depth = NearPlane,
                };
            }
        }

        return n;
    }

    private sealed class QuadDepthComparer : IComparer<Quad>
    {
        public static readonly QuadDepthComparer Instance = new();

        // far first; quads at exactly the same depth keep the order they were added in (Array.Sort is not stable, and
        // an unstable order between coplanar quads shows up as flicker)
        public int Compare(Quad x, Quad y)
        {
            var c = y.SortDepth.CompareTo(x.SortDepth);
            return c != 0 ? c : x.Seq.CompareTo(y.Seq);
        }
    }
}
