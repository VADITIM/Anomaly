using Godot;

namespace Anomaly;

public partial class SoulOrb : Node2D
{
    private const float BURST_TIME = 0.4f;

    private GameWorld world;
    private int value;
    private Vector2 velocity;
    private float z;
    private float zVelocity;
    private float time;
    private float homingSpeed;
    private float trailTimer;

    public void Setup(GameWorld gameWorld, Vector2 position, float height, int soulValue)
    {
        world = gameWorld;
        Position = position;
        z = height;
        value = soulValue;
        velocity = Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * (float)GD.RandRange(30, 90);
        zVelocity = (float)GD.RandRange(80, 170);
        Material = Vfx.Additive;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        time += dt;
        Player player = world.Player;

        if (time < BURST_TIME || player.IsDead)
        {
            Position += velocity * dt;
            velocity = velocity.Lerp(Vector2.Zero, 1f - Mathf.Exp(-5f * dt));
            zVelocity -= 500f * dt;
            z += zVelocity * dt;
            float surface = world.Grid.SurfaceZAt(GlobalPosition);
            float floor = float.IsNegativeInfinity(surface) ? z : surface + 4f;
            if (z < floor)
            {
                z = floor;
                zVelocity = Mathf.Abs(zVelocity) * 0.4f;
            }
        }
        else
        {
            homingSpeed = Mathf.Min(520f, homingSpeed + 900f * dt);
            Vector2 toPlayer = player.GlobalPosition - GlobalPosition;
            float distance = toPlayer.Length();
            if (distance < 8f)
            {
                Collect(player);
                return;
            }
            Position += toPlayer / distance * Mathf.Min(distance, homingSpeed * dt);
            z = Mathf.Lerp(z, player.Z + 12f, 1f - Mathf.Exp(-8f * dt));
        }

        trailTimer -= dt;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.04f;
            Vfx.Burst(world.Effects, GlobalPosition + new Vector2(0, -z), 1, new Color(0.45f, 0.8f, 1f, 0.7f), 6f, Vector2.Up, 180f, 0.3f, 0f, 1.2f, 5f, true);
        }
        QueueRedraw();
    }

    private void Collect(Player player)
    {
        player.AddSouls(value);
        Sfx.Play("soul", -10f, 1f + GD.Randf() * 0.3f);
        Vfx.Burst(world.Effects, player.GlobalPosition + new Vector2(0, -player.Z - 12f), 3, new Color(0.55f, 0.9f, 1f), 40f, Vector2.Up, 180f, 0.3f, 0f, 1.5f, 30f, true);
        QueueFree();
    }

    public override void _Draw()
    {
        Vector2 center = new(0, -z);
        float pulse = 1f + 0.25f * Mathf.Sin(time * 18f);
        DrawCircle(center, 3.5f * pulse, new Color(0.3f, 0.6f, 1f, 0.35f));
        DrawColoredPolygon(new[] { center + new Vector2(0, -3f), center + new Vector2(2f, 0), center + new Vector2(0, 3f), center + new Vector2(-2f, 0) }, new Color(0.7f, 0.95f, 1f));
    }
}
