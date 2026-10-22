using System.Collections.Generic;
using Godot;

namespace Anomaly;

public enum PlacementKind
{
    PlayerStart,
    RendAnvil,
    Bonfire,
    Npc,
    VoidHeart,
    VoidRift,
    Rotling,
    Hound,
    Wasp,
    Brute,
    TargetDummy,
    Crate,
    Barrel,
    Vase,
    Pot,
    SoulCache,
    Tree,
    Bush,
    Statue,
    Pillar,
    BrokenPillar,
    Rock,
    Brazier,
    RuinWall,
    Idol,
    Obelisk,
    Column,
    DeadTree,
    SkeletonKing,
    SkullPile,
    Tower,
    Fountain,
    Altar,
    SnowCave,
    Gnawer,
    BloatedGnawer,
    GnawerNest,
    Bonewalker,
    Rimebound,
    Rimeslime,
    Bogslime,
    Slimelet,
    GloomBat,
    Shadeflit,
    ShortcutGate,
    Fracture,
    SealedFracture,
}

// Id names a Void Heart, a shortcut gate's save flag or a Fracture's destination; Side is the side a gate opens from.
public readonly record struct Placement(PlacementKind Kind, Vector2I Tile, string Id = null, Vector2I Side = default);

public readonly record struct NarrativeTrigger(string Id, Rect2I Area, string Line, string RegionTitle, string RegionSubtitle);

public readonly record struct MapRegion(string Name, Biome Biome, Rect2I Area);

// A screen section: one room, road or crossing the camera frames on its own. Region groups them in the scene tree.
public readonly record struct SectionPlan(string Region, string Name, Rect2I Area);

public sealed class LevelBlueprint
{
    public const string VOID_HEART_ID = "verdanya_void_heart_01";
    private const int WORLD_WIDTH = 184;
    private const int WORLD_HEIGHT = 160;
    private const int VOID_WIDTH = 96;
    private const int VOID_HEIGHT = 80;

    // Hollow Reach is the hub: the most broken land in Verdanya, torn open by a Corrupted Fracture on every side.
    // Each continent is authored in its own local frame around it: Glaciemera north, Èrmiosa east, Elaphrium south,
    // and the sealed Azurean Verge west.
    public static readonly Vector2I HollowReachOrigin = new(50, 58);
    private static readonly Vector2I FrostOrigin = new(40, 1);
    private static readonly Vector2I BonesOrigin = new(122, 44);
    private static readonly Vector2I GroveOrigin = new(40, 110);
    private static readonly Vector2I VergeOrigin = new(1, 58);
    private static readonly Rect2I FrostFrame = new(FrostOrigin, new Vector2I(79, 49));
    private static readonly Rect2I BonesFrame = new(BonesOrigin, new Vector2I(61, 75));
    private static readonly Rect2I GroveFrame = new(GroveOrigin, new Vector2I(79, 49));
    private static readonly Rect2I VergeFrame = new(VergeOrigin, new Vector2I(44, 46));

    public WorldGrid Grid { get; }
    public List<Placement> Placements { get; } = new();
    public List<NarrativeTrigger> Triggers { get; } = new();
    public List<MapRegion> Regions { get; } = new();
    public List<SectionPlan> Sections { get; } = new();

    private Vector2I origin = Vector2I.Zero;
    private string region;

    private LevelBlueprint(int width, int height)
    {
        Grid = new WorldGrid(width, height);
    }

    public static LevelBlueprint CreateVerdanyaReach()
    {
        var level = new LevelBlueprint(WORLD_WIDTH, WORLD_HEIGHT);
        level.Fill(0, 0, WORLD_WIDTH - 1, WORLD_HEIGHT - 1, WorldGrid.WALL_HEIGHT, FloorKind.Grass);

        level.origin = HollowReachOrigin;
        level.BuildHollowReach();
        level.origin = Vector2I.Zero;
        level.BuildFrostwatch();
        level.BuildBonefields();
        level.BuildAurelianGrove();
        level.BuildAzureanVerge();
        level.AssignBiomes();
        level.ReshapeBoundaries();
        level.BuildRegions();
        level.Erode();
        level.ReportConnectivity();
        level.ReportSections();
        return level;
    }

    // The Void, reached through any open Fracture. Every land it has bitten drifts here as a shard, stitched to the others
    // with roads of Void glass. Graph: the Wound (arrival Fracture, portal) -> west road -> Frost Shard (Glaciemera)
    // -> Hanging Road (sleeping bat) -> Reach Shard (Verdanya; raised ledge with a Soul Cache) -> east bridge -> the
    // Convergence (every land at once, Brute on a dais) -> gate road south, opened from the Convergence side back onto the
    // Wound. Detours: the Bone Shard (Èrmiosa) east of the Convergence, a Gnawer nest guarding a Soul Cache, and past it a
    // one-tile thread over the abyss to the Gilded Shard (Elaphrium), a dead end with a Soul Cache and lore.
    public static LevelBlueprint CreateVoid()
    {
        var level = new LevelBlueprint(VOID_WIDTH, VOID_HEIGHT);
        level.Pit(0, 0, VOID_WIDTH - 1, VOID_HEIGHT - 1);
        level.BuildVoid();
        level.AssignVoidBiomes();
        level.Erode();
        level.ReportConnectivity();
        level.ReportSections();
        return level;
    }

