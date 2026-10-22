using Godot;

namespace Anomaly;

public partial class VoidHeartVisual : Node2D
{
    private VoidHeart heart;

    public override void _Ready() => heart = GetParent().GetParent<VoidHeart>();

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (heart.Phase == VoidHeartPhase.Ruptured)
            return;
        float t = heart.PulseTime;
        float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 3f)), 8f);
        Vector2 core = new(0, -26f + Mathf.Sin(t * 1.4f) * 1.5f);
        float health = heart.HealthRatio;

        for (int i = 0; i < 7; i++)
        {
            float angle = i / 7f * Mathf.Tau + t * 0.3f;
            Vector2 tip = core + Vector2.FromAngle(angle) * (22f + Mathf.Sin(t * 2f + i) * 4f) + new Vector2(0, 10f);
            DrawLine(core, tip, new Color(0.25f, 0.02f, 0.3f, 0.9f), 2f);
            DrawLine(core, tip.Lerp(core, 0.4f), new Color(0.75f, 0.15f, 0.65f, 0.7f), 1f);
        }

        DrawCircle(core, 17f + beat * 3f, new Color(0.85f, 0.15f, 0.75f, 0.18f));
        DrawCircle(core, 12f + beat * 2f, new Color(0.15f, 0.0f, 0.2f, 1f));
        DrawCircle(core, 9f + beat * 2f, new Color(0.55f, 0.06f, 0.5f, 1f));
        DrawCircle(core, 5f + beat * 2.5f, new Color(1f, 0.45f, 0.9f, 1f));
        DrawCircle(core + new Vector2(-2f, -2f), 2f, new Color(1f, 0.9f, 1f, 0.9f));

        if (health < 0.66f)
            DrawLine(core + new Vector2(-6, -4), core + new Vector2(3, 5), new Color(1f, 0.9f, 1f), 1f);
        if (health < 0.33f)
            DrawLine(core + new Vector2(5, -6), core + new Vector2(-1, 2), new Color(1f, 0.9f, 1f), 1f);

        if (heart.Shielded)
        {
            float shimmer = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            DrawArc(core, 21f, 0f, Mathf.Tau, 32, new Color(0.65f, 0.5f, 1f, 0.35f + shimmer * 0.3f), 1.5f);
            DrawArc(core, 23f, t, t + Mathf.Pi * 0.6f, 12, new Color(0.85f, 0.75f, 1f, 0.7f), 1f);
            DrawArc(core, 23f, t + Mathf.Pi, t + Mathf.Pi * 1.6f, 12, new Color(0.85f, 0.75f, 1f, 0.7f), 1f);
        }
    }
}
