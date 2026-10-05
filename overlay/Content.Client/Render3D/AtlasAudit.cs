using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Client.Render3D;

/// <summary>
///     The layout behind <c>r3d_audit</c>, a developer check of the billboard atlas. The audit draws each entity on its own
///     into a cell that is much bigger than its slot and saves the sheet as a picture; the log says where the slot the atlas
///     reserved for each entity lies in its cell. Art outside that slot is painted onto the neighbouring slot in the real
///     atlas (the glowing lamp tubes on walls and characters were exactly that), so a picture of the cell shows it.
/// </summary>
public static class AtlasAudit
{
    /// <summary>Side of a cell in pixels: four and a half tiles each way from the entity origin, room for the biggest slot.</summary>
    public const int CellPixels = 288;

    /// <summary>Where the reserved slot lies inside a cell whose centre is the entity origin (screen y grows downwards).</summary>
    public static UIBox2i SlotInCell(Box2 bounds, int cellX, int cellY, int slotWidth, int slotHeight)
    {
        var centre = CellPixels / 2;
        var left = cellX * CellPixels + centre + (int) MathF.Round(bounds.Left * BillboardAtlas.PixelsPerTile);
        var top = cellY * CellPixels + centre - (int) MathF.Round(bounds.Top * BillboardAtlas.PixelsPerTile);
        return UIBox2i.FromDimensions(left, top, slotWidth, slotHeight);
    }

    /// <summary>How many cells fit in one square target of the given side.</summary>
    public static int CellsPerRow(int targetSide) => Math.Max(1, targetSide / CellPixels);
}
