using System.Collections.Generic;
using Godot;

namespace Anomaly;

public enum FloorKind
{
    Grass,
    Paved,
    Stone,
}

public enum Biome
{
    Verdant,
    BrightForest,
    Undead,
    Snow,
    // Ground the Void has taken. Each tile still remembers the land it was torn from (see WorldGrid.EchoAt).
    Void,
}

public struct Obstacle
{
    public Vector2 Position;
    public float Radius;
    public float BaseZ;
    public float Height;
}

// The world is a 2D ground plane with an integer heightmap. Every tile owns a surface Z
// (height level * LEVEL_HEIGHT); entities carry their own Z and are blocked only by surfaces
// they cannot step onto, which is what lets one top-down plane express ledges, cliffs and pits.
public sealed class WorldGrid
{
    public const int TILE_SIZE = 32;
    public const float LEVEL_HEIGHT = 32f;
    public const int PIT = -100;
    public const int WALL_HEIGHT = 3;
    public const float STEP_HEIGHT = 7f;
    // Support and blocking probe the same footprint: if support used a smaller one, stepping off a ledge would drop
    // an entity while its blocking corners still overlap the higher tile, pinning it against that wall.
    private const float FOOTPRINT = 0.85f;

    public readonly int Width;
    public readonly int Height;

    private readonly int[,] heights;
    private readonly FloorKind[,] floors;
    private readonly Biome[,] biomes;
    private readonly Biome[,] echoes;
    private readonly List<Obstacle> obstacles = new();

    public WorldGrid(int width, int height)
    {
        Width = width;
        Height = height;
        heights = new int[width, height];
        floors = new FloorKind[width, height];
        biomes = new Biome[width, height];
        echoes = new Biome[width, height];
    }

    public Rect2 Bounds => new(0, 0, Width * TILE_SIZE, Height * TILE_SIZE);

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int HeightAt(int x, int y) => InBounds(x, y) ? heights[x, y] : PIT;

    public FloorKind FloorAt(int x, int y) => InBounds(x, y) ? floors[x, y] : FloorKind.Stone;

    public Biome BiomeAt(int x, int y) => InBounds(x, y) ? biomes[x, y] : Biome.Verdant;

    public Biome BiomeAt(Vector2 position)
    {
        Vector2I tile = ToTile(position);
        return BiomeAt(tile.X, tile.Y);
    }

    public void SetBiome(int x, int y, Biome biome)
    {
        if (InBounds(x, y))
            biomes[x, y] = biome;
    }

    // The land a Void tile was torn from; its scenery and its unbroken patches are dressed in that land's art.
    public Biome EchoAt(int x, int y) => InBounds(x, y) ? echoes[x, y] : Biome.Verdant;

    public void SetEcho(int x, int y, Biome echo)
    {
        if (InBounds(x, y))
            echoes[x, y] = echo;
    }

    // The biome whose art dresses a tile: its own, or for Void ground the land it remembers.
    public Biome ArtBiomeAt(int x, int y)
    {
        Biome biome = BiomeAt(x, y);
        return biome == Biome.Void ? EchoAt(x, y) : biome;
    }

    public bool IsPit(int x, int y) => HeightAt(x, y) == PIT;

    public void Set(int x, int y, int height, FloorKind floor)
    {
        if (!InBounds(x, y))
            return;
        heights[x, y] = height;
        floors[x, y] = floor;
    }

    public static Vector2I ToTile(Vector2 position) =>
        new(Mathf.FloorToInt(position.X / TILE_SIZE), Mathf.FloorToInt(position.Y / TILE_SIZE));

    public static Vector2 TileCenter(int x, int y) => new((x + 0.5f) * TILE_SIZE, (y + 0.5f) * TILE_SIZE);

    public float SurfaceZAt(Vector2 position)
    {
        Vector2I tile = ToTile(position);
        int height = HeightAt(tile.X, tile.Y);
        return height == PIT ? float.NegativeInfinity : height * LEVEL_HEIGHT;
    }

