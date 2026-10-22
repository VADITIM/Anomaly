using Godot;

namespace Anomaly;

// Builds each section's terrain inside its section node. Ground-level tiles never occlude actors, so they are baked into
// bottom canvases, one per section and pass. Elevated tiles get one node per row and section, which sits in the actor
// Y-sort and lets raised terrain correctly hide whatever stands behind it.
public partial class TerrainLayer : Node2D
{
    private const int FLOOR_Z_INDEX = -50;

    private WorldGrid grid;

    public void Build(WorldGrid worldGrid, WorldSections sections)
    {
        grid = worldGrid;
        for (int i = 0; i < sections.Count; i++)
        {
            Rect2I tiles = sections.OwnedTilesOf(i);
            Node2D section = sections.NodeOf(i);
            if (HasGround(tiles))
            {
                foreach (FloorPass pass in new[] { FloorPass.Tiles, FloorPass.Decals, FloorPass.Edges })
                {
                    var floor = new FloorCanvas { Name = $"Floor{pass}", ZIndex = FLOOR_Z_INDEX - 2 + (int)pass };
                    // Void ground is drawn through its own shader, which reads each tile's look from its modulate.
                    if (pass == FloorPass.Tiles && IsVoid(tiles))
                        floor.Material = VoidPainter.FloorMaterial;
                    floor.Setup(grid, sections, i, pass);
                    section.AddChild(floor);
                }
            }

            for (int y = tiles.Position.Y; y < tiles.End.Y; y++)
            {
                if (!RowHasElevation(sections, i, y, tiles.Position.X, tiles.End.X))
                    continue;
                var row = new TerrainRow { Name = $"TerrainRow{y}", Position = new Vector2(0, y * WorldGrid.TILE_SIZE) };
                row.Setup(grid, sections, i, y, tiles.Position.X, tiles.End.X);
                section.AddChild(row);
            }
        }
    }

    private bool IsVoid(Rect2I tiles)
    {
        Vector2I center = tiles.Position + tiles.Size / 2;
        return grid.BiomeAt(center.X, center.Y) == Biome.Void;
    }

    private bool HasGround(Rect2I tiles)
    {
        for (int y = tiles.Position.Y; y < tiles.End.Y; y++)
            for (int x = tiles.Position.X; x < tiles.End.X; x++)
                if (grid.HeightAt(x, y) >= 0)
                    return true;
        return false;
    }

    private bool RowHasElevation(WorldSections sections, int section, int y, int fromX, int toX)
    {
        for (int x = fromX; x < toX; x++)
            if (grid.HeightAt(x, y) > 0 && sections.Owns(section, x, y))
                return true;
        return false;
    }
}