    private void BuildVoid()
    {
        // Shards: one per land the Void has bitten, plus the Wound and the Convergence where the lands meet.
        Blob(48, 66, 7, 5, 0, FloorKind.Grass, 41);
        Fill(46, 64, 50, 68, 0, FloorKind.Paved);
        Blob(22, 62, 9, 7, 0, FloorKind.Grass, 42);
        Blob(24, 33, 10, 7, 0, FloorKind.Grass, 43);
        Fill(15, 28, 19, 30, 1, FloorKind.Stone);
        Blob(52, 28, 11, 8, 0, FloorKind.Grass, 44);
        Fill(50, 23, 54, 26, 1, FloorKind.Stone);
        Blob(78, 34, 8, 8, 0, FloorKind.Grass, 45);
        Blob(78, 61, 5, 4, 0, FloorKind.Grass, 46);

        // Roads of Void glass between them.
        Fill(28, 66, 42, 67, 0, FloorKind.Paved);
        Fill(21, 39, 23, 56, 0, FloorKind.Paved);
        Fill(32, 30, 43, 31, 0, FloorKind.Paved);
        Fill(47, 36, 49, 60, 0, FloorKind.Paved);
        Fill(62, 31, 71, 32, 0, FloorKind.Paved);
        Fill(78, 41, 78, 58, 0, FloorKind.Paved);

        // The Wound: where Kio lands, and the Fracture home.
        Place(PlacementKind.PlayerStart, 48, 67);
        Place(PlacementKind.Fracture, 48, 63, "Hollow Reach");
        Place(PlacementKind.Bonfire, 44, 66);
        Gate(48, 57, "void_wound_gate", 0, -1);
        PlaceMany(PlacementKind.Pot, (53, 68), (54, 66));

        // Frost Shard.
        Place(PlacementKind.SnowCave, 17, 57);
        PlaceMany(PlacementKind.Rimebound, (20, 60), (26, 64));
        PlaceMany(PlacementKind.Rimeslime, (17, 65), (25, 59));
        Place(PlacementKind.Hound, 16, 62);
        PlaceMany(PlacementKind.Tree, (15, 59), (24, 67));
        PlaceMany(PlacementKind.Rock, (19, 68), (24, 56));
        Place(PlacementKind.Bush, 15, 67);
        PlaceMany(PlacementKind.Vase, (14, 59), (14, 60));

        // The Hanging Road: a bat sleeps over the narrowest stretch, in plain sight.
        Place(PlacementKind.GloomBat, 22, 47);

        // Reach Shard: Shinōki ruins, with a raised ledge that looks out over the whole Void.
        PlaceMany(PlacementKind.Rotling, (20, 36), (28, 31), (29, 36));
        PlaceMany(PlacementKind.Wasp, (24, 29), (18, 34));
        Place(PlacementKind.Shadeflit, 31, 34);
        Place(PlacementKind.RuinWall, 24, 28);
        PlaceMany(PlacementKind.Pillar, (21, 31), (27, 35));
        Place(PlacementKind.BrokenPillar, 25, 38);
        Place(PlacementKind.Obelisk, 31, 32);
        Place(PlacementKind.Idol, 16, 29);
        Place(PlacementKind.SoulCache, 18, 29);
        PlaceMany(PlacementKind.Tree, (15, 34), (25, 39));
        PlaceMany(PlacementKind.DeadTree, (31, 36), (20, 39));

        // The Convergence: every land at once, and the worst of each.
        Place(PlacementKind.SkeletonKing, 52, 21);
        Place(PlacementKind.Brute, 52, 25);
        PlaceMany(PlacementKind.Bonewalker, (45, 29), (59, 30));
        Place(PlacementKind.Bogslime, 49, 33);
        Place(PlacementKind.Hound, 57, 22);
        Place(PlacementKind.Shadeflit, 44, 25);
        Place(PlacementKind.Wasp, 60, 26);
        PlaceMany(PlacementKind.Obelisk, (43, 28), (61, 29));
        PlaceMany(PlacementKind.Column, (47, 23), (58, 23));
        PlaceMany(PlacementKind.DeadTree, (42, 32), (60, 27));
        PlaceMany(PlacementKind.Tree, (55, 34), (45, 34));

        // Bone Shard: a nest beside the cache it guards.
        Place(PlacementKind.GnawerNest, 82, 30);
        Place(PlacementKind.SoulCache, 84, 32);
        PlaceMany(PlacementKind.Bonewalker, (74, 31), (80, 38));
        Place(PlacementKind.Gnawer, 75, 37);
        Place(PlacementKind.BloatedGnawer, 84, 36);
        Place(PlacementKind.Statue, 78, 28);
        PlaceMany(PlacementKind.Tree, (72, 35), (82, 39));
        Place(PlacementKind.Rock, 76, 27);

        // The Thread: one tile over the abyss to the Gilded Shard, a bat hanging halfway.
        Place(PlacementKind.GloomBat, 78, 50);
        Place(PlacementKind.SoulCache, 80, 62);
        Place(PlacementKind.Altar, 76, 62);
        PlaceMany(PlacementKind.Bogslime, (75, 60), (81, 60));
        PlaceMany(PlacementKind.Tree, (74, 61), (82, 59));
        Place(PlacementKind.Bush, 79, 64);

        Trigger("void_arrival", new Rect2I(38, 58, 21, 17),
            "So this is where the Fractures lead. Every land the Void bites ends up here, Hunter, stitched to all the others.", "THE VOID", "Where the Fractures lead");
        Trigger("void_frost", new Rect2I(9, 50, 22, 25), null, "FROST SHARD", "Torn from Glaciemera");
        Trigger("void_reach", new Rect2I(9, 22, 29, 22), null, "REACH SHARD", "Torn from Verdanya");
        Trigger("void_convergence", new Rect2I(38, 16, 29, 23),
            "Snow against bone, gold against rot. The Void does not tear, Hunter. It collects.", "THE CONVERGENCE", "Every land at once");
        Trigger("void_gate", new Rect2I(44, 39, 9, 10),
            "The Wound lies just below. Lift the bar, and the Void will have to let you back the short way.", null, null);
        Trigger("void_bones", new Rect2I(67, 21, 24, 21), null, "BONE SHARD", "Torn from Èrmiosa");
        Trigger("void_gilded", new Rect2I(70, 56, 17, 13),
            "A piece of Elaphrium, still warm. It was swallowed before it could even fall.", "GILDED SHARD", "Torn from Elaphrium");

        region = "The Void";
        Section("The Wound", 38, 58, 58, 74);
        Section("Gate Road", 44, 39, 52, 57);
        Section("The Convergence", 38, 16, 66, 38);
        Section("West Road", 31, 55, 37, 72);
        Section("Frost Shard", 9, 50, 30, 74);
        Section("Hanging Road", 17, 44, 27, 49);
        Section("Reach Shard", 9, 22, 37, 43);
        Section("Bone Shard", 67, 21, 90, 41);
        Section("The Thread", 72, 42, 84, 55);
        Section("Gilded Shard", 70, 56, 86, 68);

        Regions.Add(new MapRegion("The Wound", Biome.Void, new Rect2I(38, 58, 21, 17)));
        Regions.Add(new MapRegion("Frost Shard", Biome.Void, new Rect2I(9, 50, 22, 25)));
        Regions.Add(new MapRegion("Reach Shard", Biome.Void, new Rect2I(9, 22, 29, 22)));
        Regions.Add(new MapRegion("The Convergence", Biome.Void, new Rect2I(38, 16, 29, 23)));
        Regions.Add(new MapRegion("Bone Shard", Biome.Void, new Rect2I(67, 21, 24, 21)));
        Regions.Add(new MapRegion("Gilded Shard", Biome.Void, new Rect2I(70, 56, 17, 13)));
    }

