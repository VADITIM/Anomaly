using Godot;

namespace Anomaly;

public partial class SoulGlyph : Control
{
    private float time;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f);
        DrawCircle(Vector2.Zero, 6f + pulse, new Color(0.35f, 0.7f, 1f, 0.2f));
        DrawColoredPolygon(new[] { new Vector2(0, -6), new Vector2(3.5f, 0), new Vector2(0, 6), new Vector2(-3.5f, 0) }, new Color(0.7f, 0.95f, 1f));
        DrawColoredPolygon(new[] { new Vector2(0, -6), new Vector2(3.5f, 0), Vector2.Zero }, Colors.White);
    }
}
