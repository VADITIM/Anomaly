using Godot;

namespace Anomaly;

// Overhead anticipation mark. While an enemy winds up, a red diamond fills as four ticks close in on it;
// the instant the attack commits it bursts into a white glint, which is the player's cue to dodge.
public partial class AttackCue : Node2D
{
    private const float STRIKE_DURATION = 0.3f;
    private const float TICK_START = 17f;
    private const float TICK_END = 8f;
    private const float DIAMOND_SIZE = 5.5f;

    private static readonly Color ChargeColor = new(1f, 0.18f, 0.26f);
    private static readonly Color GlintColor = new(1f, 0.97f, 0.86f);

    private enum Phase
    {
        Hidden,
        Charging,
        Striking,
    }

    private Phase phase = Phase.Hidden;
    private float charge;
    private float strikeTime;
    private float time;

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = 46;
        Visible = false;
    }

    public void SetCharge(float ratio)
    {
        if (phase == Phase.Striking)
            return;
        phase = Phase.Charging;
        charge = Mathf.Clamp(ratio, 0f, 1f);
        Visible = true;
    }

    public void Strike()
    {
        phase = Phase.Striking;
        strikeTime = 0f;
        Visible = true;
    }

    public void Cancel()
    {
        if (phase == Phase.Charging)
            Clear();
    }

    private void Clear()
    {
        phase = Phase.Hidden;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        float step = (float)delta;
        time += step;
        if (phase == Phase.Striking)
        {
            strikeTime += step;
            if (strikeTime >= STRIKE_DURATION)
                Clear();
        }
        if (Visible)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (phase == Phase.Charging)
            DrawCharge();
        else if (phase == Phase.Striking)
            DrawStrike(strikeTime / STRIKE_DURATION);
    }

    private void DrawCharge()
    {
        float eased = charge * charge;
        float pulse = 0.7f + 0.3f * Mathf.Sin(time * (14f + charge * 26f));
        Color color = new(ChargeColor, Mathf.Lerp(0.7f, 1f, charge) * pulse);
        DrawDiamond(DIAMOND_SIZE, new Color(0.05f, 0f, 0.05f, 0.75f), true);
        DrawDiamond(DIAMOND_SIZE * Mathf.Max(0.15f, eased), color, true);
        DrawDiamond(DIAMOND_SIZE, color, false);

        float reach = Mathf.Lerp(TICK_START, TICK_END, eased);
        for (int i = 0; i < 4; i++)
        {
            Vector2 axis = Vector2.FromAngle(i * Mathf.Pi * 0.5f);
            Vector2 tip = (axis * reach).Round();
            Vector2 side = axis.Orthogonal() * 2.5f;
            DrawPolygon(new[] { tip, tip + axis * 4f + side, tip + axis * 4f - side }, new[] { color, color, color });
        }
    }

    private void DrawStrike(float t)
    {
        float fade = 1f - t * t;
        float length = Mathf.Lerp(10f, 22f, Mathf.Sqrt(t));
        float width = Mathf.Lerp(3.2f, 0.6f, t);
        Color glint = new(GlintColor, fade);
        for (int i = 0; i < 4; i++)
        {
            Vector2 axis = Vector2.FromAngle(i * Mathf.Pi * 0.5f);
            Vector2 side = axis.Orthogonal() * width;
            float reach = i % 2 == 0 ? length * 0.7f : length;
            DrawPolygon(new[] { side, axis * reach, -side }, new[] { glint, glint, glint });
        }
        DrawArc(Vector2.Zero, Mathf.Lerp(5f, 16f, t), 0f, Mathf.Tau, 16, new Color(ChargeColor, fade * 0.8f), Mathf.Lerp(2f, 0.5f, t));
        DrawDiamond(DIAMOND_SIZE * (1f + t * 0.4f), new Color(GlintColor.Lerp(ChargeColor, t), fade), true);
    }

    private void DrawDiamond(float size, Color color, bool filled)
    {
        Vector2[] points = { new(0, -size), new(size, 0), new(0, size), new(-size, 0) };
        if (filled)
        {
            DrawPolygon(points, new[] { color, color, color, color });
            return;
        }
        DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, color, 1f);
    }
}
