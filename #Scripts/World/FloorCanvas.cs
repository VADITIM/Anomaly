using Godot;

namespace Anomaly;

public enum FloorPass
{
    Tiles,
    Decals,
    Edges,
}

public partial class FloorCanvas : Node2D
{
    private const int T = WorldGrid.TILE_SIZE;

    private WorldGrid grid;
    private WorldSections sections;
    private int section;
    private Rect2I tiles;
    private FloorPass pass;

    // One canvas per section and pass: the passes stay layered across section seams, where a shadow or decal
    // spills over into the neighbouring section's tiles.
    public void Setup(WorldGrid worldGrid, WorldSections worldSections, int sectionIndex, FloorPass floorPass)
    {
        grid = worldGrid;
        sections = worldSections;
        section = sectionIndex;
        tiles = worldSections.OwnedTilesOf(sectionIndex);
        pass = floorPass;
    }

    // Ground this canvas paints: level tiles its section owns.
    private bool Paints(int x, int y) => grid.HeightAt(x, y) == 0 && sections.Owns(section, x, y);

    private int X0 => Mathf.Max(0, tiles.Position.X);
    private int Y0 => Mathf.Max(0, tiles.Position.Y);
    private int X1 => Mathf.Min(grid.Width, tiles.End.X);
    private int Y1 => Mathf.Min(grid.Height, tiles.End.Y);

    public override void _Draw()
    {
        switch (pass)
        {
            case FloorPass.Tiles:
                DrawTiles();
                break;
            case FloorPass.Decals:
                DrawDecals();
                break;
            case FloorPass.Edges:
                DrawEdges();
                break;
        }
    }

    private void DrawTiles()
    {
        for (int y = Y0; y < Y1; y++)
        {
            for (int x = X0; x < X1; x++)
            {
                if (!Paints(x, y))
                    continue;
                Vector2 origin = new(x * T, y * T);
                TerrainPainter.DrawFloorTile(this, grid, x, y, origin);
            }
        }
    }

    private void DrawDecals()
    {
        for (int y = Y0; y < Y1; y++)
            for (int x = X0; x < X1; x++)
            {
                if (!Paints(x, y))
                    continue;
                if (grid.BiomeAt(x, y) == Biome.Void)
                    VoidPainter.DrawDecal(this, x, y);
                else if (grid.BiomeAt(x, y) == Biome.Verdant)
                    RuinPainter.DrawFloorDecals(this, grid, x, y, new Vector2(x * T, y * T));
                else
                    DrawBiomeDecal(x, y);
            }

        Texture2D vegetation = Art.Texture(Art.VEGETATION);
        for (int y = Y0; y < Y1; y++)
        {
            for (int x = X0; x < X1; x++)
            {
                if (!Paints(x, y) || grid.FloorAt(x, y) != FloorKind.Grass || grid.BiomeAt(x, y) != Biome.Verdant)
                    continue;
                int hash = Art.Hash(x, y, 17);
                if (hash % 100 > 22)
                    continue;
                Rect2 tuft = Art.GrassTufts[hash % Art.GrassTufts.Length];
                Vector2 position = new(x * T + 4 + (hash / 13) % 18, y * T + 6 + (hash / 29) % 18);
                DrawTextureRectRegion(vegetation, new Rect2(position, tuft.Size), tuft, new Color(0.85f, 0.95f, 0.85f));
            }
        }
    }

    private void DrawEdges()
    {
        for (int y = Y0; y < Y1; y++)
        {
            for (int x = X0; x < X1; x++)
            {
                if (!Paints(x, y))
                    continue;
                Vector2 origin = new(x * T, y * T);
                RuinPainter.DrawVoidCreep(this, grid, x, y, origin);
                TerrainPainter.DrawCliffFace(this, grid, x, y, origin);
                TerrainPainter.DrawContactShadow(this, grid, x, y, origin);
            }
        }
    }

    // Ground litter that belongs to the imported packs: pebbles in the grove.
    private void DrawBiomeDecal(int x, int y)
    {
        Biome biome = grid.BiomeAt(x, y);
        if (biome is Biome.Undead or Biome.Snow || grid.FloorAt(x, y) == FloorKind.Paved)
            return;
        int hash = Art.Hash(x, y, 233);
        if (hash % 100 > 11)
            return;
        SpriteRegion decal = BiomeArt.Pick(BiomeArt.ForestPebbles, hash / 7);
        Vector2 center = new(x * T + 6 + (hash / 13) % 20, y * T + 8 + (hash / 29) % 16);
        DrawTextureRectRegion(Art.Texture(decal.TexturePath), new Rect2(center - decal.Origin * decal.Scale, decal.ScaledSize), decal.Region, BiomeArt.ObjectTint(biome));
    }
}
