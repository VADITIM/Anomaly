using Godot;

namespace Anomaly;

// Stand-in amulet art until the real sprites exist: a thin cord hanging into a faceted gem in the amulet's color,
// scaled to the control. Empty draws only a faint socket.
[Tool]
public partial class AmuletIcon : Control
{
    [Export] public Color Gem = new(0.55f, 0.82f, 0.52f);
    [Export] public bool Empty;

    public AmuletIcon() => MouseFilter = MouseFilterEnum.Ignore;

    public void Display(AmuletInfo amulet)
    {
        Empty = amulet == null;
        if (amulet != null)
            Gem = amulet.Color;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float unit = Mathf.Max(1f, Mathf.Floor(Mathf.Min(Size.X, Size.Y) / 16f));
        Vector2 c = (Size * 0.5f).Floor() + new Vector2(0f, unit * 2f);
        float r = unit * 5f;
        if (Empty)
        {
            DrawPolyline(Diamond(c, r), new Color(UiFactory.DimText, 0.35f), 1f);
            return;
        }
        Color cord = UiFactory.FrameLine;
        DrawLine(c + new Vector2(-r, -r * 1.6f), c + new Vector2(0f, -r), cord, 1f);
        DrawLine(c + new Vector2(r, -r * 1.6f), c + new Vector2(0f, -r), cord, 1f);
        Vector2[] gem = Diamond(c, r);
        DrawColoredPolygon(new[] { gem[0], gem[1], gem[2], gem[3] }, Gem.Darkened(0.2f));
        // The upper left facet catches the light.
        DrawColoredPolygon(new[] { gem[0], c, gem[3] }, Gem.Lightened(0.25f));
        DrawColoredPolygon(new[] { gem[1], gem[2], c }, Gem.Darkened(0.45f));
        DrawPolyline(Diamond(c, r + 1f), UiFactory.Outline, 1f);
        DrawRect(new Rect2(c + new Vector2(-unit * 2f, -unit * 2f), new Vector2(unit, unit)), new Color(1f, 1f, 1f, 0.8f));
    }

    private static Vector2[] Diamond(Vector2 c, float r) =>
        new[] { c + new Vector2(0f, -r), c + new Vector2(r, 0f), c + new Vector2(0f, r), c + new Vector2(-r, 0f), c + new Vector2(0f, -r) };
}
