using Godot;

namespace Anomaly;

// Shared drawing routines for terrain. Elevated tops are drawn shifted up by their height,
// and every tile that sits above its southern neighbour paints the exposed cliff face below it.
public static class TerrainPainter
{
    private const int T = WorldGrid.TILE_SIZE;
    private const float L = WorldGrid.LEVEL_HEIGHT;
    private const float PIT_FACE_DEPTH = 72f;

    private static readonly Color AmbientOcclusion = new(0.02f, 0.0f, 0.06f, 0.5f);
    public static readonly Color GrassTint = new(0.74f, 0.95f, 0.84f);
    private static readonly Color PavedTint = new(0.62f, 0.68f, 0.72f);
    private static readonly Color CanopyTint = new(0.36f, 0.5f, 0.46f);

    public static void DrawFloorTile(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 origin)
    {
        Texture2D grass = Art.Texture(Art.GRASS);
        int hash = Art.Hash(x, y);
        Rect2 target = new(origin, new Vector2(T, T));
        Biome biome = grid.BiomeAt(x, y);
        FloorKind floorKind = grid.FloorAt(x, y);
        if (biome == Biome.Void)
        {
            VoidPainter.DrawFloorTile(canvas, grid, x, y, origin);
            return;
        }
        if (biome != Biome.Verdant)
        {
            if (floorKind == FloorKind.Paved && IsPavedInterior(grid, x, y))
                canvas.DrawTextureRectRegion(Art.Texture(Art.STONE), target, Art.TileRect(1, 1), BiomeArt.PavedTint(biome).Darkened((hash % 5) * 0.03f));
            else
                canvas.DrawTextureRectRegion(Art.Texture(BiomeArt.FLOORS), target, BiomeArt.FloorTile(biome, (hash / 5) % 8), Colors.White);
            return;
        }
        switch (floorKind)
        {
            case FloorKind.Paved:
                if (IsPavedInterior(grid, x, y))
                    canvas.DrawTextureRectRegion(Art.Texture(Art.STONE), target, Art.TileRect(1, 1), PavedTint.Darkened((hash % 5) * 0.03f));
                else
                    canvas.DrawTextureRectRegion(grass, target, Art.TileRect(hash % 8, 4 + (hash / 8) % 4), GrassTint);
                break;
            default:
                int variant = hash % 100;
                int column = variant < 70 ? (hash / 7) % 4 : 4 + (hash / 7) % 4;
                int row = variant < 70 ? (hash / 31) % 2 : (hash / 31) % 4;
                canvas.DrawTextureRectRegion(grass, target, Art.TileRect(column, row), GrassTint);
                break;
        }
    }

    private static bool IsPavedInterior(WorldGrid grid, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (grid.HeightAt(x + dx, y + dy) == 0 && grid.FloorAt(x + dx, y + dy) != FloorKind.Paved)
                    return false;
        return true;
    }

    public static void DrawTop(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 origin)
    {
        int height = grid.HeightAt(x, y);
        bool open(int nx, int ny) => grid.HeightAt(nx, ny) != height;
        bool left = open(x - 1, y), right = open(x + 1, y), top = open(x, y - 1), bottom = open(x, y + 1);
        Rect2 target = new(origin, new Vector2(T, T));

        if (height >= WorldGrid.WALL_HEIGHT)
        {
            DrawWallTop(canvas, grid.BiomeAt(x, y), x, y, target, left, right, top, bottom);
            return;
        }

        int column = left && right ? 3 : left ? 0 : right ? 2 : 1;
        int row = top && bottom ? 3 : top ? 0 : bottom ? 2 : 1;
        float shade = 0.7f + height * 0.05f;
        Color topTint = BiomeArt.TopTint(grid.BiomeAt(x, y));
        canvas.DrawTextureRectRegion(Art.Texture(Art.STONE), target, Art.TileRect(column, row), new Color(shade * 0.95f, shade, shade * 1.06f) * topTint);
    }

    public static void DrawTopDetail(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 origin)
    {
        if (grid.HeightAt(x, y) >= WorldGrid.WALL_HEIGHT)
            return;
        int hash = Art.Hash(x, y, 91);
        Texture2D vegetation = Art.Texture(Art.VEGETATION);
        if (hash % 100 < 28 && grid.ArtBiomeAt(x, y) != Biome.Undead)
        {
            Rect2 tuft = Art.GrassTufts[hash % Art.GrassTufts.Length];
            Vector2 position = origin + new Vector2(5 + (hash / 7) % 16, 6 + (hash / 19) % 16);
            canvas.DrawTextureRectRegion(vegetation, new Rect2(position, tuft.Size), tuft, GrassTint.Darkened(0.15f));
        }
        if (hash % 100 > 82)
        {
            Vector2 crack = origin + new Vector2(6 + (hash / 11) % 18, 8 + (hash / 23) % 14);
            Color line = new(0.12f, 0.1f, 0.16f, 0.55f);
            canvas.DrawLine(crack, crack + new Vector2(5, 2), line, 1f);
            canvas.DrawLine(crack + new Vector2(5, 2), crack + new Vector2(8, 6), line, 1f);
        }
        if (hash % 100 is > 60 and < 66)
            canvas.DrawRect(new Rect2(origin + new Vector2(8 + (hash / 5) % 14, 10 + (hash / 13) % 12), new Vector2(3, 2)), new Color(0.45f, 0.44f, 0.5f));
    }

