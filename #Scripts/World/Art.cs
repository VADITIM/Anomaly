using System.Collections.Generic;
using Godot;

namespace Anomaly;

public readonly record struct SpriteRegion(string TexturePath, Rect2 Region, Vector2 Origin, float Scale = 1f)
{
    public Vector2 ScaledSize => Region.Size * Scale;
}

public static class Art
{
    public const string ASSETS = "res://#Assets/";
    public const string GRASS = "Tilesets/Texture/Tileset Grass.png";
    public const string STONE = "Tilesets/Texture/Tileset Stone Ground.png";
    public const string WALL = "Tilesets/Texture/Tileset Wall.png";
    public const string OBJECTS = "Tilesets/Texture/Object Entities.png";
    public const string VEGETATION = "Tilesets/Texture/Vegetation.png";
    public const string RUINS = "Tilesets/Texture/LPC/Ruins.png";

    private static readonly Dictionary<string, Texture2D> textures = new();
    private static readonly Dictionary<string, Font> fonts = new();

    public static Texture2D Texture(string path)
    {
        if (!textures.TryGetValue(path, out Texture2D texture))
        {
            texture = GD.Load<Texture2D>(ASSETS + path);
            textures[path] = texture;
        }
        return texture;
    }

    public static Font Font(string path)
    {
        if (!fonts.TryGetValue(path, out Font font))
        {
            font = GD.Load<Font>(ASSETS + path);
            fonts[path] = font;
        }
        return font;
    }

    public static Font TitleFont => Font("UI/Fonts/Yoster.ttf");
    public static Font BodyFont => Font("UI/Fonts/PortoBuena.otf");
    public static Font NumberFont => BodyFont;

    // Both fonts are pixel fonts: Yoster is drawn on a 12px em grid, Porto Buena on 8px. Any other size lands glyph
    // pixels between screen pixels, so requested sizes snap to the nearest whole multiple of the font's grid.
    private const int TITLE_GRID = 12;
    private const int BODY_GRID = 8;

    public static int PixelSize(Font font, int size)
    {
        int grid = font == TitleFont ? TITLE_GRID : BODY_GRID;
        return grid * Mathf.Max(1, Mathf.RoundToInt((float)size / grid));
    }

    // Screen-space UI may also use three quarters of the grid: one glyph pixel is then 1/360 of the screen height, a whole
    // number of screen pixels at 720p, 1080p, 1440p and 2160p, which lets body text sit smaller than the art's pixel.
    public static int UiPixelSize(Font font, int size)
    {
        int grid = font == TitleFont ? TITLE_GRID : BODY_GRID;
        int step = grid * 3 / 4;
        int fine = step * Mathf.Max(1, Mathf.RoundToInt((float)size / step));
        int coarse = grid * Mathf.Max(1, Mathf.RoundToInt((float)size / grid));
        return Mathf.Abs(coarse - size) < Mathf.Abs(fine - size) ? coarse : fine;
    }

    // Origins mark the sprite pixel that touches the ground, so every prop sorts and casts its shadow from its footprint.
    public static readonly SpriteRegion Crate = new(OBJECTS, new Rect2(160, 18, 32, 46), new Vector2(16, 44));
    public static readonly SpriteRegion Barrel = new(OBJECTS, new Rect2(162, 153, 28, 36), new Vector2(14, 34));
    public static readonly SpriteRegion Vase = new(OBJECTS, new Rect2(165, 217, 21, 34), new Vector2(10, 32));
    public static readonly SpriteRegion Pot = new(OBJECTS, new Rect2(164, 288, 25, 27), new Vector2(12, 25));
    public static readonly SpriteRegion Statue = new(OBJECTS, new Rect2(445, 21, 39, 72), new Vector2(20, 68));
    public static readonly SpriteRegion Pillar = new(OBJECTS, new Rect2(352, 174, 32, 77), new Vector2(16, 72));
    public static readonly SpriteRegion BrokenPillar = new(OBJECTS, new Rect2(416, 194, 32, 57), new Vector2(16, 52));
    public static readonly SpriteRegion Altar = new(OBJECTS, new Rect2(353, 269, 94, 72), new Vector2(47, 50));
    public static readonly SpriteRegion Rock = new(OBJECTS, new Rect2(3, 430, 57, 42), new Vector2(28, 36));
    public static readonly SpriteRegion Chest = new(OBJECTS, new Rect2(96, 30, 32, 31), new Vector2(16, 29));

    public static readonly SpriteRegion[] Trees =
    {
        new(VEGETATION, new Rect2(24, 14, 113, 139), new Vector2(55, 136)),
        new(VEGETATION, new Rect2(161, 17, 95, 136), new Vector2(46, 133)),
        new(VEGETATION, new Rect2(295, 31, 79, 120), new Vector2(41, 117)),
    };