    // All of it is Void ground. Each shard remembers its own land; the Wound, the Convergence and the roads remember a
    // different land patch by patch, so everything breaks together there.
    private void AssignVoidBiomes()
    {
        Biome[] lands = { Biome.Verdant, Biome.BrightForest, Biome.Undead, Biome.Snow };
        (Rect2I Area, Biome Land)[] shards =
        {
            (new Rect2I(9, 50, 22, 25), Biome.Snow),
            (new Rect2I(9, 22, 29, 22), Biome.Verdant),
            (new Rect2I(67, 21, 24, 21), Biome.Undead),
            (new Rect2I(70, 56, 17, 13), Biome.BrightForest),
        };
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                var tile = new Vector2I(x, y);
                Biome echo = lands[Art.Hash(x / 4, y / 3, 950) % lands.Length];
                foreach ((Rect2I area, Biome land) in shards)
                    if (area.HasPoint(tile))
                        echo = land;
                Grid.SetBiome(x, y, Biome.Void);
                Grid.SetEcho(x, y, echo);
            }
    }

    private void BuildHollowReach()
    {
        Fill(3, 30, 21, 41, 0, FloorKind.Grass);
        Fill(6, 33, 11, 37, 0, FloorKind.Paved);

        Fill(5, 15, 11, 29, 0, FloorKind.Grass);
        Fill(7, 15, 9, 29, 0, FloorKind.Paved);
        Fill(10, 18, 11, 22, 1, FloorKind.Stone);

        Fill(3, 3, 26, 14, 0, FloorKind.Grass);
        Fill(11, 11, 18, 13, 0, FloorKind.Paved);
        Fill(4, 4, 9, 7, 1, FloorKind.Stone);
        Fill(19, 3, 25, 8, 1, FloorKind.Stone);
        Fill(21, 4, 24, 6, 2, FloorKind.Stone);
        Pit(13, 8, 16, 10);

        Pit(27, 2, 36, 17);
        Fill(27, 8, 36, 9, 0, FloorKind.Paved);

        Fill(37, 3, 57, 20, 0, FloorKind.Grass);
        Fill(42, 3, 57, 12, 1, FloorKind.Stone);
        Fill(47, 3, 57, 7, 2, FloorKind.Stone);
        Fill(38, 14, 42, 16, 0, FloorKind.Paved);

        Pit(22, 21, 58, 42);
        Fill(46, 21, 52, 26, 0, FloorKind.Grass);
        Fill(37, 25, 44, 30, 0, FloorKind.Grass);
        Fill(28, 29, 35, 35, 0, FloorKind.Grass);
        Fill(22, 32, 27, 33, 0, FloorKind.Paved);
        Fill(46, 29, 47, 30, 0, FloorKind.Paved);
        Fill(46, 32, 51, 36, 0, FloorKind.Grass);

        // Fracture spans: north from the glade to Glaciemera, east from the ruin terrace to Èrmiosa,
        // south from the sanctuary to Elaphrium and west from the sanctuary toward the sealed Azurean tear.
        Fill(14, -14, 16, 3, 0, FloorKind.Paved);
        Fill(52, 14, 76, 16, 0, FloorKind.Paved);
        Fill(11, 40, 13, 58, 0, FloorKind.Paved);
        Fill(-12, 35, 3, 37, 0, FloorKind.Paved);
        Place(PlacementKind.Fracture, 18, 4, "Glaciemera");
        Place(PlacementKind.Fracture, 56, 18, "Èrmiosa");
        Place(PlacementKind.Fracture, 15, 40, "Elaphrium");
        Place(PlacementKind.Fracture, 4, 39, "Azurean");

        Place(PlacementKind.PlayerStart, 11, 36);
        Place(PlacementKind.RendAnvil, 8, 34);
        Place(PlacementKind.Bonfire, 8, 38);
        Place(PlacementKind.TargetDummy, 16, 35);

        // Sanctuary villagers: dummies for the narration box. Two ask yes/no, two just talk (see Dialogues).
        Place(PlacementKind.Npc, 13, 37, Dialogues.SCRIBE);
        Place(PlacementKind.Npc, 4, 34, Dialogues.WAYFARER);
        Place(PlacementKind.Npc, 18, 36, Dialogues.WATCHER);
        Place(PlacementKind.Npc, 17, 40, Dialogues.DRIFTER);

        PlaceMany(PlacementKind.Tree, (4, 31), (4, 39), (15, 40), (19, 31), (19, 38), (13, 31), (5, 19), (11, 27), (25, 11), (3, 9), (38, 5), (39, 11), (56, 19), (38, 19), (30, 34), (50, 25), (38, 29), (51, 35));
        PlaceMany(PlacementKind.Bush, (6, 40), (17, 33), (20, 35), (5, 29), (11, 16), (4, 12), (24, 13), (12, 4), (40, 9), (55, 17), (47, 22), (43, 26), (29, 30), (48, 33));
        PlaceMany(PlacementKind.Rock, (16, 37), (3, 35), (10, 24), (22, 9), (37, 17), (52, 21), (34, 31));
        PlaceMany(PlacementKind.Statue, (14, 34), (50, 5), (55, 5));
        PlaceMany(PlacementKind.Pillar, (11, 4), (17, 4), (43, 13), (47, 13), (51, 13), (37, 12));
        PlaceMany(PlacementKind.BrokenPillar, (14, 5), (12, 14), (45, 8), (55, 11));

        PlaceMany(PlacementKind.Vase, (6, 36), (12, 33), (6, 28));
        PlaceMany(PlacementKind.Pot, (10, 37), (25, 14), (53, 9));
        PlaceMany(PlacementKind.Crate, (10, 19), (11, 21), (4, 13), (5, 13), (47, 33), (50, 34), (41, 16), (57, 13));
        PlaceMany(PlacementKind.Barrel, (6, 27), (24, 12), (25, 13), (39, 15));
        Place(PlacementKind.SoulCache, 49, 35);

        PlaceMany(PlacementKind.Brazier, (6, 32), (11, 32), (11, 12), (18, 12), (45, 13), (49, 13), (40, 13));

        PlaceMany(PlacementKind.Rotling, (8, 25), (7, 21), (9, 17), (6, 10), (45, 9), (54, 10), (49, 24), (30, 31), (33, 33));
        PlaceMany(PlacementKind.Hound, (13, 12), (19, 12), (40, 15), (44, 17), (41, 28));
        PlaceMany(PlacementKind.Wasp, (6, 5), (22, 5), (31, 6), (55, 9), (40, 27));
        Place(PlacementKind.Brute, 49, 16);
        // The Fractures bleed into the Reach: shades on the islands, a bat asleep over the void bridge.
        PlaceMany(PlacementKind.Shadeflit, (40, 27), (31, 33));
        Place(PlacementKind.GloomBat, 32, 9);

        PlaceMany(PlacementKind.RuinWall, (16, 31), (14, 3), (40, 3));
        PlaceMany(PlacementKind.Idol, (20, 33), (8, 12), (42, 18), (56, 15), (46, 4), (35, 30));
        PlaceMany(PlacementKind.Obelisk, (20, 10), (43, 4), (57, 9), (5, 22));
        PlaceMany(PlacementKind.Column, (15, 39), (24, 4), (53, 13));
        PlaceMany(PlacementKind.DeadTree, (21, 36), (41, 19), (44, 29), (52, 23), (28, 35), (46, 36), (3, 5));

        Heart(52, 4, VOID_HEART_ID);
        PlaceMany(PlacementKind.VoidRift, (49, 6), (55, 6), (52, 7));

        Trigger("voice_corridor", new Rect2I(5, 24, 7, 6),
            "The corrupted remember nothing of what they were. Grant them an ending, Hunter.", null, null);
        Trigger("region_glade", new Rect2I(3, 3, 24, 12), null, "THE SUNKEN GLADE", "Where the ground forgot to hold");
        Trigger("voice_ruins", new Rect2I(37, 3, 21, 18),
            "There. A Void Heart, beating above the ruins. Cleanse it, and this land may breathe again.", "SHINŌKI RUINS", "The last terrace");
        Trigger("voice_islands", new Rect2I(37, 21, 16, 10),
            "The ground here has already been taken. Step carefully. The Void keeps what falls.", null, null);

        // Screen sections: the sanctuary, each room, the void bridge, the island hops and each Fracture span.
        region = "Hollow Reach";
        Section("Sanctuary", 3, 29, 21, 41);
        Section("Ash Corridor", 3, 15, 12, 28);
        Section("Sunken Glade", 3, 2, 26, 14);
        Section("North Span", 12, -8, 18, 1);
        Section("Void Bridge", 27, 4, 36, 12);
        Section("Shinōki Ruins", 37, 0, 57, 20);
        Section("East Span", 58, 12, 72, 18);
        Section("Drowned Islands", 22, 21, 53, 37);
        Section("South Span", 9, 42, 15, 51);
    }

    // Glaciemera, reached by the north Fracture. Graph: Landing (portal) -> Wind Steppe -> Rimebound Watch (east, vista ledge)
    // -> Snowbound Pantheon (Void Heart) -> west road -> Frozen Ravine (sleeping-bat gauntlet) -> shortcut gate opened from the
    // ravine side back onto the Landing. Detours: a south-east knoll with lore and pots off the Landing, and a one-tile ice
    // bridge east of the Pantheon to a Soul Cache islet with a bat hanging over it (bait).
    private void BuildFrostwatch()
    {
        origin = FrostOrigin;
        Blob(25, 42, 8, 5, 0, FloorKind.Grass, 1);
        Blob(29, 29, 12, 6, 0, FloorKind.Grass, 2);
        Fill(25, 33, 27, 40, 0, FloorKind.Paved);
        Blob(54, 26, 9, 6, 0, FloorKind.Grass, 3);
        Blob(57, 24, 4, 3, 1, FloorKind.Stone, 4);
        Fill(38, 27, 47, 29, 0, FloorKind.Paved);
        Blob(42, 9, 12, 5, 0, FloorKind.Grass, 5);
        Fill(52, 13, 54, 21, 0, FloorKind.Paved);
        Fill(2, 3, 12, 9, 0, FloorKind.Grass);
        Fill(9, 7, 31, 9, 0, FloorKind.Paved);
        Fill(3, 8, 8, 38, 0, FloorKind.Grass);
        Fill(3, 39, 20, 41, 0, FloorKind.Paved);
        Fill(55, 8, 65, 8, 0, FloorKind.Paved);
        Blob(70, 8, 5, 4, 0, FloorKind.Grass, 7);
        Blob(41, 43, 4, 3, 0, FloorKind.Grass, 8);
        Fill(32, 42, 38, 43, 0, FloorKind.Paved);

        Place(PlacementKind.Bonfire, 20, 42);
        Gate(13, 40, "frost_ravine_gate", -1, 0);
        Place(PlacementKind.SnowCave, 7, 4);
        Heart(42, 8, "glaciemera_void_heart");
        PlaceMany(PlacementKind.VoidRift, (37, 10), (47, 10), (42, 12));
        Place(PlacementKind.SoulCache, 71, 8);

        PlaceMany(PlacementKind.Tree, (19, 26), (38, 33), (33, 23), (46, 22), (61, 30), (49, 30), (31, 5), (53, 6), (4, 12), (7, 20), (18, 46), (32, 46), (73, 6));
        PlaceMany(PlacementKind.Bush, (22, 32), (36, 25), (24, 37), (58, 30), (45, 12), (6, 30), (16, 41), (43, 44), (68, 10));
        PlaceMany(PlacementKind.Rock, (27, 26), (34, 32), (50, 26), (39, 12), (4, 18), (7, 36), (29, 45));
        PlaceMany(PlacementKind.Pillar, (34, 6), (50, 6), (34, 12), (50, 12));
        PlaceMany(PlacementKind.BrokenPillar, (38, 5), (46, 5), (62, 26));
        // Snow throws light back hard: Frostwatch gets few, spaced lights.
        Place(PlacementKind.Brazier, 11, 39);
        PlaceMany(PlacementKind.Crate, (60, 23), (61, 23));
        PlaceMany(PlacementKind.Barrel, (30, 27), (52, 29));
        PlaceMany(PlacementKind.Vase, (42, 42), (40, 44));
        PlaceMany(PlacementKind.Pot, (43, 43), (6, 5));

        PlaceMany(PlacementKind.Hound, (22, 28), (33, 31), (30, 25));
        PlaceMany(PlacementKind.Rimeslime, (25, 30), (36, 28), (60, 25), (6, 26));
        PlaceMany(PlacementKind.Rimebound, (54, 26), (58, 28), (38, 10), (46, 7), (5, 35));
        Place(PlacementKind.Wasp, 50, 23);
        Place(PlacementKind.Hound, 44, 12);
        PlaceMany(PlacementKind.GloomBat, (5, 14), (6, 22), (5, 30), (61, 8));

        Trigger("region_frost", new Rect2I(0, 0, 79, 49), null, "FROSTWATCH", "Glaciemera, past the Fracture");
        Trigger("voice_frost", new Rect2I(18, 22, 22, 12),
            "Cold does not cleanse, Hunter. It only delays the rot. Their dead stay frozen in the shape they fell in.", null, null);
        Trigger("voice_pantheon", new Rect2I(30, 3, 25, 10),
            "The Snowbound Pantheon. They fought here for glory once. Now something else beats beneath it.", "SNOWBOUND PANTHEON", "Glaciemera's heart");
        Trigger("voice_ravine", new Rect2I(2, 10, 8, 10),
            "Hush. Things hang from the ice here, sleeping. Do not wake what you do not have to.", null, null);
        Trigger("voice_knoll", new Rect2I(37, 40, 8, 6),
            "Someone kept watch from this rock until the cold took them. They were watching the Reach.", null, null);

        // Screen sections: one per room, and every long road or crossing gets its own.
        region = "Glaciemera";
        Section("Landing", 11, 36, 32, 48);
        Section("Watcher's Knoll", 33, 38, 47, 48);
        Section("Wind Steppe", 16, 19, 43, 35);
        Section("Rimebound Watch", 44, 15, 65, 33);
        Section("Snowbound Pantheon", 28, 1, 56, 14);
        Section("Cache Islet", 57, 1, 77, 14);
        Section("Frozen Road", 1, 1, 27, 11);
        Section("Frozen Ravine", 1, 12, 10, 42);
        origin = Vector2I.Zero;
    }

    // Èrmiosa, reached by the east Fracture: the Lower Empire's watch-posts above, its burial pits below.
    // Graph: Landing (bonfire) -> Watch-post (Gnawer nest ambush) -> Ossuary Field (north, Skeleton King, Bonewalkers)
    // -> Higher Empire sanctum (east, Void Heart) -> Burial Pits (south, narrow causeways, sleeping bats) -> shortcut gate
    // opened from the pits back onto the Watch-post. Detour: a gallows alcove west of the pits with a Soul Cache over a nest (bait).
    private void BuildBonefields()
    {
        origin = BonesOrigin;
        Blob(7, 29, 5, 4, 0, FloorKind.Grass, 11);
        Blob(21, 29, 8, 6, 0, FloorKind.Grass, 12);
        Fill(10, 28, 16, 30, 0, FloorKind.Paved);
        Blob(24, 9, 14, 6, 0, FloorKind.Grass, 13);
        Fill(20, 14, 22, 23, 0, FloorKind.Paved);
        Blob(50, 30, 7, 9, 0, FloorKind.Grass, 14);
        Fill(36, 6, 50, 8, 0, FloorKind.Paved);
        Fill(48, 8, 50, 22, 0, FloorKind.Paved);
        Fill(10, 46, 46, 68, 0, FloorKind.Grass);
        Pit(14, 50, 19, 54);
        Pit(26, 49, 31, 55);
        Pit(36, 51, 41, 57);
        Pit(16, 59, 23, 64);
        Pit(29, 60, 37, 65);
        Fill(49, 38, 51, 58, 0, FloorKind.Paved);
        Fill(44, 56, 51, 58, 0, FloorKind.Paved);
        Fill(20, 36, 22, 46, 0, FloorKind.Paved);
        Blob(5, 65, 4, 4, 0, FloorKind.Grass, 15);
        Fill(7, 62, 11, 64, 0, FloorKind.Paved);

        Place(PlacementKind.Bonfire, 5, 26);
        Gate(21, 41, "bones_pits_gate", 0, 1);
        Place(PlacementKind.SkeletonKing, 14, 6);
        Place(PlacementKind.SkullPile, 30, 12);
        Heart(51, 28, "ermiosa_void_heart");
        PlaceMany(PlacementKind.VoidRift, (47, 31), (55, 31), (51, 35));
        Place(PlacementKind.SoulCache, 5, 66);
        PlaceMany(PlacementKind.GnawerNest, (16, 33), (6, 68));

        PlaceMany(PlacementKind.Tree, (26, 24), (14, 25), (34, 5), (10, 10), (55, 24), (45, 34), (12, 48), (44, 66), (25, 67), (2, 63));
        PlaceMany(PlacementKind.Bush, (18, 25), (27, 33), (20, 4), (38, 12), (54, 38), (24, 57), (40, 47), (11, 66));
        PlaceMany(PlacementKind.Rock, (9, 27), (25, 31), (17, 12), (31, 8), (46, 26), (34, 47), (13, 57));
        PlaceMany(PlacementKind.Statue, (28, 6), (46, 28), (56, 28));
        PlaceMany(PlacementKind.Pillar, (47, 24), (55, 24), (17, 24));
        PlaceMany(PlacementKind.BrokenPillar, (24, 21), (33, 50), (12, 52));
        PlaceMany(PlacementKind.RuinWall, (56, 37), (16, 3));
        Place(PlacementKind.Idol, 3, 63);
        PlaceMany(PlacementKind.Brazier, (8, 25), (8, 33), (22, 15), (48, 22), (50, 22), (20, 44), (22, 44));
        PlaceMany(PlacementKind.Crate, (27, 27), (28, 27), (39, 64));
        PlaceMany(PlacementKind.Barrel, (13, 7), (52, 39));
        PlaceMany(PlacementKind.Pot, (30, 10), (42, 49), (11, 47));

        PlaceMany(PlacementKind.Gnawer, (18, 27), (24, 32), (14, 57), (44, 50), (12, 66));
        PlaceMany(PlacementKind.Bonewalker, (24, 28), (20, 8), (28, 11), (32, 6), (30, 58), (50, 34));
        Place(PlacementKind.Brute, 24, 12);
        PlaceMany(PlacementKind.BloatedGnawer, (11, 10), (40, 62));
        PlaceMany(PlacementKind.GloomBat, (23, 51), (34, 57), (26, 62), (43, 54));

        Trigger("region_bones", new Rect2I(0, 0, 61, 75), null, "THE BONEFIELDS", "Èrmiosa, where an empire buried its own");
        Trigger("voice_bones", new Rect2I(14, 22, 14, 12),
            "Listen to the bones. Rats nest in what the Lower Empire left behind, and they do not like company.", null, null);
        Trigger("voice_ossuary", new Rect2I(8, 2, 32, 12),
            "They kept their dead to honour them. It kept them, Hunter. Break what stands back up.", null, null);
        Trigger("voice_pits", new Rect2I(10, 46, 36, 8),
            "The burial pits. The ground between the graves is narrow, and the things above them are light sleepers.", "BURIAL PITS", "Below the Higher Empire");

        // Screen sections: one per room, and every long road or crossing gets its own.
        region = "Èrmiosa";
        Section("Landing", 1, 22, 11, 36);
        Section("Watch-post", 12, 21, 32, 36);
        Section("Ossuary", 6, 0, 40, 20);
        Section("Sanctum Road", 41, 3, 53, 19);
        Section("Higher Sanctum", 41, 20, 60, 40);
        Section("Sanctum Descent", 47, 41, 54, 59);
        Section("Pits Gate", 17, 37, 25, 44);
        Section("Burial Pits", 10, 45, 46, 70);
        Section("Gallows Alcove", -1, 59, 9, 70);
        origin = Vector2I.Zero;
    }

    // Elaphrium, reached by the south Fracture. The front gate of the first walls is barred from inside, so the obvious road
    // is a dead end the first time. Graph: Landing (bonfire) -> barred Great Gate (closed) / east road -> Golden Wood
    // -> Fountain Court (release: second bonfire) -> Altar of the First (Void Heart) -> inner road -> open the Great Gate
    // from inside onto the Landing. Detour: a western thicket off the Altar with a Soul Cache and a Gnawer nest.
    private void BuildAurelianGrove()
    {
        origin = GroveOrigin;
        Blob(22, 6, 6, 4, 0, FloorKind.Grass, 21);
        Fill(21, 9, 23, 31, 0, FloorKind.Paved);
        Fill(28, 5, 51, 7, 0, FloorKind.Paved);
        Blob(54, 11, 10, 6, 0, FloorKind.Grass, 22);
        Fill(55, 14, 57, 30, 0, FloorKind.Paved);
        Blob(52, 34, 11, 7, 0, FloorKind.Grass, 23);
        Blob(22, 37, 9, 6, 0, FloorKind.Grass, 24);
        Fill(30, 33, 46, 35, 0, FloorKind.Paved);
        Blob(5, 38, 4, 4, 0, FloorKind.Grass, 25);
        Fill(7, 37, 14, 38, 0, FloorKind.Paved);

        Place(PlacementKind.Bonfire, 18, 5);
        Place(PlacementKind.Bonfire, 46, 30);
        Gate(22, 14, "grove_great_gate", 0, 1);
        Place(PlacementKind.Tower, 62, 6);
        Place(PlacementKind.Fountain, 53, 34);
        Place(PlacementKind.Altar, 15, 34);
        Heart(24, 38, "elaphrium_void_heart");
        PlaceMany(PlacementKind.VoidRift, (20, 40), (28, 40), (24, 42));
        Place(PlacementKind.SoulCache, 4, 39);
        Place(PlacementKind.GnawerNest, 6, 36);

        PlaceMany(PlacementKind.Tree, (17, 3), (27, 3), (46, 9), (49, 15), (60, 15), (64, 11), (43, 32), (61, 38), (58, 29), (14, 39), (30, 41), (2, 36), (8, 41));
        PlaceMany(PlacementKind.Bush, (25, 9), (52, 7), (57, 13), (45, 37), (56, 40), (18, 42), (29, 36), (6, 40));
        PlaceMany(PlacementKind.Rock, (19, 8), (51, 16), (62, 33), (12, 36), (33, 34));
        PlaceMany(PlacementKind.Column, (19, 9), (25, 9));
        PlaceMany(PlacementKind.Statue, (47, 34), (59, 34));
        PlaceMany(PlacementKind.Pillar, (49, 28), (57, 28), (19, 33), (29, 33));
        PlaceMany(PlacementKind.BrokenPillar, (44, 12), (61, 18), (25, 44));
        PlaceMany(PlacementKind.RuinWall, (52, 40), (55, 5));
        PlaceMany(PlacementKind.Brazier, (17, 7), (27, 7), (21, 30), (45, 29), (47, 29));
        PlaceMany(PlacementKind.Crate, (63, 9), (63, 10), (41, 34));
        PlaceMany(PlacementKind.Barrel, (48, 13), (60, 36));
        PlaceMany(PlacementKind.Vase, (50, 32), (56, 32), (18, 36));
        PlaceMany(PlacementKind.Pot, (26, 38), (5, 37));

        PlaceMany(PlacementKind.Bogslime, (50, 10), (58, 13), (60, 31), (27, 37));
        PlaceMany(PlacementKind.Hound, (60, 9), (44, 35));
        PlaceMany(PlacementKind.Wasp, (55, 7), (20, 37));
        PlaceMany(PlacementKind.Gnawer, (47, 14), (46, 36), (58, 38));
        PlaceMany(PlacementKind.Rotling, (22, 22), (22, 27));
        Place(PlacementKind.Brute, 18, 39);

        Trigger("region_grove", new Rect2I(0, 0, 79, 49), null, "AURELIAN GROVE", "Elaphrium, within the first walls");
        Trigger("voice_grove", new Rect2I(15, 2, 14, 8),
            "The gate is barred from inside. There will be another way in. There always is.", null, null);
        Trigger("voice_court", new Rect2I(41, 27, 22, 14),
            "Still green. Still breathing. Rest your feet, Hunter, but do not trust it. A wound can bloom.", "FOUNTAIN COURT", "The first city's well");
        Trigger("voice_altar", new Rect2I(13, 31, 18, 12),
            "The Altar of the First. The Elaphrians prayed here before anyone had a word for prayer.", null, null);

        // Screen sections: one per room, and every long road or crossing gets its own.
        region = "Elaphrium";
        Section("Landing", 13, 0, 30, 16);
        Section("East Road", 31, 2, 42, 10);
        Section("Golden Wood", 43, 0, 67, 19);
        Section("Court Road", 48, 20, 60, 26);
        Section("Fountain Court", 37, 27, 67, 45);
        Section("Inner Road", 18, 17, 26, 27);
        Section("Altar of the First", 10, 28, 36, 46);
        Section("Western Thicket", -1, 32, 9, 45);
        origin = Vector2I.Zero;
    }

    // The west Fracture opens on a chain of islands toward a tear that will not open. Graph: Arrival -> narrow bridge
    // (sleeping bat) -> shade islands -> Sealed Fracture plateau (vista) -> one-tile east bridge -> Soul Cache islet
    // -> long causeway back down to the Arrival. The Fracture itself is the late-game lock: four Void Hearts.
    private void BuildAzureanVerge()
    {
        origin = VergeOrigin;
        Blob(37, 36, 4, 3, 0, FloorKind.Grass, 31);
        Fill(26, 34, 33, 35, 0, FloorKind.Paved);
        Blob(23, 31, 4, 4, 0, FloorKind.Grass, 32);
        Fill(15, 23, 21, 27, 0, FloorKind.Paved);
        Blob(12, 20, 5, 4, 0, FloorKind.Grass, 33);
        Fill(11, 11, 13, 16, 0, FloorKind.Paved);
        Blob(14, 7, 7, 4, 0, FloorKind.Grass, 34);
        Fill(21, 6, 31, 6, 0, FloorKind.Paved);
        Blob(33, 7, 3, 3, 0, FloorKind.Grass, 35);
        Fill(33, 9, 34, 33, 0, FloorKind.Paved);

        Place(PlacementKind.SealedFracture, 14, 5);
        Place(PlacementKind.SoulCache, 34, 7);
        PlaceMany(PlacementKind.DeadTree, (9, 18), (16, 22), (20, 29), (9, 6), (19, 9), (39, 34));
        PlaceMany(PlacementKind.Idol, (11, 9), (17, 9));
        PlaceMany(PlacementKind.Rock, (25, 32), (8, 21));

        PlaceMany(PlacementKind.Shadeflit, (23, 31), (12, 20), (16, 8));
        PlaceMany(PlacementKind.GloomBat, (29, 34), (33, 22));

        Trigger("region_verge", new Rect2I(0, 0, 44, 46), null, "AZUREAN VERGE", "A Fracture that will not open");
        Trigger("voice_sealed", new Rect2I(8, 3, 13, 8),
            "Azurean lies beyond. Four hearts still bind this wound. Cut them out, Hunter, and the way opens.", null, null);

        // Screen sections: one per room, and every long road or crossing gets its own.
        region = "Azurean Verge";
        Section("Arrival", 28, 28, 51, 41);
        Section("Shade Islands", 4, 14, 27, 38);
        Section("Sealed Plateau", 4, 0, 23, 13);
        Section("Islet Bridge", 24, 0, 39, 12);
        Section("Long Causeway", 30, 13, 39, 27);
        origin = Vector2I.Zero;
    }

    // Biome follows the continent a tile belongs to; the Reach, its Fracture spans and the Verge stay Verdant.
    private void AssignBiomes()
    {
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
            {
                var tile = new Vector2I(x, y);
                Biome biome = FrostFrame.HasPoint(tile) ? Biome.Snow : BonesFrame.HasPoint(tile) ? Biome.Undead : GroveFrame.HasPoint(tile) ? Biome.BrightForest : Biome.Verdant;
                Grid.SetBiome(x, y, biome);
            }
    }

    private void BuildRegions()
    {
        Regions.Add(new MapRegion(BiomeArt.Name(Biome.Snow), Biome.Snow, FrostFrame));
        Regions.Add(new MapRegion(BiomeArt.Name(Biome.Undead), Biome.Undead, BonesFrame));
        Regions.Add(new MapRegion(BiomeArt.Name(Biome.Verdant), Biome.Verdant, new Rect2I(HollowReachOrigin, new Vector2I(60, 44))));
        Regions.Add(new MapRegion(BiomeArt.Name(Biome.BrightForest), Biome.BrightForest, GroveFrame));
        Regions.Add(new MapRegion("Azurean Verge", Biome.Verdant, VergeFrame));
    }

    private void ReportConnectivity()
    {
        int components = CountWalkableComponents();
        if (components == 1)
            return;
        GD.PushWarning($"[LevelBlueprint] world has {components} disconnected walkable areas");
        var visited = new bool[Grid.Width, Grid.Height];
        for (int y = 0; y < Grid.Height; y++)
        {
            for (int x = 0; x < Grid.Width; x++)
            {
                if (visited[x, y] || !IsWalkable(x, y))
                    continue;
                int size = 0;
                var queue = new Queue<Vector2I>();
                queue.Enqueue(new Vector2I(x, y));
                visited[x, y] = true;
                while (queue.Count > 0)
                {
                    Vector2I tile = queue.Dequeue();
                    size++;
                    foreach (Vector2I step in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
                    {
                        Vector2I next = tile + step;
                        if (IsWalkable(next.X, next.Y) && !visited[next.X, next.Y])
                        {
                            visited[next.X, next.Y] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
                GD.Print($"[LevelBlueprint] area of {size} tiles starting at {x},{y}");
            }
        }
    }

    // Sections must not overlap, and every walkable tile has to sit in one, or the camera would have nothing to frame there.
    private void ReportSections()
    {
        var owner = new int[Grid.Width, Grid.Height];
        for (int i = 0; i < Sections.Count; i++)
        {
            Rect2I area = Sections[i].Area.Intersection(new Rect2I(0, 0, Grid.Width, Grid.Height));
            for (int y = area.Position.Y; y < area.End.Y; y++)
                for (int x = area.Position.X; x < area.End.X; x++)
                {
                    if (owner[x, y] != 0)
                        GD.PushWarning($"[LevelBlueprint] section {Sections[i].Name} overlaps {Sections[owner[x, y] - 1].Name} at {x},{y}");
                    owner[x, y] = i + 1;
                }
        }
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                if (owner[x, y] == 0 && IsWalkable(x, y))
                    GD.PushWarning($"[LevelBlueprint] walkable tile {x},{y} lies in no section");
    }

    // The Void has been eating the islands from the rim inward: crumble ragged bites out of every shoreline
    // it can reach. Nothing that is placed, paved or load-bearing for a path goes.
    private void Erode()
    {
        var protectedTiles = new HashSet<Vector2I>();
        foreach (Placement placement in Placements)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    protectedTiles.Add(placement.Tile + new Vector2I(dx, dy));

        int components = CountWalkableComponents();
        for (int pass = 0; pass < 3; pass++)
        {
            int chance = pass switch { 0 => 46, 1 => 30, _ => 18 };
            var bites = new List<Vector2I>();
            for (int y = 0; y < Grid.Height; y++)
                for (int x = 0; x < Grid.Width; x++)
                    if (IsErodible(x, y, protectedTiles) && PitNeighbours(x, y) > 0 && Art.Hash(x, y, 300 + pass) % 100 < chance)
                        bites.Add(new Vector2I(x, y));
            foreach (Vector2I bite in bites)
                TryCrumble(bite, ref components);
        }
    }

    private void TryCrumble(Vector2I tile, ref int components)
    {
        FloorKind floor = Grid.FloorAt(tile.X, tile.Y);
        Grid.Set(tile.X, tile.Y, WorldGrid.PIT, FloorKind.Stone);
        int after = CountWalkableComponents();
        if (after != components)
            Grid.Set(tile.X, tile.Y, 0, floor);
    }

    private bool IsErodible(int x, int y, HashSet<Vector2I> protectedTiles) =>
        Grid.HeightAt(x, y) == 0 && Grid.FloorAt(x, y) == FloorKind.Grass && !protectedTiles.Contains(new Vector2I(x, y));

    private int PitNeighbours(int x, int y)
    {
        int count = 0;
        if (Grid.IsPit(x - 1, y)) count++;
        if (Grid.IsPit(x + 1, y)) count++;
        if (Grid.IsPit(x, y - 1)) count++;
        if (Grid.IsPit(x, y + 1)) count++;
        return count;
    }

    private int CountWalkableComponents()
    {
        var visited = new bool[Grid.Width, Grid.Height];
        var queue = new Queue<Vector2I>();
        int components = 0;
        for (int y = 0; y < Grid.Height; y++)
        {
            for (int x = 0; x < Grid.Width; x++)
            {
                if (visited[x, y] || !IsWalkable(x, y))
                    continue;
                components++;
                visited[x, y] = true;
                queue.Enqueue(new Vector2I(x, y));
                while (queue.Count > 0)
                {
                    Vector2I tile = queue.Dequeue();
                    foreach (Vector2I step in new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down })
                    {
                        Vector2I next = tile + step;
                        if (IsWalkable(next.X, next.Y) && !visited[next.X, next.Y])
                        {
                            visited[next.X, next.Y] = true;
                            queue.Enqueue(next);
                        }
                    }
                }
            }
        }
        return components;
    }

    // Only back walls (north of walkable ground) and flanking walls survive. A wall south of the ground would rise over it
    // in this projection and hide the player, and distant filler reads better as open Void beneath the ruins.
    private void ReshapeBoundaries()
    {
        var result = new int[Grid.Width, Grid.Height];
        for (int y = 0; y < Grid.Height; y++)
        {
            for (int x = 0; x < Grid.Width; x++)
            {
                int height = Grid.HeightAt(x, y);
                result[x, y] = height;
                if (height != WorldGrid.WALL_HEIGHT)
                    continue;
                bool southOfGround = false;
                for (int distance = 1; distance <= WorldGrid.WALL_HEIGHT && !southOfGround; distance++)
                    southOfGround = IsWalkable(x, y - distance);
                bool backWall = false;
                for (int distance = 1; distance <= 4 && !backWall; distance++)
                    backWall = IsWalkable(x, y + distance);
                bool flankingWall = false;
                for (int dx = -2; dx <= 2 && !flankingWall; dx++)
                    for (int dy = -1; dy <= 3 && !flankingWall; dy++)
                        flankingWall = dx != 0 && IsWalkable(x + dx, y + dy);
                if (southOfGround || !(backWall || flankingWall))
                    result[x, y] = WorldGrid.PIT;
            }
        }
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                if (result[x, y] != Grid.HeightAt(x, y))
                    Grid.Set(x, y, result[x, y], FloorKind.Stone);

        for (int y = 0; y < Grid.Height; y++)
        {
            for (int x = 0; x < Grid.Width; x++)
            {
                if (Grid.HeightAt(x, y) != WorldGrid.WALL_HEIGHT)
                    continue;
                bool horizontalNeighbour = Grid.HeightAt(x - 1, y) == WorldGrid.WALL_HEIGHT || Grid.HeightAt(x + 1, y) == WorldGrid.WALL_HEIGHT;
                bool verticalNeighbour = Grid.HeightAt(x, y - 1) == WorldGrid.WALL_HEIGHT || Grid.HeightAt(x, y + 1) == WorldGrid.WALL_HEIGHT;
                if (!horizontalNeighbour || !verticalNeighbour)
                    Grid.Set(x, y, WorldGrid.PIT, FloorKind.Stone);
            }
        }
    }

    private bool IsWalkable(int x, int y)
    {
        if (!Grid.InBounds(x, y))
            return false;
        int height = Grid.HeightAt(x, y);
        return height >= 0 && height < WorldGrid.WALL_HEIGHT;
    }

    private void Fill(int x0, int y0, int x1, int y1, int height, FloorKind floor)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Grid.Set(x + origin.X, y + origin.Y, height, floor);
    }

    private void Pit(int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Grid.Set(x + origin.X, y + origin.Y, WorldGrid.PIT, FloorKind.Stone);
    }

    // An island with a smoothly wobbling shoreline: the radius varies with the angle, so the rim looks eroded, not elliptical.
    private void Blob(int cx, int cy, int rx, int ry, int height, FloorKind floor, int seed)
    {
        float phaseA = seed * 1.7f;
        float phaseB = seed * 2.9f;
        for (int y = cy - ry - 3; y <= cy + ry + 3; y++)
        {
            for (int x = cx - rx - 3; x <= cx + rx + 3; x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                float angle = Mathf.Atan2(dy, dx);
                float radius = 1f + 0.14f * Mathf.Sin(3f * angle + phaseA) + 0.08f * Mathf.Sin(5f * angle + phaseB);
                if (dx * dx + dy * dy <= radius * radius)
                    Grid.Set(x + origin.X, y + origin.Y, height, floor);
            }
        }
    }

    private void Place(PlacementKind kind, int x, int y, string id = null) => Placements.Add(new Placement(kind, new Vector2I(x, y) + origin, id));

    // Inclusive tile corners in the current frame, like Fill.
    private void Section(string name, int x0, int y0, int x1, int y1) =>
        Sections.Add(new SectionPlan(region, name, new Rect2I(x0 + origin.X, y0 + origin.Y, x1 - x0 + 1, y1 - y0 + 1)));

    private void Heart(int x, int y, string id) => Place(PlacementKind.VoidHeart, x, y, id);

    private void Gate(int x, int y, string id, int sideX, int sideY) =>
        Placements.Add(new Placement(PlacementKind.ShortcutGate, new Vector2I(x, y) + origin, id, new Vector2I(sideX, sideY)));

    private void Trigger(string id, Rect2I area, string line, string regionTitle, string regionSubtitle) =>
        Triggers.Add(new NarrativeTrigger(id, new Rect2I(area.Position + origin, area.Size), line, regionTitle, regionSubtitle));

    private void PlaceMany(PlacementKind kind, params (int x, int y)[] tiles)
    {
        foreach ((int x, int y) in tiles)
            Place(kind, x, y);
    }
}
