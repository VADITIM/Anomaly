using Godot;

namespace Anomaly;

public partial class ImpactStar : Node2D
{
    public float Size = 14f;
    public float Duration = 0.12f;
    public Color Color = Colors.White;
    private float time;

    public override void _Ready()
    {
        Rotation = (float)GD.RandRange(-0.4, 0.4);
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        if (time >= Duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = time / Duration;
        float length = Size * (0.6f + 0.6f * Mathf.Sqrt(t));
        float width = Mathf.Lerp(3.2f, 0.4f, t);
        Color color = new(Color, 1f - t * t);
        for (int i = 0; i < 4; i++)
        {
            Vector2 axis = Vector2.FromAngle(i * Mathf.Pi * 0.5f);
            Vector2 side = axis.Orthogonal() * width;
            float reach = i % 2 == 0 ? length : length * 0.6f;
            DrawPolygon(new[] { side, axis * reach, -side }, new[] { color, color, color });
        }
        DrawCircle(Vector2.Zero, width * 1.2f * (1f - t), color);
    }
}
