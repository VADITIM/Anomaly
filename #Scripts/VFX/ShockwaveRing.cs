using Godot;

namespace Anomaly;

public partial class ShockwaveRing : Node2D
{
    public float StartRadius = 4f;
    public float EndRadius = 36f;
    public float Thickness = 4f;
    public float Duration = 0.3f;
    public float Squash = 0.5f;
    public Color Color = new(1f, 1f, 1f, 0.9f);
    private float time;

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
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float radius = Mathf.Lerp(StartRadius, EndRadius, eased);
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, Squash));
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 40, new Color(Color, Color.A * (1f - t)), Mathf.Max(0.5f, Thickness * (1f - t)));
    }
}
