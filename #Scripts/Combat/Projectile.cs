using Godot;

namespace Anomaly;

// Enemy residue bolt. A Scythe strike rewrites its owner: reflected bolts fly back faster and hunt enemies instead.
public partial class Projectile : Node2D, IHittable
{
    private const float LIFETIME = 3.2f;

    private GameWorld world;
    private Vector2 velocity;
    private float damage;
    private float z;
    private float zVelocity;
    private float age;
    private bool reflected;
    private float trailTimer;

    // Optional look and payload: a slime's ice shard or bile glob, which leaves a patch where it bursts.
    public Color Tint = new(0.7f, 1f, 0.25f);
    public System.Action<Vector2> OnBurst;

    public Vector2 GroundPosition => GlobalPosition;
    public float Z => z - 4f;
    public float HurtRadius => 7f;
    public float HeightSpan => 8f;
    public bool CanReceivePlayerHits => !reflected;

    // Climb is vertical speed, so a shot from above can dip to its target's height.
    public void Setup(Enemy owner, Vector2 position, float height, Vector2 direction, float speed, float hitDamage, float climb = 0f)
    {
        Position = position;
        z = height;
        zVelocity = climb;
        velocity = direction * speed;
        damage = hitDamage;
    }

    public override void _Ready()
    {
        world = NodeResolver.FindOnRoot<GameWorld>(this);
        world.RegisterHittable(this);
    }

    public override void _ExitTree() => world?.UnregisterHittable(this);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        age += dt;
        Vector2 next = Position + velocity * dt;
        if (age > LIFETIME || world.Grid.SurfaceZAt(next) > z)
        {
            Burst();
            return;
        }
        Position = next;
        z += zVelocity * dt;

        trailTimer -= dt;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.03f;
            Vfx.Burst(world.Effects, GlobalPosition + new Vector2(0, -z), 1, CoreColor, 8f, -velocity, 20f, 0.25f, 0f, 2f, 5f, true);
        }

        if (reflected)
            HitEnemies();
        else
            HitPlayer();
        QueueRedraw();
    }

    private Color CoreColor => reflected ? new Color(0.55f, 0.95f, 1f) : Tint;

    private void HitPlayer()
    {
        Player player = world.Player;
        if (player.IsDead || GlobalPosition.DistanceTo(player.GlobalPosition) > 7f + player.Radius || !player.IsWithinVerticalReach(z - 6f, 10f))
            return;
        var hit = new HitInfo { Damage = damage, Direction = velocity.Normalized(), Knockback = 120f, Source = HitSource.Enemy, Attacker = this };
        HitResponse response = player.ReceiveHit(hit);
        if (response is HitResponse.Landed or HitResponse.Killed)
            Burst();
    }

    private void HitEnemies()
    {
        foreach (Entity entity in world.Entities)
        {
            if (entity is not Enemy enemy || enemy.IsDead || GlobalPosition.DistanceTo(enemy.GlobalPosition) > 8f + enemy.HurtRadius || !enemy.IsWithinVerticalReach(z - 6f, 10f))
                continue;
            HitInfo hit = world.Player.BuildHit(enemy, 2.2f, DamageType.Piercing, velocity.Normalized(), 220f, 30f, 0.07f, true);
            world.Player.DeliverHit(enemy, hit, 0.25f);
            Burst();
            return;
        }
    }

    public HitResponse ReceiveHit(HitInfo hit)
    {
        if (reflected)
            return HitResponse.Ignored;
        reflected = true;
        zVelocity = 0f;
        age = 0f;
        Vector2 direction = world.Player.AimDirection;
        velocity = direction * velocity.Length() * 2.4f;
        Sfx.Play("catch", -2f, 1f);
        Vfx.ImpactStar(world.Effects, GlobalPosition + new Vector2(0, -z), 16f, new Color(0.6f, 0.95f, 1f));
        Vfx.Number(world.Overlay, GlobalPosition + new Vector2(0, -z - 12f), "REND", new Color(0.6f, 0.95f, 1f), 8);
        TimeControl.HitStop(0.04f);
        return HitResponse.Landed;
    }

    private void Burst()
    {
        Vfx.Burst(world.Effects, GlobalPosition + new Vector2(0, -z), 8, CoreColor, 80f, Vector2.Up, 180f, 0.3f, 100f, 2f, 40f, true);
        if (!reflected)
            OnBurst?.Invoke(GlobalPosition);
        QueueFree();
    }

    public override void _Draw()
    {
        Vector2 center = new(0, -z);
        float pulse = 1f + 0.2f * Mathf.Sin(age * 30f);
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.45f));
        DrawCircle(Vector2.Zero, 3.5f, new Color(0.02f, 0f, 0.05f, 0.3f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawCircle(center, 5f * pulse, new Color(CoreColor, 0.3f));
        DrawCircle(center, 3f * pulse, CoreColor);
        DrawCircle(center, 1.5f, Colors.White);
    }
}
