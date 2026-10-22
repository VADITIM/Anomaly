using Godot;

namespace Anomaly;

// Per-biome art tables. Verdant keeps the original tilesets; the other three regions are imported packs:
// Bright Forest (EPIC RPG World Pack: Ancient Ruins), Undead (Undead tileset, Objects palette) and Snow (Mana Seed winter).
// Pixel sizes differ per pack, so Snow sprites carry a Scale to land on the 32px tile grid.
public static class BiomeArt
{
    public const string FLOORS = "Tilesets/Biomes/floors.png";
    private const string FOREST = "Tilesets/Biomes/BrightForest/props.png";
    private const string BONES = "Tilesets/Biomes/Undead/Objects.png";
    private const string SNOW = "Tilesets/Biomes/Snowy/winter_sample.png";
    // The sample sheet paints the boulder's lower half over a snow ground tile, so it is cut out into its own sprite.
    private const string SNOW_ROCK = "Tilesets/Biomes/Snowy/snow_rock.png";

    public static string Name(Biome biome) => biome switch
    {
        Biome.BrightForest => "Aurelian Grove",
        Biome.Undead => "The Bonefields",
        Biome.Snow => "Frostwatch",
        Biome.Void => "The Void",
        _ => "Hollow Reach",
    };

    public static Color MapColor(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(0.52f, 0.68f, 0.28f),
        Biome.Undead => new Color(0.5f, 0.5f, 0.56f),
        Biome.Snow => new Color(0.82f, 0.88f, 0.92f),
        Biome.Void => new Color(0.46f, 0.2f, 0.58f),
        _ => new Color(0.3f, 0.62f, 0.5f),
    };

    public static Color LabelColor(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(0.98f, 0.9f, 0.45f),
        Biome.Undead => new Color(0.82f, 0.74f, 0.9f),
        Biome.Snow => new Color(0.82f, 0.95f, 1f),
        Biome.Void => new Color(0.98f, 0.55f, 0.95f),
        _ => new Color(0.6f, 1f, 0.8f),
    };

    // Multipliers applied to elevated stone tops and to cliff faces so each region reads differently.
    public static Color TopTint(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(1.08f, 1f, 0.82f),
        Biome.Undead => new Color(0.82f, 0.78f, 0.9f),
        Biome.Snow => new Color(0.95f, 1.02f, 1.1f),
        Biome.Void => new Color(0.62f, 0.48f, 0.9f),
        _ => Colors.White,
    };

    public static Color CliffTint(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(1.25f, 1.02f, 0.78f),
        Biome.Undead => new Color(0.9f, 0.85f, 1f),
        Biome.Snow => new Color(1.0f, 1.12f, 1.35f),
        Biome.Void => new Color(0.7f, 0.5f, 1.05f),
        _ => Colors.White,
    };

    public static Color PavedTint(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(0.9f, 0.82f, 0.62f),
        Biome.Undead => new Color(0.42f, 0.4f, 0.48f),
        Biome.Snow => new Color(0.74f, 0.8f, 0.9f),
        Biome.Void => new Color(0.4f, 0.3f, 0.55f),
        _ => new Color(0.62f, 0.68f, 0.72f),
    };

    public static Color CanopyTint(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(0.62f, 0.56f, 0.22f),
        Biome.Undead => new Color(0.22f, 0.2f, 0.26f),
        Biome.Snow => new Color(0.66f, 0.78f, 0.84f),
        Biome.Void => new Color(0.32f, 0.16f, 0.42f),
        _ => new Color(0.36f, 0.5f, 0.46f),
    };

    public static Color WallTopTint(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(0.5f, 0.5f, 0.28f),
        Biome.Undead => new Color(0.26f, 0.25f, 0.3f),
        Biome.Snow => new Color(0.6f, 0.7f, 0.78f),
        Biome.Void => new Color(0.24f, 0.12f, 0.34f),
        _ => new Color(0.4f, 0.52f, 0.47f),
    };

    public static Color ObjectTint(Biome biome) => biome switch
    {
        Biome.BrightForest => new Color(0.92f, 0.95f, 0.92f),
        Biome.Undead => new Color(0.82f, 0.8f, 0.9f),
        Biome.Snow => new Color(0.9f, 0.94f, 1f),
        Biome.Void => new Color(0.78f, 0.66f, 0.95f),
        _ => Colors.White,
    };

