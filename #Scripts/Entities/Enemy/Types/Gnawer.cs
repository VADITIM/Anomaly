using Godot;

namespace Anomaly;

// Plague rat of the Èrmiosan burials. Darts in on a weave, bites and scurries back out of reach before the Scythe answers.
// A nest hides a pack under bones; the bones rattle and squeak before it bursts. Thin hide yields to Slashing.
// A Bloated Gnawer is slow and heavy, and when it dies it bursts into a miasma pool: kill it at reach and step away.
public partial class Gnawer : SheetEnemy
{
    private const float WAKE_RADIUS = 62f;
    private const float LUNGE_SPEED = 235f;
    private const float LUNGE_TIME = 0.16f;
    private const float RETREAT_END = 0.55f;
    private static readonly Color MiasmaColor = new(0.55f, 0.85f, 0.3f);

    public bool Bloated { get; init; }
    public bool Nested { get; init; }

    private float weave;
    private float rattleTimer;
    private bool struck;
    private Vector2 lungeDirection;
    private float retreatSide = 1f;

    public override string DisplayName => Bloated ? "Bloated Gnawer" : "Gnawer";
    public override bool CanReceivePlayerHits => base.CanReceivePlayerHits && StateMachine?.Is(EnemyState.Dormant) != true;
    protected override string SpriteFolder => "Gnawer";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Gnawer.tres";
    protected override float SpriteScale => Bloated ? 1.3f : 0.85f;
    protected override float SpriteFeetOffset => Bloated ? 14f : 10f;
    protected override float BarHeight => Bloated ? 18f : 12f;
    protected override Color IchorColor => Bloated ? MiasmaColor : new Color(0.62f, 0.2f, 0.22f);
    protected override EnemyState InitialState => Nested ? EnemyState.Dormant : EnemyState.Idle;

    protected override float ComputeMaxHealth() => base.ComputeMaxHealth() * (Bloated ? 2.4f : 1f);

    protected override void ConfigureSheet()
    {
        Define("idle", "idle", 10, 8f);
        Define("move", "run", 3, 12f);
        Define("crouch", "jump", 3, 8f);
        Define("bite", "bite", 9, 26f);
        Define("hurt", "hurt", 2, 8f);
        Define("death", "death", 6, 8f);
        Radius = Bloated ? 7f : 5f;
        HurtRadius = Bloated ? 11f : 8f;
        HeightSpan = Bloated ? 18f : 12f;
        MoveSpeed = Bloated ? 44f : 80f;
        Acceleration = 700f;
        AttackRange = Bloated ? 30f : 54f;
        AttackDamage = Bloated ? 14f : 8f;
        TelegraphTime = Bloated ? 0.55f : 0.32f;
        RecoverTime = Bloated ? 0.6f : 0.3f;
        AttackCooldown = Bloated ? 1.2f : 0.75f;
        AggroRadius = 150f;
        EvadeChance = Bloated ? 0f : 0.25f;
        EvadeSpeed = 190f;
        CounterChance = 0.2f;
        Weakness = Bloated ? DamageType.Piercing : DamageType.Slashing;
        SoulReward = Bloated ? 24 : 7;
        ResidueReward = Bloated ? 16 : 5;
        weave = GD.Randf() * 10f;
        rattleTimer = (float)GD.RandRange(0.5, 1.5);
        if (Bloated)
            Tint(new Color(0.6f, 0.9f, 0.35f), 0.35f);
    }

    protected override void AddStates(StateMachine<EnemyState> machine) =>
        machine.Add(EnemyState.Dormant, EnterDormant, UpdateDormant, ExitDormant);

    private void EnterDormant()
    {
        Body.Visible = false;
        GetNode<Node2D>("Shadow").Visible = false;
        Solid = false;
    }

