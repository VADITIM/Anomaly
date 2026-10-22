using Godot;

namespace Anomaly;

// Ground-level ruin dressing: sunken paving fragments, grit, cracked flagstones, and the Void creeping in
// over every rim that borders it.
public static class RuinPainter
{
    private const int T = WorldGrid.TILE_SIZE;

    private static readonly Color SlabTint = new(0.86f, 0.9f, 0.88f);
    private static readonly Color PebbleTint = new(0.66f, 0.66f, 0.7f);
    private static readonly Color CrackColor = new(0.1f, 0.07f, 0.14f, 0.7f);
    private static readonly Color CreepColor = new(0.06f, 0.0f, 0.12f, 0.62f);
    private static readonly Color VeinColor = new(0.62f, 0.16f, 0.66f, 0.9f);
    private static readonly Color VeinShadow = new(0.08f, 0.0f, 0.1f, 0.8f);

    private static readonly Vector2I[] Sides = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

    public static void DrawFloorDecals(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 origin)
    {
        int hash = Art.Hash(x, y, 211);
        Texture2D ruins = Art.Texture(Art.RUINS);
        FloorKind floor = grid.FloorAt(x, y);

        if (floor == FloorKind.Paved)
        {
            if (hash % 100 < 26)
                DrawCrack(canvas, origin, hash);
            return;
        }

        int slabChance = NearPaved(grid, x, y) ? 14 : 4;
        if (hash % 100 < slabChance)
        {
            SpriteRegion slab = Art.Slabs[(hash / 7) % Art.Slabs.Length];
            Vector2 center = origin + new Vector2(16 + (hash / 13) % 10 - 5, 16 + (hash / 41) % 10 - 5);
            canvas.DrawTextureRectRegion(ruins, new Rect2(center - slab.Origin, slab.Region.Size), slab.Region, SlabTint.Darkened(((hash / 3) % 4) * 0.06f));
        }
        if ((hash / 101) % 100 < 14)
        {
            SpriteRegion pebble = Art.Pebbles[(hash / 17) % Art.Pebbles.Length];
            Vector2 center = origin + new Vector2(6 + (hash / 23) % 20, 6 + (hash / 59) % 20);
            canvas.DrawTextureRectRegion(ruins, new Rect2(center - pebble.Origin, pebble.Region.Size), pebble.Region, PebbleTint);
        }
    }

    // A tile that borders the Void darkens toward that rim, and corrupted veins crawl in from the edge.
    public static void DrawVoidCreep(CanvasItem canvas, WorldGrid grid, int x, int y, Vector2 origin)
    {
        foreach (Vector2I side in Sides)
        {
            if (!grid.IsPit(x + side.X, y + side.Y))
                continue;
            DrawRimShade(canvas, origin, side);
            int hash = Art.Hash(x, y, 400 + side.X * 3 + side.Y * 7);
            int veins = hash % 3;
            for (int vein = 0; vein < veins; vein++)
                DrawVein(canvas, origin, side, Art.Hash(x, y, 420 + vein * 11 + side.X + side.Y * 5));
        }
    }

    private static void DrawRimShade(CanvasItem canvas, Vector2 origin, Vector2I side)
    {
        const float depth = 13f;
        Color clear = new(CreepColor.R, CreepColor.G, CreepColor.B, 0f);
        Vector2 a, b, c, d;
        switch (side)
        {
            case { Y: -1 }:
                a = origin; b = origin + new Vector2(T, 0); c = b + new Vector2(0, depth); d = a + new Vector2(0, depth);
                break;
            case { Y: 1 }:
                a = origin + new Vector2(0, T); b = a + new Vector2(T, 0); c = b - new Vector2(0, depth); d = a - new Vector2(0, depth);
                break;
            case { X: -1 }:
                a = origin + new Vector2(0, T); b = origin; c = b + new Vector2(depth, 0); d = a + new Vector2(depth, 0);
                break;
            default:
                a = origin + new Vector2(T, 0); b = origin + new Vector2(T, T); c = b - new Vector2(depth, 0); d = a - new Vector2(depth, 0);
                break;
        }
        canvas.DrawPolygon(new[] { a, b, c, d }, new[] { CreepColor, CreepColor, clear, clear });
    }

    private static void DrawVein(CanvasItem canvas, Vector2 origin, Vector2I side, int hash)
    {
        Vector2 inward = -new Vector2(side.X, side.Y);
        Vector2 along = new(Mathf.Abs(side.Y), Mathf.Abs(side.X));
        Vector2 edgeCenter = origin + new Vector2(16, 16) + new Vector2(side.X, side.Y) * 16f;
        Vector2 point = (edgeCenter + along * ((hash % 26) - 13)).Floor();
        int segments = 2 + (hash / 7) % 3;
        for (int segment = 0; segment < segments; segment++)
        {
            int step = Art.Hash(hash, segment, 9);
            Vector2 next = point + inward * (3 + step % 4) + along * ((step / 5) % 5 - 2);
            canvas.DrawLine(point + new Vector2(0, 1), next + new Vector2(0, 1), VeinShadow, 1f);
            canvas.DrawLine(point, next, VeinColor.Darkened(segment * 0.18f), 1f);
            point = next;
        }
    }

    private static void DrawCrack(CanvasItem canvas, Vector2 origin, int hash)
    {
        Vector2 point = origin + new Vector2(4 + (hash / 7) % 20, 4 + (hash / 19) % 20);
        for (int segment = 0; segment < 3; segment++)
        {
            int step = Art.Hash(hash, segment, 15);
            Vector2 next = point + new Vector2(step % 9 - 4, (step / 9) % 7 - 1);
            canvas.DrawLine(point, next, CrackColor, 1f);
            point = next;
        }
    }

    private static bool NearPaved(WorldGrid grid, int x, int y)
    {
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                if (grid.FloorAt(x + dx, y + dy) == FloorKind.Paved && grid.HeightAt(x + dx, y + dy) == 0)
                    return true;
        return false;
    }
}