    // The floors atlas holds one 8 x 32px row per imported biome.
    public static Rect2 FloorTile(Biome biome, int hash)
    {
        int row = biome switch { Biome.BrightForest => 0, Biome.Undead => 1, _ => 2 };
        return new Rect2((hash % 8) * 32, row * 32, 32, 32);
    }

    private static SpriteRegion F(float x, float y, float w, float h, float ox, float oy, float scale = 1f) => new(FOREST, new Rect2(x, y, w, h), new Vector2(ox, oy), scale);
    private static SpriteRegion U(float x, float y, float w, float h, float ox, float oy, float scale = 1f) => new(BONES, new Rect2(x, y, w, h), new Vector2(ox, oy), scale);
    private static SpriteRegion S(float x, float y, float w, float h, float ox, float oy, float scale = 2f) => new(SNOW, new Rect2(x, y, w, h), new Vector2(ox, oy), scale);

    // Bright Forest
    public static readonly SpriteRegion[] ForestTrees = { F(10, 2, 101, 182, 51, 172), F(138, 2, 101, 182, 51, 172), F(256, 0, 32, 58, 16, 55, 1.6f), F(288, 13, 31, 43, 15, 41, 1.6f) };
    public static readonly SpriteRegion[] ForestBushes = { F(265, 65, 49, 30, 24, 28), F(324, 70, 18, 22, 9, 20, 1.4f), F(356, 67, 27, 29, 13, 27, 1.4f) };
    public static readonly SpriteRegion[] ForestRocks = { F(135, 227, 47, 27, 23, 22), F(141, 200, 41, 21, 20, 17), F(138, 264, 38, 20, 19, 16) };
    public static readonly SpriteRegion[] ForestDead = { F(320, 12, 29, 51, 14, 50, 1.5f) };
    public static readonly SpriteRegion[] ForestPebbles = { F(42, 199, 17, 10, 8, 8), F(72, 200, 14, 8, 7, 6), F(42, 234, 12, 8, 6, 6), F(42, 263, 17, 10, 8, 8) };
    public static readonly SpriteRegion[] ForestPillars = { F(205, 209, 36, 71, 18, 66), F(327, 129, 51, 60, 25, 56) };
    public static readonly SpriteRegion ForestPlinth = F(263, 114, 53, 76, 26, 70);
    public static readonly SpriteRegion ForestWall = F(259, 235, 90, 48, 45, 40);
    public static readonly SpriteRegion ForestAltar = F(386, 146, 56, 44, 28, 40);
    public static readonly SpriteRegion ForestTower = F(359, 196, 144, 187, 72, 180);
    public static readonly SpriteRegion ForestFountain = F(83, 294, 123, 119, 61, 104);

    // Undead
    public static readonly SpriteRegion[] BoneTrees = { U(67, 229, 59, 74, 30, 68, 1.2f), U(131, 229, 59, 74, 30, 68, 1.2f) };
    public static readonly SpriteRegion[] BoneBushes = { U(192, 224, 110, 80, 55, 70, 0.7f), U(4, 630, 24, 23, 12, 21), U(193, 438, 46, 50, 23, 46, 0.8f) };
    public static readonly SpriteRegion[] BoneRocks = { U(2, 373, 57, 56, 28, 50), U(2, 498, 45, 44, 22, 40), U(54, 505, 34, 33, 17, 30) };
    public static readonly SpriteRegion[] BoneScatter =
    {
        U(104, 550, 32, 34, 16, 30), U(3, 548, 43, 39, 21, 34), U(68, 593, 41, 31, 20, 27), U(32, 593, 30, 29, 15, 25),
        U(98, 628, 17, 23, 8, 21), U(146, 629, 19, 22, 9, 20), U(194, 629, 15, 20, 7, 18), U(208, 628, 18, 22, 9, 20), U(4, 630, 24, 23, 12, 21), U(199, 375, 71, 51, 35, 46, 0.6f),
    };
    public static readonly SpriteRegion[] BoneGraves = { U(98, 628, 17, 23, 8, 21), U(146, 629, 19, 22, 9, 20), U(194, 629, 15, 20, 7, 18), U(208, 628, 18, 22, 9, 20) };
    public static readonly SpriteRegion BoneRuin = U(0, 99, 80, 124, 40, 118);
    public static readonly SpriteRegion BonePillar = U(245, 307, 55, 54, 27, 48);
    public static readonly SpriteRegion SkeletonKing = U(5, 3, 150, 113, 75, 100);
    public static readonly SpriteRegion SkullPile = U(199, 375, 71, 51, 35, 46);
    public static readonly SpriteRegion Hanging = U(1, 434, 61, 62, 30, 58);

