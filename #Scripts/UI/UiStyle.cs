using Godot;

namespace Anomaly;

// Anomaly's drawing kit: square ink plates, a hard black rim, a tarnished inner line, gilt corner brackets and Rend-red
// diamonds, the same four-point cut as the HUD stars. Everything is drawn in whole design pixels; no rounded corners.
public static class UiDraw
{
    public static void Rect(Rid item, Rect2 rect, Color color) => RenderingServer.CanvasItemAddRect(item, rect, color);

    public static void Border(Rid item, Rect2 rect, Color color)
    {
        Rect(item, new Rect2(rect.Position, new Vector2(rect.Size.X, 1f)), color);
        Rect(item, new Rect2(rect.Position.X, rect.End.Y - 1f, rect.Size.X, 1f), color);
        Rect(item, new Rect2(rect.Position.X, rect.Position.Y + 1f, 1f, rect.Size.Y - 2f), color);
        Rect(item, new Rect2(rect.End.X - 1f, rect.Position.Y + 1f, 1f, rect.Size.Y - 2f), color);
    }

    public static void Gradient(Rid item, Rect2 rect, Color from, Color to, bool vertical = false)
    {
        Vector2[] points = { rect.Position, new(rect.End.X, rect.Position.Y), rect.End, new(rect.Position.X, rect.End.Y) };
        Color[] colors = vertical ? new[] { from, from, to, to } : new[] { from, to, to, from };
        RenderingServer.CanvasItemAddPolygon(item, points, colors);
    }

    public static void Diamond(Rid item, Vector2 center, float radius, Color color, bool outlined = true)
    {
        if (outlined)
            RenderingServer.CanvasItemAddPolygon(item, DiamondPoints(center, radius + 1f), new[] { UiFactory.Outline });
        RenderingServer.CanvasItemAddPolygon(item, DiamondPoints(center, radius), new[] { color });
    }

    private static Vector2[] DiamondPoints(Vector2 c, float r) => new[] { c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0) };
}

// Panel plate. Fill may be transparent to lay the frame over content that draws its own backdrop (the maps).
public partial class FrameStyle : StyleBox
{
    private const float BRACKET = 6f;

    public Color Fill = UiFactory.Ink;
    public Color Line = UiFactory.FrameLine;
    public bool Ornaments = true;

    public FrameStyle() => SetPadding(10f, 8f);

    public FrameStyle SetPadding(float horizontal, float vertical)
    {
        ContentMarginLeft = ContentMarginRight = horizontal;
        ContentMarginTop = ContentMarginBottom = vertical;
        return this;
    }

    public override void _Draw(Rid item, Rect2 rect)
    {
        if (Fill.A > 0f)
            UiDraw.Gradient(item, rect.Grow(-1f), Fill.Lightened(0.06f), Fill, true);
        UiDraw.Border(item, rect, UiFactory.Outline);
        Rect2 inner = rect.Grow(-2f);
        UiDraw.Border(item, inner, Line);
        if (!Ornaments)
            return;
        Vector2[] corners = { inner.Position, new(inner.End.X - 1f, inner.Position.Y), new(inner.Position.X, inner.End.Y - 1f), inner.End - Vector2.One };
        foreach (Vector2 corner in corners)
        {
            float dx = corner.X < inner.GetCenter().X ? 1f : -1f;
            float dy = corner.Y < inner.GetCenter().Y ? 1f : -1f;
            UiDraw.Rect(item, new Rect2(dx > 0 ? corner.X : corner.X - BRACKET + 1f, corner.Y, BRACKET, 1f).Abs(), UiFactory.Accent);
            UiDraw.Rect(item, new Rect2(corner.X, dy > 0 ? corner.Y : corner.Y - BRACKET + 1f, 1f, BRACKET).Abs(), UiFactory.Accent);
            UiDraw.Diamond(item, corner + new Vector2(0.5f, 0.5f), 1.5f, UiFactory.Rend);
        }
    }
}

// Selection mark: a Rend wash bleeding out to the right, a hard edge on the left and a gilt diamond before the text.
public partial class MarkStyle : StyleBox
{
    public const float TEXT_INSET = 12f;

    public float Strength = 1f;

    public MarkStyle()
    {
        ContentMarginLeft = TEXT_INSET;
        ContentMarginRight = 6f;
    }

    public override void _Draw(Rid item, Rect2 rect)
    {
        Color rend = UiFactory.Rend;
        UiDraw.Gradient(item, rect, new Color(rend, 0.32f * Strength), new Color(rend, 0f));
        UiDraw.Rect(item, new Rect2(rect.Position, new Vector2(1f, rect.Size.Y)), new Color(rend, Mathf.Min(1f, 0.9f * Strength)));
        UiDraw.Diamond(item, new Vector2(rect.Position.X + 6f, Mathf.Floor(rect.GetCenter().Y) + 0.5f), 2f, UiFactory.Accent);
    }
}

// Backing strip for floating text: solid in the middle, fading to nothing at both ends, edged top and bottom.
public partial class Band : Control
{
    public Color Fill = new(UiFactory.Ink, 0.7f);
    public Color Edge = new(UiFactory.FrameLine, 0f);
    public float Fade = 0.3f;

    public Band() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        Rid item = GetCanvasItem();
        float fade = Mathf.Floor(Size.X * Fade);
        Strip(item, 0f, Size.Y, Fill, fade);
        if (Edge.A <= 0f)
            return;
        Strip(item, 0f, 1f, Edge, fade);
        Strip(item, Size.Y - 1f, 1f, Edge, fade);
    }

    private void Strip(Rid item, float y, float height, Color color, float fade)
    {
        var clear = new Color(color, 0f);
        UiDraw.Gradient(item, new Rect2(0, y, fade, height), clear, color);
        UiDraw.Rect(item, new Rect2(fade, y, Size.X - fade * 2f, height), color);
        UiDraw.Gradient(item, new Rect2(Size.X - fade, y, fade, height), color, clear);
    }
}
