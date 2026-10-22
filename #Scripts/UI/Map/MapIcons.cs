using Godot;

namespace Anomaly;

// Marker glyphs shared by the minimap and the world map, drawn in the map's own pixel space.
public static class MapIcons
{
    private static readonly Color Gold = new(1f, 0.82f, 0.35f);
    private static readonly Color Magenta = new(0.95f, 0.3f, 0.85f);
    private static readonly Color Outline = new(0.03f, 0.01f, 0.06f);
    private static readonly Color Kio = new(0.75f, 1f, 0.95f);
    private static readonly Color Violet = new(0.62f, 0.55f, 1f);

    public static void DrawMarker(CanvasItem canvas, MapMarkerKind kind, Vector2 center, float size, float pulse)
    {
        switch (kind)
        {
            case MapMarkerKind.RendAnvil:
                DrawDiamond(canvas, center, size, Gold);
                break;
            case MapMarkerKind.VoidHeart:
                DrawDiamond(canvas, center, size * (1f + 0.25f * pulse), Magenta);
                break;
            case MapMarkerKind.Bonfire:
                DrawDiamond(canvas, center, size * 0.75f, Violet);
                break;
            case MapMarkerKind.Fracture:
                canvas.DrawArc(center, size * 0.9f, 0f, Mathf.Tau, 12, Outline, 3f);
                canvas.DrawArc(center, size * 0.9f, 0f, Mathf.Tau, 12, Magenta.Lerp(Colors.White, 0.3f * pulse), 1.5f);
                break;
        }
    }

    public static void DrawDiamond(CanvasItem canvas, Vector2 center, float size, Color color)
    {
        canvas.DrawColoredPolygon(Diamond(center, size + 1f), Outline);
        canvas.DrawColoredPolygon(Diamond(center, size), color);
    }

    public static void DrawPlayer(CanvasItem canvas, Vector2 center, Vector2 facing, float size)
    {
        Vector2 forward = facing.LengthSquared() < 0.01f ? Vector2.Down : facing.Normalized();
        Vector2 side = new(-forward.Y, forward.X);
        Vector2[] outline = Arrow(center, forward, side, size + 1.2f);
        Vector2[] fill = Arrow(center, forward, side, size);
        canvas.DrawColoredPolygon(outline, Outline);
        canvas.DrawColoredPolygon(fill, Kio);
    }

    private static Vector2[] Diamond(Vector2 center, float size) => new[]
    {
        center + new Vector2(0, -size), center + new Vector2(size, 0), center + new Vector2(0, size), center + new Vector2(-size, 0),
    };

    private static Vector2[] Arrow(Vector2 center, Vector2 forward, Vector2 side, float size) => new[]
    {
        center + forward * size * 1.3f, center - forward * size * 0.9f + side * size * 0.9f, center - forward * size * 0.35f, center - forward * size * 0.9f - side * size * 0.9f,
    };
}
