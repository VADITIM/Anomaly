using Godot;

namespace Anomaly;

// Hangs asleep over graves and ravines, and the sleeping shape is the tell: walk under it and it wakes.
// Flies a weaving orbit, dives through Kio with a bite, and when cornered or wounded screeches a ring that throws Kio back.
// Membrane wings yield to Piercing.
public partial class GloomBat : SheetEnemy
{
    private const float WAKE_RADIUS = 84f;
    private const float WAKE_TIME = 0.8f;
    private const float HANG_ALTITUDE = 30f;
    private const float FLY_ALTITUDE = 18f;
    private const float ORBIT_DISTANCE = 56f;
    private const float DIVE_SPEED = 245f;
    private const float DIVE_TIME = 0.32f;
    private const float SCREECH_RADIUS = 52f;
    private const float SCREECH_COOLDOWN = 4.5f;
    private static readonly Color ScreechColor = new(0.7f, 0.45f, 1f);

    public bool Sleeping { get; init; } = true;

    private float groundZ;
    private float bob;
    private float orbitSign = 1f;
    private bool waking;
    private float wakeStarted;
    private bool screechQueued;
    private bool screeching;
    private bool struck;
    private float screechCooldown = 2f;
    private Vector2 diveDirection;
    private float altitude;

    public override string DisplayName => "Gloom Bat";
    public override bool IsRanged => true;
    protected override string SpriteFolder => "GloomBat";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/GloomBat.tres";
    protected override float FeetInFrame => 6f;
    protected override float SpriteScale => 0.85f;
    protected override float SpriteFeetOffset => 12f;
    protected override float BarHeight => 12f;
    protected override Color IchorColor => new(0.55f, 0.3f, 0.75f);
    protected override EnemyState InitialState => Sleeping ? EnemyState.Dormant : EnemyState.Idle;

    protected override void ConfigureSheet()
    {
        Define("idle", "fly", 9, 12f);
        Define("move", "run", 8, 14f);
        Define("sleep", "sleep", 3, 3f);
        Define("wake", "wake", 16, 16f / WAKE_TIME);
        Define("bite", "bite", 8, 22f);
        Define("screech", "screech", 11, 18f);
        Define("hurt", "hurt", 5, 14f);
        Define("death", "death", 11, 14f);
        Radius = 5f;
        HurtRadius = 10f;
        HeightSpan = 14f;
        IsFlying = true;
        CarefulMovement = false;
        MoveSpeed = 72f;
        Acceleration = 420f;
        AttackRange = 96f;
        AttackDamage = 11f;
        TelegraphTime = 0.42f;
        RecoverTime = 0.5f;
        AttackCooldown = 1.1f;
        AggroRadius = 160f;
        EvadeChance = 0.35f;
        EvadeSpeed = 170f;
        Weakness = DamageType.Piercing;
        SoulReward = 12;
        ResidueReward = 9;
        groundZ = SupportZ;
        altitude = Sleeping ? HANG_ALTITUDE : FLY_ALTITUDE;
        Z = groundZ + altitude;
        bob = GD.Randf() * 10f;
        orbitSign = GD.Randf() < 0.5f ? 1f : -1f;
    }

    protected override void AddStates(StateMachine<EnemyState> machine) =>
        machine.Add(EnemyState.Dormant, EnterDormant, UpdateDormant);

    protected override void TickEntity(float delta)
    {
        bob += delta;
        if (screechCooldown > 0f)
            screechCooldown -= delta;
        if (!IsDead)
        {
            if (!float.IsNegativeInfinity(SupportZ))
                groundZ = SupportZ;
            float sway = StateMachine.Is(EnemyState.Dormant) && !waking ? 0.6f : 2.5f;
            Z = Mathf.Lerp(Z, groundZ + altitude + Mathf.Sin(bob * 3.6f) * sway, 1f - Mathf.Exp(-7f * delta));
        }
        base.TickEntity(delta);
    }

    private void EnterDormant()
    {
        waking = false;
        Restart("sleep", 3f, true);
    }

    private void UpdateDormant(float delta)
    {
        MoveVelocity = Vector2.Zero;
        if (!waking)
        {
            if (IsAggroed || TargetAvailable && ToTarget.Length() < WAKE_RADIUS)
            {
                waking = true;
                wakeStarted = StateMachine.TimeInState;
                Restart("wake", 16f / WAKE_TIME);
                Sfx.Play("telegraph", -8f, 1.6f);
                Face(ToTarget);
            }
            return;
        }
        altitude = Mathf.MoveToward(altitude, FLY_ALTITUDE, delta * 20f);
        if (StateMachine.TimeInState - wakeStarted >= WAKE_TIME)
        {
            Aggro();
            StateMachine.Transition(EnemyState.Chase);
        }
    }

