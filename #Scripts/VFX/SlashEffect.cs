using Godot;

namespace Anomaly;

// Procedural crescent: the head sweeps across the arc, then the tail chases it while the whole shape fades.
public partial class SlashEffect : Node2D
{
    private const int SEGMENTS = 22;

    public float Angle;
    public float ArcRadians = Mathf.Pi * 0.8f;
    public float Radius = 30f;
    public float Thickness = 10f;
    public float Duration = 0.2f;
    public float SweepPortion = 0.38f;
    public bool Clockwise = true;
    public Color Color = new(0.75f, 0.95f, 1f);
    public Color CoreColor = Colors.White;

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
        float head = 1f - Mathf.Pow(1f - Mathf.Clamp(t / SweepPortion, 0f, 1f), 3f);
        float tail = t < SweepPortion ? 0f : Mathf.Pow((t - SweepPortion) / (1f - SweepPortion), 1.5f);
        float fade = t < SweepPortion ? 1f : 1f - (t - SweepPortion) / (1f - SweepPortion);
        float radius = Radius * (1f + 0.1f * t);

        DrawCrescent(tail, head, radius, Thickness, Color, fade);
        DrawCrescent(Mathf.Lerp(tail, head, 0.35f), head, radius - 1f, Thickness * 0.4f, CoreColor, fade);
    }

    private void DrawCrescent(float tailFraction, float headFraction, float radius, float thickness, Color color, float fade)
    {
        if (headFraction - tailFraction < 0.01f)
            return;
        float start = Angle - ArcRadians * 0.5f;
        float end = Angle + ArcRadians * 0.5f;
        if (!Clockwise)
            (start, end) = (end, start);

        for (int i = 0; i < SEGMENTS; i++)
        {
            float u0 = i / (float)SEGMENTS;
            float u1 = (i + 1) / (float)SEGMENTS;
            float a0 = Mathf.Lerp(start, end, Mathf.Lerp(tailFraction, headFraction, u0));
            float a1 = Mathf.Lerp(start, end, Mathf.Lerp(tailFraction, headFraction, u1));
            float w0 = Profile(u0) * thickness;
            float w1 = Profile(u1) * thickness;
            Vector2 d0 = Vector2.FromAngle(a0);
            Vector2 d1 = Vector2.FromAngle(a1);
            Color c0 = new(color, fade * (0.1f + 0.9f * u0));
            Color c1 = new(color, fade * (0.1f + 0.9f * u1));
            DrawPolygon(
                new[] { d0 * radius, d1 * radius, d1 * (radius - w1), d0 * (radius - w0) },
                new[] { c0, c1, c1, c0 });
        }
    }

    private static float Profile(float u) => Mathf.Max(0.08f, Mathf.Pow(Mathf.Sin(Mathf.Pi * Mathf.Clamp(u * 0.92f + 0.04f, 0f, 1f)), 0.6f) * (0.55f + 0.45f * u));
}
