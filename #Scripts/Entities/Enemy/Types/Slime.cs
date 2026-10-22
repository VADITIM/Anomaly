using Godot;

namespace Anomaly;

public enum SlimeKind
{
    Rime,
    Bog,
    Slimelet,
}

// An ooze that crawls, lashes at arm's length and spits at range, hopping back when Kio closes in.
// Rimeslime (Glaciemera): ice shards that leave a frost patch where they burst; brittle, it shatters under Smashing.
// Bogslime (Elaphrium): bile globs that pool, and when it dies it splits into two Slimelets.
// Slimelet: a fast, small fragment that only lashes.
public partial class Slime : SheetEnemy
{
    private const float SPIT_MIN_RANGE = 56f;
    private const float SPIT_MAX_RANGE = 140f;
    private const float SPIT_COOLDOWN = 3.2f;
    private const float PROJECTILE_SPEED = 110f;
    private const float HOP_COOLDOWN = 2.4f;
    private const float PREFERRED_DISTANCE = 86f;
    private static readonly Color FrostColor = new(0.6f, 0.88f, 1f);
    private static readonly Color BileColor = new(0.5f, 0.85f, 0.25f);

    public SlimeKind Kind { get; init; } = SlimeKind.Rime;

    private bool spitQueued;
    private bool spitting;
    private bool acted;
    private float spitCooldown = 1.5f;
    private float hopCooldown;

    public override string DisplayName => Kind switch { SlimeKind.Bog => "Bogslime", SlimeKind.Slimelet => "Slimelet", _ => "Rimeslime" };
    public override bool IsRanged => Kind != SlimeKind.Slimelet;
    protected override string SpriteFolder => "Slime";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Slime.tres";
    protected override Vector2I FrameSize => new(96, 64);
    protected override float SpriteScale => Kind switch { SlimeKind.Bog => 1.15f, SlimeKind.Slimelet => 0.6f, _ => 1f };
    protected override float SpriteFeetOffset => Kind == SlimeKind.Slimelet ? 6f : 10f;
    protected override float BarHeight => Kind == SlimeKind.Slimelet ? 10f : 14f;
    protected override Color IchorColor => Kind == SlimeKind.Rime ? FrostColor : BileColor;
    protected override float StandoffDistance => IsRanged ? PREFERRED_DISTANCE + 20f : base.StandoffDistance;
    private Color PayloadColor => Kind == SlimeKind.Rime ? FrostColor : BileColor;

    protected override float ComputeMaxHealth() => base.ComputeMaxHealth() * (Kind == SlimeKind.Slimelet ? 0.3f : Kind == SlimeKind.Bog ? 1.15f : 1f);

    protected override void ConfigureSheet()
    {
        Define("idle", "idle", 10, 8f);
        Define("move", "crawl", 10, 12f);
        Define("hop", "jump", 10, 16f);
        Define("lash", "lash", 12, 20f);
        Define("spit", "spit", 9, 16f);
        Define("hurt", "hurt", 2, 8f);
        Define("death", "death", 10, 12f);
        bool small = Kind == SlimeKind.Slimelet;
        Radius = small ? 5f : 8f;
        HurtRadius = small ? 7f : 11f;
        HeightSpan = small ? 8f : 14f;
        MoveSpeed = small ? 52f : 28f;
        AttackRange = small ? 24f : 34f;
        AttackDamage = small ? 7f : 13f;
        TelegraphTime = small ? 0.35f : 0.5f;
        RecoverTime = 0.5f;
        AttackCooldown = small ? 0.8f : 1.1f;
        AggroRadius = 150f;
        Weakness = Kind == SlimeKind.Rime ? DamageType.Smashing : DamageType.Slashing;
        SoulReward = small ? 4 : 16;
        ResidueReward = small ? 3 : 12;
        if (Kind != SlimeKind.Rime)
            Tint(new Color(0.45f, 0.85f, 0.3f), 0.6f);
    }

    protected override void TickEntity(float delta)
    {
        if (spitCooldown > 0f)
            spitCooldown -= delta;
        if (hopCooldown > 0f)
            hopCooldown -= delta;
        base.TickEntity(delta);
    }

    protected override void ChaseBehaviour(float delta)
    {
        if (!IsGrounded)
        {
            Play("hop", 16f, false);
            // Momentum fades through the arc so a hop can never carry the slime on after it touches down.
            Brake(delta, 120f);
            return;
        }
        float distance = ToTarget.Length();
        if (IsRanged && distance < 46f && hopCooldown <= 0f && Hop(-ToTarget.Normalized()))
            return;
        Vector2 goal = IsRanged && spitCooldown <= 1f ? Target.GlobalPosition - ToTarget.Normalized() * PREFERRED_DISTANCE : FlankApproach();
        SteerToward(goal, MoveSpeed, delta);
        Play("Walk", 10f);
    }

