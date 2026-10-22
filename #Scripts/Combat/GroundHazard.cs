using Godot;

namespace Anomaly;

// A lingering patch on the floor plane: frost, bile or a burst Gnawer's miasma. While it arms it only shows a pulsing rim,
// so it can always be read before it bites; once armed it hurts whoever stands in it every half second.
public partial class GroundHazard : Node2D
{
    private const float TICK = 0.5f;
    private const float FADE = 0.4f;

    private GameWorld world;
    private float radius;
    private float delay;
    private float duration;
    private float damage;
    private Color color;
    private float surface;
    private float age;
    private float tick;

    public static void Spawn(GameWorld world, Vector2 position, float radius, float delay, float duration, float damage, Color color)
    {
        float z = world.Grid.SurfaceZAt(position);
        if (float.IsNegativeInfinity(z))
            return;
        var hazard = new GroundHazard { Name = "Hazard", Position = position, ZIndex = -40 };
        hazard.world = world;
        hazard.radius = radius;
        hazard.delay = delay;
        hazard.duration = duration;
        hazard.damage = damage;
        hazard.color = color;
        hazard.surface = z;
        world.AddChild(hazard);
    }

    private bool Armed => age >= delay;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        age += dt;
        if (age > delay + duration)
        {
            QueueFree();
            return;
        }
        if (Armed)
        {
            tick -= dt;
            if (tick <= 0f && Contains(world.Player))
            {
                tick = TICK;
                Vector2 away = world.Player.GlobalPosition - GlobalPosition;
                world.Player.ReceiveHit(new HitInfo
                {
                    Damage = damage,
                    Direction = away.LengthSquared() > 0.1f ? away.Normalized() : Vector2.Down,
                    Knockback = 40f,
                    Source = HitSource.Environment,
                    Attacker = this,
                });
            }
            if (GD.Randf() < dt * 10f)
            {
                Vector2 spot = new Vector2((float)GD.RandRange(-radius, radius), (float)GD.RandRange(-radius, radius) * 0.5f) * 0.8f;
                Vfx.Burst(world.Effects, GlobalPosition + spot + new Vector2(0, -surface), 1, new Color(color, 0.8f), 10f, Vector2.Up, 25f, 0.8f, -12f, 1.4f, 4f, true);
            }
        }
        QueueRedraw();
    }

    private bool Contains(Player player)
    {
        if (player.IsDead || player.Z - surface > 10f)
            return false;
        Vector2 offset = player.GlobalPosition - GlobalPosition;
        offset.Y *= 2f;
        return offset.Length() < radius + player.Radius;
    }

    public override void _Draw()
    {
        float remaining = delay + duration - age;
        float alpha = Mathf.Clamp(remaining / FADE, 0f, 1f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * 18f);
        DrawSetTransform(new Vector2(0, -surface), 0f, new Vector2(1f, 0.5f));
        if (!Armed)
        {
            float grow = Mathf.Clamp(age / Mathf.Max(delay, 0.01f), 0f, 1f);
            DrawArc(Vector2.Zero, radius * (0.4f + 0.6f * grow), 0f, Mathf.Tau, 32, new Color(color, 0.5f + 0.5f * pulse), 2f);
        }
        else
        {
            DrawCircle(Vector2.Zero, radius, new Color(color.Darkened(0.35f), 0.38f * alpha));
            DrawCircle(Vector2.Zero, radius * 0.6f, new Color(color, 0.22f * alpha));
            DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 32, new Color(color.Lightened(0.3f), 0.7f * alpha), 1.5f);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