    // Hidden under the pile. The bones twitch and something squeaks now and then; walking up on that is the mistake.
    private void UpdateDormant(float delta)
    {
        MoveVelocity = Vector2.Zero;
        rattleTimer -= delta;
        if (rattleTimer <= 0f)
        {
            rattleTimer = (float)GD.RandRange(1.1, 2.2);
            Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z - 4f), 4, new Color(0.85f, 0.8f, 0.7f), 40f, Vector2.Up, 70f, 0.35f, 220f, 1.5f, 20f);
            if (Target.GlobalPosition.DistanceTo(GlobalPosition) < 200f)
                Sfx.Play("step", -16f, 2.4f);
        }
        if (IsAggroed || TargetAvailable && ToTarget.Length() < WAKE_RADIUS)
            StateMachine.Transition(EnemyState.Chase);
    }

    private void ExitDormant()
    {
        Body.Visible = true;
        GetNode<Node2D>("Shadow").Visible = true;
        Solid = true;
        Vector2 burst = Vector2.FromAngle(GD.Randf() * Mathf.Tau);
        MoveVelocity = burst * 90f;
        Face(ToTarget);
        Launch(170f);
        Body.Scale = new Vector2(0.7f, 1.4f);
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z - 4f), 10, new Color(0.85f, 0.8f, 0.7f), 90f, Vector2.Up, 80f, 0.45f, 300f, 2f, 30f);
        Sfx.Play("break", -10f, 1.6f);
        Aggro();
    }

    // Never straight at Kio: a fast weave that makes the lunge's angle hard to read until the crouch.
    protected override void ChaseBehaviour(float delta)
    {
        weave += delta * (Bloated ? 3f : 7f);
        Vector2 offset = FlankApproach() - GlobalPosition;
        Vector2 direction = offset.LengthSquared() > 1f ? offset.Normalized() : ToTarget.Normalized();
        SteerDirection(direction.Rotated(Mathf.Sin(weave) * (Bloated ? 0.25f : 0.65f)), MoveSpeed, delta);
        Play("Walk", 12f);
    }

    protected override void OnTelegraphStart() => Restart("crouch", 8f);

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        Body.Scale = new Vector2(1f + 0.18f * ratio, 1f - 0.18f * ratio);
        if (Bloated && GD.Randf() < 0.4f)
            Vfx.Burst(World.Effects, VisualCenter, 1, MiasmaColor, 20f, Vector2.Up, 60f, 0.4f, -20f, 1.6f, 10f, true);
    }

    protected override void OnAttackStart()
    {
        struck = false;
        Vector2 aim = PredictTarget(0.15f) - GlobalPosition;
        lungeDirection = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
        retreatSide = GD.Randf() < 0.5f ? 1f : -1f;
        Face(lungeDirection);
        Restart("bite", Bloated ? 16f : 26f);
        Sfx.Play("enemy_attack", -8f, Bloated ? 0.8f : 1.5f);
    }

    protected override bool UpdateAttack(float delta)
    {
        float t = StateMachine.TimeInState;
        float lunge = Bloated ? 0.28f : LUNGE_TIME;
        if (t < lunge)
        {
            MoveVelocity = lungeDirection * (Bloated ? 140f : LUNGE_SPEED) * Momentum;
            if (!struck)
                struck = TryStrikePlayer(GlobalPosition + lungeDirection * 8f, Bloated ? 14f : 10f, ScaledDamage, 150f, lungeDirection);
            return false;
        }
        if (Bloated)
        {
            Brake(delta, 900f);
            return t > lunge + 0.3f;
        }
        // Hit and run: scurry back off at an angle before Kio can answer.
        if (t < RETREAT_END)
        {
            MoveVelocity = MoveVelocity.MoveToward(-lungeDirection.Rotated(0.7f * retreatSide) * 130f, 1600f * delta);
            Face(lungeDirection);
            Play("Walk", 14f);
            return false;
        }
        return true;
    }

    protected override void OnDeath(HitInfo hit)
    {
        base.OnDeath(hit);
        if (!Bloated)
            return;
        // The swelling gives it away: the pool arms a beat after the body drops.
        GroundHazard.Spawn(World, GlobalPosition, 34f, 0.55f, 3.2f, 7f, MiasmaColor);
        Sfx.Play("slime_hop", -4f, 0.5f);
    }
}