    // A squelching bound out of reach: the hop the lash recovery leaves open.
    private bool Hop(Vector2 direction)
    {
        foreach (float angle in new[] { 0f, 0.6f, -0.6f, 1.2f, -1.2f })
        {
            Vector2 candidate = direction.Rotated(angle);
            if (!IsPathClear(candidate))
                continue;
            hopCooldown = HOP_COOLDOWN;
            Launch(220f);
            MoveVelocity = candidate * 120f;
            Body.Scale = new Vector2(0.75f, 1.3f);
            Restart("hop", 16f);
            Sfx.Play("slime_hop", -10f, 0.8f);
            return true;
        }
        return false;
    }

    protected override void OnLanded(float impactSpeed)
    {
        MoveVelocity = Vector2.Zero;
        // Touching down mid-windup or mid-lash keeps the attack strip; only the hop hands back to the crawl.
        if (StateMachine.Current is not (EnemyState.Telegraph or EnemyState.Attack))
            Play("Walk", 4f);
    }

    protected override bool WantsToAttack()
    {
        spitQueued = false;
        // Mid-hop or knocked aloft it would wind up while sliding through the air, frozen on the first windup frame.
        if (!IsGrounded)
            return false;
        float distance = ToTarget.Length();
        if (distance <= AttackRange)
            return true;
        if (IsRanged && spitCooldown <= 0f && distance >= SPIT_MIN_RANGE && distance <= SPIT_MAX_RANGE && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z))
        {
            spitQueued = true;
            return true;
        }
        return false;
    }

    protected override void OnTelegraphStart()
    {
        spitting = spitQueued;
        // The windup draws the strip's first four frames across the whole telegraph, so it never holds one pose.
        Restart(spitting ? "spit" : "lash", 4f / Mathf.Max(TelegraphTime, 0.1f));
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        Body.Scale = new Vector2(1f + 0.15f * ratio, 1f - 0.12f * ratio);
        if (spitting && GD.Randf() < 0.4f)
            Vfx.Burst(World.Effects, VisualCenter, 1, PayloadColor, 20f, Vector2.Up, 60f, 0.3f, 0f, 1.5f, 10f, true);
    }

    protected override void OnAttackStart()
    {
        acted = false;
        if (spitting)
        {
            spitCooldown = SPIT_COOLDOWN;
            Restart("spit", 16f);
            return;
        }
        Restart("lash", 22f);
        Sfx.Play("slime_hop", -6f, 1.3f);
    }

    protected override bool UpdateAttack(float delta)
    {
        Brake(delta, 900f);
        float t = StateMachine.TimeInState;
        if (spitting)
        {
            if (!acted && t >= 0.3f)
            {
                acted = true;
                Spit();
            }
            return t >= 0.55f;
        }
        // The lash reaches on frame 5 of the strip.
        if (!acted && t >= 0.23f)
        {
            acted = true;
            Vector2 center = GlobalPosition + FacingDirection * (Kind == SlimeKind.Slimelet ? 10f : 20f);
            TryStrikePlayer(center, Kind == SlimeKind.Slimelet ? 10f : 18f, ScaledDamage, 180f, FacingDirection);
            Vfx.Burst(World.Effects, center + new Vector2(0, -Z - 6f), 6, PayloadColor, 70f, FacingDirection, 50f, 0.3f, 200f, 1.8f, 30f);
        }
        return t >= 0.55f;
    }

    private void Spit()
    {
        Vector2 aim = PredictTarget(ToTarget.Length() / PROJECTILE_SPEED) - GlobalPosition;
        Vector2 direction = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
        Face(direction);
        float flightTime = Mathf.Max(ToTarget.Length(), 1f) / PROJECTILE_SPEED;
        var projectile = new Projectile { Tint = PayloadColor };
        projectile.OnBurst = spot => GroundHazard.Spawn(World, spot, Kind == SlimeKind.Rime ? 22f : 26f, 0.25f, 2.6f, 5f, PayloadColor);
        projectile.Setup(this, GlobalPosition + direction * 8f, Z + 8f, direction, PROJECTILE_SPEED, ScaledDamage, (Target.Z + 8f - (Z + 8f)) / flightTime);
        World.Actors.AddChild(projectile);
        Sfx.Play("spit", -4f, Kind == SlimeKind.Rime ? 1.2f : 0.7f);
    }

    protected override void OnDeath(HitInfo hit)
    {
        base.OnDeath(hit);
        if (Kind != SlimeKind.Bog)
            return;
        Vector2 side = hit.Direction.LengthSquared() > 0.01f ? hit.Direction.Orthogonal() : Vector2.Right;
        foreach (float sign in new[] { 1f, -1f })
        {
            Vector2 spot = GlobalPosition + side * sign * 10f;
            CallDeferred(MethodName.SpawnSlimelet, spot);
        }
    }

    private void SpawnSlimelet(Vector2 spot)
    {
        Enemy slimelet = World.SpawnEnemy(PlacementKind.Slimelet, spot);
        slimelet.SpawnedByEncounter = SpawnedByEncounter;
        slimelet.Launch(180f);
        slimelet.MoveVelocity = (spot - GlobalPosition).Normalized() * 70f;
        slimelet.Aggro();
    }
}