    public bool IsPitAt(Vector2 position)
    {
        Vector2I tile = ToTile(position);
        return IsPit(tile.X, tile.Y);
    }

    // Highest surface under the footprint the entity can stand on from its current Z.
    public float SupportZ(Vector2 position, float radius, float z)
    {
        float reach = radius * FOOTPRINT;
        float best = float.NegativeInfinity;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 probe = position + new Vector2(corner % 2 == 0 ? -reach : reach, corner < 2 ? -reach : reach);
            float surface = SurfaceZAt(probe);
            if (surface <= z + STEP_HEIGHT && surface > best)
                best = surface;
        }
        return best;
    }

    public bool Blocks(Vector2 position, float radius, float z)
    {
        float reach = radius * FOOTPRINT;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 probe = position + new Vector2(corner % 2 == 0 ? -reach : reach, corner < 2 ? -reach : reach);
            if (SurfaceZAt(probe) > z + STEP_HEIGHT)
                return true;
        }
        return BlockedByObstacle(position, radius, z);
    }

    public void AddObstacle(Vector2 position, float radius, float baseZ, float height)
    {
        var obstacle = new Obstacle { Position = position, Radius = radius, BaseZ = baseZ, Height = height };
        obstacles.Add(obstacle);
        Index(obstacle);
    }

    public void RemoveObstaclesNear(Vector2 position, float range)
    {
        obstacles.RemoveAll(obstacle => obstacle.Position.DistanceTo(position) <= range);
        obstacleCells.Clear();
        foreach (Obstacle obstacle in obstacles)
            Index(obstacle);
    }

    // Obstacles are bucketed by the cells their footprint touches, so a movement probe only tests its own neighbourhood.
    private const int OBSTACLE_CELL = 64;
    private const float MAX_PROBE_RADIUS = 32f;
    private readonly Dictionary<Vector2I, List<Obstacle>> obstacleCells = new();
    private static readonly List<Obstacle> NoObstacles = new();

    private static Vector2I CellOf(Vector2 position) =>
        new(Mathf.FloorToInt(position.X / OBSTACLE_CELL), Mathf.FloorToInt(position.Y / OBSTACLE_CELL));

    private void Index(Obstacle obstacle)
    {
        float reach = obstacle.Radius + MAX_PROBE_RADIUS;
        Vector2I from = CellOf(obstacle.Position - new Vector2(reach, reach));
        Vector2I to = CellOf(obstacle.Position + new Vector2(reach, reach));
        for (int y = from.Y; y <= to.Y; y++)
            for (int x = from.X; x <= to.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (!obstacleCells.TryGetValue(cell, out List<Obstacle> list))
                    obstacleCells[cell] = list = new List<Obstacle>();
                list.Add(obstacle);
            }
    }

    public bool BlockedByObstacle(Vector2 position, float radius, float z)
    {
        // Each obstacle is indexed into every cell within its radius plus the widest probe, so one cell holds all candidates.
        List<Obstacle> nearby = radius <= MAX_PROBE_RADIUS
            ? obstacleCells.GetValueOrDefault(CellOf(position), NoObstacles)
            : obstacles;
        foreach (Obstacle obstacle in nearby)
        {
            if (z >= obstacle.BaseZ + obstacle.Height - STEP_HEIGHT || z + 24f < obstacle.BaseZ)
                continue;
            float combined = obstacle.Radius + radius;
            if (position.DistanceSquaredTo(obstacle.Position) < combined * combined)
                return true;
        }
        return false;
    }

    public bool HasLineOfSight(Vector2 from, Vector2 to, float z)
    {
        float distance = from.DistanceTo(to);
        int steps = Mathf.CeilToInt(distance / 8f);
        for (int i = 1; i < steps; i++)
        {
            Vector2 sample = from.Lerp(to, i / (float)steps);
            if (SurfaceZAt(sample) > z + LEVEL_HEIGHT)
                return false;
        }
        return true;
    }
}