    // Weave a loose orbit around Kio, dipping in and out of reach.
    protected override void ChaseBehaviour(float delta) => Orbit(ORBIT_DISTANCE, delta);

    protected override void HoldStandoff(float delta) => Orbit(StandoffDistance + 10f, delta);

    private void Orbit(float preferred, float delta)
    {
        Vector2 offset = ToTarget;
        float distance = offset.Length();
        Vector2 radial = distance > 0.1f ? offset / distance : Vector2.Down;
        Vector2 desire = radial.Orthogonal() * orbitSign * 0.8f + radial * Mathf.Clamp((distance - preferred) / 30f, -1f, 1f);
        if (GD.Randf() < delta * 0.4f)
            orbitSign = -orbitSign;
        SteerDirection(desire.Normalized(), MoveSpeed, delta);
        Face(offset);
        Play("Walk", 14f);
    }

    protected override bool WantsToAttack()
    {
        float distance = ToTarget.Length();
        screechQueued = screechCooldown <= 0f && distance < SCREECH_RADIUS * 0.9f && (HealthRatio < 0.6f || GD.Randf() < 0.35f);
        return screechQueued || distance <= AttackRange && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z);
    }

    protected override float TelegraphFor() => screechQueued ? 0.6f : TelegraphTime;

    protected override void OnTelegraphStart()
    {
        screeching = screechQueued;
        Restart(screeching ? "screech" : "idle", screeching ? 6f : 20f, !screeching);
        if (!screeching)
            altitude = FLY_ALTITUDE + 8f;
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        if (screeching && GD.Randf() < 0.25f)
            Vfx.Ring(World.Effects, VisualCenter, 2f, 10f + 10f * ratio, new Color(ScreechColor, 0.6f), 0.2f, 1.5f, 0.8f);
    }

    protected override void OnAttackStart()
    {
        struck = false;
        if (screeching)
        {
            screechCooldown = SCREECH_COOLDOWN;
            Restart("screech", 22f);
            return;
        }
        Vector2 aim = PredictTarget(0.2f) - GlobalPosition;
        diveDirection = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
        Face(diveDirection);
        Restart("bite", 24f);
        Sfx.Play("dash", -10f, 1.5f);
    }

    protected override bool UpdateAttack(float delta)
    {
        float t = StateMachine.TimeInState;
        if (screeching)
        {
            Brake(delta, 900f);
            if (!struck && t >= 0.18f)
            {
                struck = true;
                Vfx.Ring(World.Effects, VisualCenter, 6f, SCREECH_RADIUS + 10f, ScreechColor, 0.4f, 4f, 0.6f);
                Vfx.Ring(World.Effects, VisualCenter, 4f, SCREECH_RADIUS * 0.6f, new Color(1f, 1f, 1f, 0.6f), 0.25f, 2f, 0.6f);
                Sfx.Play("void_rupture", -10f, 2.2f);
                World.Camera.Feedback.AddTrauma(0.15f);
                Vector2 away = -ToTarget;
                if (ToTarget.Length() < SCREECH_RADIUS + Target.Radius)
                    TryStrikePlayer(GlobalPosition, SCREECH_RADIUS, ScaledDamage * 0.8f, 300f, away.LengthSquared() > 0.1f ? -away : Vector2.Down);
            }
            return t >= 0.6f;
        }
        if (t < DIVE_TIME)
        {
            altitude = 6f;
            MoveVelocity = diveDirection * DIVE_SPEED * Momentum;
            if (!struck)
                struck = TryStrikePlayer(GlobalPosition + diveDirection * 6f, 11f, ScaledDamage, 160f, diveDirection);
            if ((int)(t * 50f) % 3 == 0)
                Vfx.AfterImage(World.Actors, this, Sprite, Z, new Color(IchorColor, 0.3f), 0.18f);
            return false;
        }
        altitude = FLY_ALTITUDE;
        Brake(delta, 500f);
        return t >= DIVE_TIME + 0.25f;
    }

    protected override void OnAttackEnd() => altitude = FLY_ALTITUDE;

    protected override void OnDeath(HitInfo hit)
    {
        IsFlying = false;
        base.OnDeath(hit);
    }
}