    public static readonly SpriteRegion[] Bushes =
    {
        new(VEGETATION, new Rect2(156, 190, 38, 32), new Vector2(19, 29)),
        new(VEGETATION, new Rect2(216, 185, 47, 42), new Vector2(23, 39)),
        new(VEGETATION, new Rect2(282, 186, 39, 45), new Vector2(19, 42)),
        new(VEGETATION, new Rect2(346, 190, 40, 35), new Vector2(20, 32)),
        new(VEGETATION, new Rect2(98, 195, 27, 25), new Vector2(13, 23)),
    };

    public static readonly Rect2[] GrassTufts =
    {
        new(8, 394, 17, 9), new(41, 394, 16, 10), new(73, 394, 15, 10), new(102, 394, 15, 11),
        new(9, 426, 12, 10), new(43, 427, 13, 9), new(74, 427, 13, 9), new(104, 428, 14, 7),
        new(9, 459, 12, 8), new(41, 458, 12, 10), new(71, 459, 14, 10), new(106, 459, 12, 9),
    };

    // Ruin and dead-wood sprites cut from the LPC Jungle Ruins and LPC Trees packs (see Tilesets/Texture/LPC/README.md).
    public static readonly SpriteRegion DeadTall = new(RUINS, new Rect2(2, 2, 106, 183), new Vector2(52, 170));
    public static readonly SpriteRegion DeadClaw = new(RUINS, new Rect2(110, 2, 128, 156), new Vector2(54, 152));
    public static readonly SpriteRegion DeadHollow = new(RUINS, new Rect2(240, 2, 94, 150), new Vector2(64, 146));
    public static readonly SpriteRegion DeadLeaning = new(RUINS, new Rect2(336, 2, 139, 148), new Vector2(36, 144));
    public static readonly SpriteRegion DeadReaching = new(RUINS, new Rect2(477, 2, 95, 140), new Vector2(43, 131));
    public static readonly SpriteRegion DeadSpire = new(RUINS, new Rect2(574, 2, 57, 140), new Vector2(30, 129));
    public static readonly SpriteRegion Column = new(RUINS, new Rect2(633, 2, 30, 127), new Vector2(14, 125));
    public static readonly SpriteRegion WallRuin = new(RUINS, new Rect2(665, 2, 96, 123), new Vector2(48, 121));
    public static readonly SpriteRegion DeadBranching = new(RUINS, new Rect2(763, 2, 82, 122), new Vector2(38, 105));
    public static readonly SpriteRegion DeadTwisted = new(RUINS, new Rect2(847, 2, 93, 122), new Vector2(32, 115));
    public static readonly SpriteRegion DeadWide = new(RUINS, new Rect2(2, 187, 101, 121), new Vector2(62, 102));
    public static readonly SpriteRegion DeadGnarled = new(RUINS, new Rect2(105, 187, 86, 107), new Vector2(44, 94));
    public static readonly SpriteRegion DeadStubby = new(RUINS, new Rect2(193, 187, 76, 99), new Vector2(36, 86));
    public static readonly SpriteRegion SlabBigB = new(RUINS, new Rect2(271, 187, 92, 93), new Vector2(46, 46.5f));
    public static readonly SpriteRegion IdolWhole = new(RUINS, new Rect2(365, 187, 32, 88), new Vector2(22, 86));
    public static readonly SpriteRegion ObeliskTall = new(RUINS, new Rect2(399, 187, 32, 87), new Vector2(16, 85));
    public static readonly SpriteRegion SlabBigA = new(RUINS, new Rect2(433, 187, 83, 83), new Vector2(41.5f, 41.5f));
    public static readonly SpriteRegion IdolCracked = new(RUINS, new Rect2(518, 187, 33, 66), new Vector2(16, 64));
    public static readonly SpriteRegion IdolSunken = new(RUINS, new Rect2(553, 187, 32, 66), new Vector2(13, 64));
    public static readonly SpriteRegion DeadSapling = new(RUINS, new Rect2(587, 187, 47, 65), new Vector2(26, 58));
    public static readonly SpriteRegion ObeliskShort = new(RUINS, new Rect2(636, 187, 32, 46), new Vector2(16, 44));
    public static readonly SpriteRegion ColumnBase = new(RUINS, new Rect2(670, 187, 30, 39), new Vector2(14, 37));
    public static readonly SpriteRegion RubbleA = new(RUINS, new Rect2(702, 187, 22, 34), new Vector2(10, 32));
    public static readonly SpriteRegion RubbleF = new(RUINS, new Rect2(726, 187, 31, 32), new Vector2(14, 30));
    public static readonly SpriteRegion IdolStump = new(RUINS, new Rect2(759, 187, 32, 32), new Vector2(16, 30));
    public static readonly SpriteRegion ColumnShardA = new(RUINS, new Rect2(793, 187, 24, 32), new Vector2(12, 30));
    public static readonly SpriteRegion ColumnShardB = new(RUINS, new Rect2(819, 187, 24, 32), new Vector2(12, 30));
    public static readonly SpriteRegion RubbleB = new(RUINS, new Rect2(845, 187, 19, 29), new Vector2(9, 27));
    public static readonly SpriteRegion IdolHead = new(RUINS, new Rect2(866, 187, 28, 29), new Vector2(14, 27));
    public static readonly SpriteRegion IdolHeadAngry = new(RUINS, new Rect2(896, 187, 26, 29), new Vector2(12, 27));
    public static readonly SpriteRegion StumpC = new(RUINS, new Rect2(924, 187, 27, 29), new Vector2(14, 23));
    public static readonly SpriteRegion SlabC = new(RUINS, new Rect2(953, 187, 29, 28), new Vector2(14.5f, 14));
    public static readonly SpriteRegion SlabE = new(RUINS, new Rect2(984, 187, 24, 28), new Vector2(12, 14));
    public static readonly SpriteRegion RubbleC = new(RUINS, new Rect2(2, 310, 29, 28), new Vector2(14, 26));
    public static readonly SpriteRegion RubbleG = new(RUINS, new Rect2(33, 310, 30, 28), new Vector2(12, 26));
    public static readonly SpriteRegion StumpB = new(RUINS, new Rect2(65, 310, 22, 28), new Vector2(11, 20));
    public static readonly SpriteRegion RubbleD = new(RUINS, new Rect2(89, 310, 30, 27), new Vector2(19, 25));
    public static readonly SpriteRegion StumpA = new(RUINS, new Rect2(121, 310, 16, 26), new Vector2(8, 19));
    public static readonly SpriteRegion SlabA = new(RUINS, new Rect2(139, 310, 25, 25), new Vector2(12.5f, 12.5f));
    public static readonly SpriteRegion RubbleE = new(RUINS, new Rect2(166, 310, 21, 24), new Vector2(11, 22));
    public static readonly SpriteRegion SlabB = new(RUINS, new Rect2(189, 310, 25, 23), new Vector2(12.5f, 11.5f));
    public static readonly SpriteRegion ThornA = new(RUINS, new Rect2(216, 310, 27, 23), new Vector2(14, 19));
    public static readonly SpriteRegion ThornB = new(RUINS, new Rect2(245, 310, 31, 23), new Vector2(16, 19));
    public static readonly SpriteRegion ThornC = new(RUINS, new Rect2(278, 310, 27, 23), new Vector2(12, 19));
    public static readonly SpriteRegion SlabD = new(RUINS, new Rect2(307, 310, 27, 22), new Vector2(13.5f, 11));
    public static readonly SpriteRegion SlabF = new(RUINS, new Rect2(336, 310, 18, 18), new Vector2(9, 9));
    public static readonly SpriteRegion RubbleH = new(RUINS, new Rect2(356, 310, 16, 15), new Vector2(8, 13));
    public static readonly SpriteRegion PebbleA = new(RUINS, new Rect2(374, 310, 12, 10), new Vector2(6, 5));
    public static readonly SpriteRegion PebbleB = new(RUINS, new Rect2(388, 310, 7, 7), new Vector2(3.5f, 3.5f));
    public static readonly SpriteRegion PebbleC = new(RUINS, new Rect2(397, 310, 7, 7), new Vector2(3.5f, 3.5f));
    public static readonly SpriteRegion PebbleD = new(RUINS, new Rect2(406, 310, 8, 5), new Vector2(4, 2.5f));

