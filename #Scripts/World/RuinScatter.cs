using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Procedural ruin dressing between the authored placements: rubble and dead scrub collect along the rims
// the Void is eating, toppled relics lie half-sunk in open ground, and grit trickles off the cliffs.
public static class RuinScatter
{
    public static readonly Color StoneTint = new(0.8f, 0.82f, 0.8f);
    public static readonly Color DeadWoodTint = new(0.66f, 0.6f, 0.66f);

    public static void Populate(GameWorld world)
    {
        WorldGrid grid = world.Grid;
        HashSet<Vector2I> occupied = OccupiedTiles(world.Blueprint);
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                int height = grid.HeightAt(x, y);
                if (height < 0 || height >= WorldGrid.WALL_HEIGHT || occupied.Contains(new Vector2I(x, y)) || grid.FloorAt(x, y) == FloorKind.Paved)
                    continue;
                int hash = Art.Hash(x, y, 500);
                int roll = hash % 1000;
                bool rim = DistanceToPit(grid, x, y, 2) <= 2;
                Vector2 jitter = new((hash / 7) % 17 - 8, (hash / 31) % 13 - 6);
                Vector2 position = WorldGrid.TileCenter(x, y) + jitter;
                float elevation = height * WorldGrid.LEVEL_HEIGHT;

                Biome biome = grid.ArtBiomeAt(x, y);
                if (biome != Biome.Verdant)
                {
                    ScatterBiome(world, biome, position, elevation, hash, roll, rim);
                    continue;
                }

                if (roll < (rim ? 110 : 30))
                    world.AddRuinDecoration(Art.Rubble[(hash / 11) % Art.Rubble.Length], position, elevation, StoneTint, false, 0f);
                else if (roll < (rim ? 170 : 52))
                    world.AddRuinDecoration(Art.DeadShrubs[(hash / 13) % Art.DeadShrubs.Length], position, elevation, DeadWoodTint, false, 0f);
                else if (roll < (rim ? 182 : 62))
                    world.AddRuinDecoration(Art.Relics[(hash / 17) % Art.Relics.Length], position, elevation, StoneTint, false, 9f);
            }
        }
    }

    // Imported-pack dressing: litter everywhere, the odd solid boulder or grave marker, denser along the rims.
    private static void ScatterBiome(GameWorld world, Biome biome, Vector2 position, float elevation, int hash, int roll, bool rim)
    {
        Color tint = BiomeArt.ObjectTint(biome);
        if (roll < (rim ? 90 : 38))
            world.AddRuinDecoration(BiomeArt.Pick(BiomeArt.Scatter(biome), hash / 11), position, elevation, tint, false, 0f);
        else if (roll < (rim ? 120 : 56))
            world.AddRuinDecoration(BiomeArt.Bush(biome, hash / 13), position, elevation, tint, false, 0f);
        else if (roll < (rim ? 134 : 66))
            world.AddRuinDecoration(BiomeArt.Rock(biome, hash / 17), position, elevation, tint, false, 11f);
        else if (biome == Biome.Undead && roll < 74)
            world.AddRuinDecoration(BiomeArt.Pick(BiomeArt.BoneGraves, hash / 19), position, elevation, tint, false, 6f);
    }

    public static int DistanceToPit(WorldGrid grid, int x, int y, int maximum)
    {
        for (int radius = 1; radius <= maximum; radius++)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (grid.IsPit(x + dx, y + dy))
                        return radius;
        return int.MaxValue;
    }

    private static HashSet<Vector2I> OccupiedTiles(LevelBlueprint blueprint)
    {
        var occupied = new HashSet<Vector2I>();
        foreach (Placement placement in blueprint.Placements)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    occupied.Add(placement.Tile + new Vector2I(dx, dy));
        return occupied;
    }

    // Grit and pebbles break off south-facing rims and tumble down the cliff into the Void.
    public static void AttachCrumble(GameWorld world, Node parent)
    {
        WorldGrid grid = world.Grid;
        var texture = new AtlasTexture { Atlas = Art.Texture(Art.RUINS), Region = Art.PebbleA.Region };
        var fade = new Gradient();
        fade.SetColor(0, new Color(0.62f, 0.6f, 0.68f, 1f));
        fade.SetColor(1, new Color(0.2f, 0.05f, 0.3f, 0f));
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.HeightAt(x, y) != 0 || !grid.IsPit(x, y + 1) || Art.Hash(x, y, 610) % 100 >= 12)
                    continue;
                parent.AddChild(new CpuParticles2D
                {
                    Position = new Vector2((x + 0.5f) * WorldGrid.TILE_SIZE, (y + 1) * WorldGrid.TILE_SIZE),
                    Amount = 3,
                    Lifetime = 2.6f,
                    Preprocess = 2.6f,
                    Randomness = 1f,
                    Explosiveness = 0f,
                    Texture = texture,
                    EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                    EmissionRectExtents = new Vector2(13, 1),
                    Direction = Vector2.Down,
                    Spread = 12f,
                    InitialVelocityMin = 4f,
                    InitialVelocityMax = 14f,
                    Gravity = new Vector2(0, 38f),
                    ScaleAmountMin = 0.35f,
                    ScaleAmountMax = 0.8f,
                    AngularVelocityMin = -90f,
                    AngularVelocityMax = 90f,
                    ColorRamp = fade,
                    ZIndex = -49,
                });
            }
        }
    }
}