    private static void DrawWallTop(CanvasItem canvas, Biome biome, int x, int y, Rect2 target, bool left, bool right, bool top, bool bottom)
    {
        int hash = Art.Hash(x, y, 5);
        canvas.DrawTextureRectRegion(Art.Texture(Art.GRASS), target, Art.TileRect((hash / 3) % 4, (hash / 11) % 2), BiomeArt.WallTopTint(biome));
        Texture2D vegetation = Art.Texture(Art.VEGETATION);
        for (int clump = 0; clump < 2; clump++)
        {
            int clumpHash = Art.Hash(x, y, 40 + clump);
            SpriteRegion bush = Art.Bushes[clumpHash % Art.Bushes.Length];
            Vector2 size = bush.Region.Size;
            Vector2 offset = new((clumpHash / 7) % 24 - 12, (clumpHash / 53) % 20 - 8);
            Rect2 rect = new(target.Position + new Vector2(16, 18) - size * 0.5f + offset, size);
            canvas.DrawTextureRectRegion(vegetation, rect, bush.Region, (biome == Biome.Verdant ? CanopyTint : BiomeArt.CanopyTint(biome)).Lightened(((clumpHash / 3) % 10) * 0.02f));
        }
        Color lip = new(0.78f, 0.86f, 0.6f, 0.55f);
        Color edge = new(0.05f, 0.04f, 0.08f, 0.9f);
        if (bottom)
        {
            canvas.DrawRect(new Rect2(target.Position + new Vector2(0, T - 3), new Vector2(T, 1)), lip);
            canvas.DrawRect(new Rect2(target.Position + new Vector2(0, T - 2), new Vector2(T, 2)), edge);
        }
        if (top)
            canvas.DrawRect(new Rect2(target.Position, new Vector2(T, 1)), edge);
        if (left)
            canvas.DrawRect(new Rect2(target.Position, new Vector2(1, T)), edge);
        if (right)
            canvas.DrawRect(new Rect2(target.Position + new Vector2(T - 1, 0), new Vector2(1, T)), edge);
    }

    // Face spans from the tile's top edge (shifted by its own height) down to the neighbour's surface.
    public static void DrawCliffFace(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 tileOrigin)
    {
        int height = grid.HeightAt(x, y);
        int southHeight = grid.HeightAt(x, y + 1);
        if (height == WorldGrid.PIT || southHeight >= height)
            return;

        bool intoPit = southHeight == WorldGrid.PIT;
        float faceTop = tileOrigin.Y + T - height * L;
        float faceBottom = intoPit ? tileOrigin.Y + T + PIT_FACE_DEPTH : tileOrigin.Y + T - southHeight * L;

        Texture2D wall = Art.Texture(Art.WALL);
        bool westContinues = grid.HeightAt(x - 1, y) == height && grid.HeightAt(x - 1, y + 1) < height;
        bool eastContinues = grid.HeightAt(x + 1, y) == height && grid.HeightAt(x + 1, y + 1) < height;
        int sourceColumn = !westContinues ? 1 : !eastContinues ? 4 : 2 + (x & 1);

        float cursor = faceTop;
        bool first = true;
        while (cursor < faceBottom - 0.5f)
        {
            float segment = Mathf.Min(L, faceBottom - cursor);
            Rect2 source = new(sourceColumn * 32, first ? 192 : 224, 32, segment);
            float depth = (cursor - faceTop) / Mathf.Max(1f, faceBottom - faceTop);
            float tint = height >= WorldGrid.WALL_HEIGHT ? 0.5f : 0.72f;
            Color modulate = (new Color(tint, tint * 0.95f, tint * 1.08f) * BiomeArt.CliffTint(grid.BiomeAt(x, y))).Lerp(new Color(0.08f, 0.03f, 0.14f), intoPit ? depth * 0.9f : depth * 0.35f);
            canvas.DrawTextureRectRegion(wall, new Rect2(tileOrigin.X, cursor, T, segment), source, modulate);
            cursor += segment;
            first = false;
        }

        if (intoPit)
        {
            var fadeTop = new Color(0.03f, 0.0f, 0.07f, 0f);
            var fadeBottom = new Color(0.03f, 0.0f, 0.07f, 1f);
            float fadeStart = faceBottom - PIT_FACE_DEPTH * 0.7f;
            canvas.DrawPolygon(
                new[] { new Vector2(tileOrigin.X, fadeStart), new Vector2(tileOrigin.X + T, fadeStart), new Vector2(tileOrigin.X + T, faceBottom), new Vector2(tileOrigin.X, faceBottom) },
                new[] { fadeTop, fadeTop, fadeBottom, fadeBottom });
        }
        else
        {
            canvas.DrawRect(new Rect2(tileOrigin.X, faceBottom - 2, T, 2), new Color(0.03f, 0.01f, 0.06f, 0.55f));
        }
    }

    public static void DrawContactShadow(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 topOrigin)
    {
        int height = grid.HeightAt(x, y);
        int northHeight = grid.HeightAt(x, y - 1);
        if (height == WorldGrid.PIT || northHeight <= height)
            return;
        float strength = Mathf.Min(1f, (northHeight - height) * 0.6f);
        Color top = new(AmbientOcclusion.R, AmbientOcclusion.G, AmbientOcclusion.B, AmbientOcclusion.A * strength);
        Color bottom = new(AmbientOcclusion.R, AmbientOcclusion.G, AmbientOcclusion.B, 0f);
        canvas.DrawPolygon(
            new[] { topOrigin, topOrigin + new Vector2(T, 0), topOrigin + new Vector2(T, 14), topOrigin + new Vector2(0, 14) },
            new[] { top, top, bottom, bottom });
    }
}