    public static readonly SpriteRegion[] DeadTrees = { DeadTall, DeadClaw, DeadHollow, DeadLeaning, DeadReaching, DeadBranching, DeadTwisted, DeadWide, DeadGnarled, DeadStubby };
    public static readonly SpriteRegion[] DeadShrubs = { DeadSapling, StumpA, StumpB, StumpC, ThornA, ThornB, ThornC };
    public static readonly SpriteRegion[] Rubble = { RubbleA, RubbleB, RubbleC, RubbleD, RubbleE, RubbleF, RubbleG, RubbleH, ColumnShardA, ColumnShardB };
    public static readonly SpriteRegion[] Relics = { IdolCracked, IdolSunken, IdolStump, IdolHead, IdolHeadAngry, ObeliskShort, ColumnBase };
    public static readonly SpriteRegion[] Slabs = { SlabA, SlabB, SlabC, SlabD, SlabE, SlabF };
    public static readonly SpriteRegion[] Pebbles = { PebbleA, PebbleB, PebbleC, PebbleD };

    public static Rect2 TileRect(int column, int row) => new(column * 32, row * 32, 32, 32);

    public static Sprite2D CreateSprite(SpriteRegion region)
    {
        return new Sprite2D
        {
            Texture = Texture(region.TexturePath),
            RegionEnabled = true,
            RegionRect = region.Region,
            Centered = false,
            Offset = -region.Origin,
            Scale = Vector2.One * region.Scale,
        };
    }

    public static int Hash(int x, int y, int salt = 0)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h & 0x7fffffff;
        }
    }
}
