using Godot;

namespace Anomaly;

// Ground of the Void. It is mostly Void tiles: the abyss of the overworld set solid underfoot, in five styles that change
// patch by patch. Between them lie torn patches of every land the Void has swallowed, each one a tile of the land its
// shard remembers (WorldGrid.EchoAt), half eaten. The floor canvas renders them through void_floor.gdshader, which
// reads each tile's style, seed and how much the Void has taken from its modulate.
public static class VoidPainter
{
    private const int T = WorldGrid.TILE_SIZE;
    private const int STYLES = 5;
    // Patches are 4 x 3 tiles, every other band shifted by half a patch so the seams do not line up into a grid.
    private const int PATCH_WIDTH = 4;
    private const int PATCH_HEIGHT = 3;
    private const int TORN_PERCENT = 38;

    private static readonly Biome[] Lands = { Biome.Verdant, Biome.BrightForest, Biome.Undead, Biome.Snow };
    private static readonly Color ShardGlow = new(0.95f, 0.35f, 0.85f, 0.85f);
    private static readonly Color ShardCold = new(0.5f, 0.88f, 1f, 0.8f);

    private static ShaderMaterial floorMaterial;

    public static ShaderMaterial FloorMaterial => floorMaterial ??= new ShaderMaterial { Shader = ShaderLibrary.VoidFloor };

    private static Vector2I PatchOf(int x, int y)
    {
        int band = Mathf.FloorToInt(y / (float)PATCH_HEIGHT);
        int shift = (band & 1) * (PATCH_WIDTH / 2);
        return new Vector2I(Mathf.FloorToInt((x + shift) / (float)PATCH_WIDTH), band);
    }

    private static bool IsTorn(Vector2I patch) => Art.Hash(patch.X, patch.Y, 900) % 100 < TORN_PERCENT;

    public static void DrawFloorTile(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 origin)
    {
        int hash = Art.Hash(x, y, 905);
        Vector2I patch = PatchOf(x, y);
        bool torn = IsTorn(patch) && grid.FloorAt(x, y) != FloorKind.Paved;
        bool seam = IsTorn(PatchOf(x - 1, y)) != torn || IsTorn(PatchOf(x + 1, y)) != torn || IsTorn(PatchOf(x, y - 1)) != torn || IsTorn(PatchOf(x, y + 1)) != torn;

        // Paved Void is a road: always the plate style, fully taken, so the way through reads at a glance.
        int style;
        float taken;
        Texture2D texture;
        Rect2 source;
        if (grid.FloorAt(x, y) == FloorKind.Paved)
        {
            style = 0;
            taken = 1f;
            texture = Art.Texture(Art.STONE);
            source = Art.TileRect(1, 1);
        }
        else if (torn)
        {
            style = hash % STYLES;
            taken = (seam ? 0.42f : 0.12f) + (hash / 7 % 4) * 0.06f;
            (texture, source) = TornTile(grid, x, y, patch, hash);
        }
        else
        {
            style = Art.Hash(patch.X, patch.Y, 901) % STYLES;
            taken = seam ? 0.74f : 0.9f + (hash / 7 % 3) * 0.05f;
            texture = Art.Texture(Art.STONE);
            source = Art.TileRect(1, 1);
        }

        var code = new Color(style / 8f + 0.01f, (hash / 13 % 97) / 97f, 0f, taken);
        canvas.DrawTextureRectRegion(texture, new Rect2(origin, new Vector2(T, T)), source, code);
    }

    // A torn patch is a tile of the land its shard remembers; one patch in five strays from another land entirely.
    private static (Texture2D, Rect2) TornTile(WorldGrid grid, int x, int y, Vector2I patch, int hash)
    {
        Biome land = grid.EchoAt(x, y);
        int stray = Art.Hash(patch.X, patch.Y, 902);
        if (stray % 100 < 20)
            land = Lands[(stray / 100) % Lands.Length];
        if (land == Biome.Verdant)
        {
            return hash % 5 == 0
                ? (Art.Texture(Art.STONE), Art.TileRect(1, 1))
                : (Art.Texture(Art.GRASS), Art.TileRect((hash / 7) % 4, (hash / 31) % 2));
        }
        return (Art.Texture(BiomeArt.FLOORS), BiomeArt.FloorTile(land, (hash / 5) % 8));
    }

    // Shards of glass and light lying on the Void floor. Never cracks: those are kept for secrets.
    public static void DrawDecal(CanvasItem canvas, int x, int y)
    {
        int hash = Art.Hash(x, y, 910);
        if (hash % 100 > 6)
            return;
        Vector2 center = new(x * T + 6 + (hash / 13) % 20, y * T + 6 + (hash / 29) % 20);
        Color color = (hash / 7) % 3 == 0 ? ShardCold : ShardGlow;
        canvas.DrawRect(new Rect2(center, new Vector2(1, 3)), color);
        canvas.DrawRect(new Rect2(center + new Vector2(-1, 1), new Vector2(3, 1)), color);
        canvas.DrawRect(new Rect2(center + new Vector2(0, 4), new Vector2(1, 1)), new Color(0.05f, 0.0f, 0.1f, 0.6f));
    }
}