    // Snow
    public static readonly SpriteRegion[] SnowTrees = { S(177, 1, 79, 106, 44, 100, 1.5f) };
    public static readonly SpriteRegion[] SnowBushes = { S(17, 129, 14, 13, 7, 12), S(34, 130, 30, 24, 15, 22) };
    public static readonly SpriteRegion[] SnowRocks = { new(SNOW_ROCK, new Rect2(0, 0, 32, 32), new Vector2(16, 29), 1.5f) };
    // Small frost-crusted stones; the pack's twig tiles are opaque squares, so they cannot be scattered as sprites.
    public static readonly SpriteRegion[] SnowStones = { new(SNOW_ROCK, new Rect2(0, 0, 32, 32), new Vector2(16, 29), 0.6f), new(SNOW_ROCK, new Rect2(0, 0, 32, 32), new Vector2(16, 29), 0.45f) };
    public static readonly SpriteRegion SnowCave = S(176, 114, 64, 77, 32, 74);

    public static SpriteRegion Pick(SpriteRegion[] set, int hash) => set[hash % set.Length];

    public static SpriteRegion Tree(Biome biome, int hash) => biome switch
    {
        Biome.BrightForest => Pick(ForestTrees, hash),
        Biome.Undead => Pick(BoneTrees, hash),
        Biome.Snow => Pick(SnowTrees, hash),
        _ => Pick(Art.Trees, hash),
    };

    public static SpriteRegion DeadTree(Biome biome, int hash) => biome switch
    {
        Biome.BrightForest => Pick(ForestDead, hash),
        Biome.Undead => Pick(BoneTrees, hash),
        Biome.Snow => Pick(SnowTrees, hash),
        _ => Pick(Art.DeadTrees, hash),
    };

    public static SpriteRegion Bush(Biome biome, int hash) => biome switch
    {
        Biome.BrightForest => Pick(ForestBushes, hash),
        Biome.Undead => Pick(BoneBushes, hash),
        Biome.Snow => Pick(SnowBushes, hash),
        _ => Pick(Art.Bushes, hash),
    };

    public static SpriteRegion Rock(Biome biome, int hash) => biome switch
    {
        Biome.BrightForest => Pick(ForestRocks, hash),
        Biome.Undead => Pick(BoneRocks, hash),
        Biome.Snow => Pick(SnowRocks, hash),
        _ => Art.Rock,
    };

    // Ground scatter between the authored placements; Verdant keeps its own ruin scatter.
    public static SpriteRegion[] Scatter(Biome biome) => biome switch
    {
        Biome.BrightForest => ForestPebbles,
        Biome.Undead => BoneScatter,
        _ => SnowStones,
    };

    // Solid blockers dressed in the biome's own look: stands in for Statue/Pillar/BrokenPillar/Idol/Obelisk/Column/RuinWall.
    public static SpriteRegion Monument(Biome biome, PlacementKind kind, int hash)
    {
        switch (biome)
        {
            case Biome.BrightForest:
                return kind switch
                {
                    PlacementKind.RuinWall => ForestWall,
                    PlacementKind.Statue or PlacementKind.Idol => ForestPlinth,
                    _ => Pick(ForestPillars, hash),
                };
            case Biome.Undead:
                return kind switch
                {
                    PlacementKind.RuinWall => BoneRuin,
                    PlacementKind.Statue or PlacementKind.Idol => Hanging,
                    _ => BonePillar,
                };
            default:
                return Pick(Art.Relics, hash);
        }
    }
}
