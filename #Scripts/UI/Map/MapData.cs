using System.Collections.Generic;
using Godot;

namespace Anomaly;

public readonly record struct MapMarker(Vector2I Tile, MapMarkerKind Kind, string Id = null);

public enum MapMarkerKind
{
    RendAnvil,
    VoidHeart,
    Bonfire,
    Fracture,
}

// One pixel per world tile, shared by the minimap and the world map. Tiles stay dark until Kio has walked near them,
// so the map fills in as the slice is explored.
public sealed class MapData
{
    private const int REVEAL_RADIUS = 13;
    private static readonly Color VoidColor = new(0.035f, 0.015f, 0.075f);
    private static readonly Color WallColor = new(0.07f, 0.06f, 0.1f);
    private static readonly Color UnknownColor = new(0.1f, 0.085f, 0.15f);

    public LevelBlueprint Blueprint { get; }
    public ImageTexture Texture { get; }
    public List<MapMarker> Markers { get; } = new();
    public int Width => Blueprint.Grid.Width;
    public int Height => Blueprint.Grid.Height;

    private readonly WorldGrid grid;
    private readonly bool[,] explored;
    private readonly Image image;
    private bool dirty;

    public MapData(LevelBlueprint blueprint)
    {
        Blueprint = blueprint;
        grid = blueprint.Grid;
        explored = new bool[grid.Width, grid.Height];
        image = Image.CreateEmpty(grid.Width, grid.Height, false, Image.Format.Rgba8);
        foreach (Placement placement in blueprint.Placements)
        {
            if (placement.Kind == PlacementKind.RendAnvil)
                Markers.Add(new MapMarker(placement.Tile, MapMarkerKind.RendAnvil));
            else if (placement.Kind == PlacementKind.VoidHeart && !SaveManager.World.DestroyedVoidHearts.Contains(placement.Id))
                Markers.Add(new MapMarker(placement.Tile, MapMarkerKind.VoidHeart, placement.Id));
            else if (placement.Kind == PlacementKind.Bonfire)
                Markers.Add(new MapMarker(placement.Tile, MapMarkerKind.Bonfire));
            else if (placement.Kind is PlacementKind.Fracture or PlacementKind.SealedFracture)
                Markers.Add(new MapMarker(placement.Tile, MapMarkerKind.Fracture));
        }
        Repaint();
        Texture = ImageTexture.CreateFromImage(image);
    }

    public void RemoveHeartMarker(string id) => Markers.RemoveAll(marker => marker.Kind == MapMarkerKind.VoidHeart && marker.Id == id);

    public bool IsExplored(int x, int y) => grid.InBounds(x, y) && explored[x, y];

    public bool RegionExplored(Rect2I area)
    {
        for (int y = area.Position.Y; y < area.End.Y; y++)
            for (int x = area.Position.X; x < area.End.X; x++)
                if (IsExplored(x, y) && !grid.IsPit(x, y))
                    return true;
        return false;
    }

    public void Reveal(Vector2I center, int radius = REVEAL_RADIUS)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = center.X + dx;
                int y = center.Y + dy;
                if (dx * dx + dy * dy > radius * radius || !grid.InBounds(x, y) || explored[x, y])
                    continue;
                explored[x, y] = true;
                image.SetPixel(x, y, PixelColor(x, y));
                dirty = true;
            }
        }
    }

    public void RevealAll()
    {
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                explored[x, y] = true;
        Repaint();
        dirty = true;
    }

    public void Flush()
    {
        if (!dirty)
            return;
        dirty = false;
        Texture.Update(image);
    }

    private void Repaint()
    {
        for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                image.SetPixel(x, y, PixelColor(x, y));
    }

    private Color PixelColor(int x, int y)
    {
        int height = grid.HeightAt(x, y);
        if (height == WorldGrid.PIT)
            return VoidColor;
        if (height >= WorldGrid.WALL_HEIGHT)
            return WallColor;
        if (!explored[x, y])
            return UnknownColor;

        Color color = BiomeArt.MapColor(grid.BiomeAt(x, y));
        if (grid.FloorAt(x, y) == FloorKind.Paved)
            color = color.Lerp(new Color(0.78f, 0.74f, 0.66f), 0.55f);
        color = color.Lightened(height * 0.16f);
        if (BordersPit(x, y))
            color = color.Darkened(0.38f);
        return color;
    }

    private bool BordersPit(int x, int y) => grid.IsPit(x - 1, y) || grid.IsPit(x + 1, y) || grid.IsPit(x, y - 1) || grid.IsPit(x, y + 1);
}
