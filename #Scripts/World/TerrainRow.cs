using Godot;

namespace Anomaly;

public partial class TerrainRow : Node2D
{
    private const int T = WorldGrid.TILE_SIZE;

    private WorldGrid grid;
    private WorldSections sections;
    private int section;
    private int row;
    private int fromX;
    private int toX;

    // A row is split between sections so each section draws only the raised tiles it owns.
    public void Setup(WorldGrid worldGrid, WorldSections worldSections, int sectionIndex, int rowIndex, int firstColumn, int endColumn)
    {
        grid = worldGrid;
        sections = worldSections;
        section = sectionIndex;
        row = rowIndex;
        fromX = Mathf.Max(0, firstColumn);
        toX = Mathf.Min(grid.Width, endColumn);
    }

    public override void _Draw()
    {
        for (int x = fromX; x < toX; x++)
        {
            int height = grid.HeightAt(x, row);
            if (height <= 0 || !sections.Owns(section, x, row))
                continue;
            Vector2 tileOrigin = new(x * T, 0);
            TerrainPainter.DrawCliffFace(this, grid, x, row, tileOrigin);
        }

        for (int x = fromX; x < toX; x++)
        {
            int height = grid.HeightAt(x, row);
            if (height <= 0 || !sections.Owns(section, x, row))
                continue;
            Vector2 topOrigin = new(x * T, -height * WorldGrid.LEVEL_HEIGHT);
            TerrainPainter.DrawTop(this, grid, x, row, topOrigin);
            TerrainPainter.DrawTopDetail(this, grid, x, row, topOrigin);
            if (height < WorldGrid.WALL_HEIGHT)
                RuinPainter.DrawVoidCreep(this, grid, x, row, topOrigin);
            TerrainPainter.DrawContactShadow(this, grid, x, row, topOrigin);
        }
    }
}
