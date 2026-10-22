using Godot;

namespace Anomaly;

// Èrmiosan burial guard, still standing its post. Up close it throws a two-blow combo; at range it leaps into an overhead slam
// that cracks the ground. Cut down, it collapses into a rattling pile and stands again a few seconds later at half strength,
// unless the pile is struck while it lies there or the killing blow was Smashing.
// Rimebound walkers (Glaciemera) are frozen through: slower, shrugging off light blows, and their slam leaves a frost patch,
// but the cold has taken whatever let them rise again.
public partial class Bonewalker : SheetEnemy
{
    private const float SLAM_MIN_RANGE = 58f;
    private const float SLAM_MAX_RANGE = 118f;
    private const float SLAM_COOLDOWN = 5f;
    private const float SLAM_VELOCITY = 300f;
    private const float RISE_AFTER = 4f;
    private const float RISE_TIME = 0.75f;
    private static readonly Color RattleColor = new(0.7f, 0.5f, 0.95f);
    private static readonly Color FrostColor = new(0.65f, 0.88f, 1f);

    public bool Rimebound { get; init; }

    private bool slamQueued;
    private bool slamming;
    private bool landed;
    private float landedAt;
    private float slamCooldown = 2f;
    private int blows;
    private bool reassembled;
    private bool rising;

    public override string DisplayName => Rimebound ? "Rimebound Walker" : "Bonewalker";
    protected override string SpriteFolder => "Bonewalker";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Bonewalker.tres";
    protected override float SpriteScale => Rimebound ? 1.15f : 1f;
    protected override float SpriteFeetOffset => 14f;
    protected override float BarHeight => 18f;
    protected override float BaseCameraPriority => 1.5f;
    protected override Color IchorColor => Rimebound ? FrostColor : new Color(0.85f, 0.82f, 0.72f);
    private bool CanReassemble => !Rimebound && !reassembled;

    protected override float ComputeMaxHealth() => base.ComputeMaxHealth() * (Rimebound ? 1.35f : 1f);

    protected override void ConfigureSheet()
    {
        Define("idle", "idle", 11, 8f);
        Define("move", "walk", 8, 9f);
        Define("attack", "attack", 10, 18f);
        Define("slam", "slam", 12, 14f);
        Define("hurt", "hurt", 2, 8f);
        Define("death", "death", 6, 8f);
        Define("rise", "death", 6, 8f, true);
        Radius = 6f;
        HurtRadius = 9f;
        HeightSpan = 26f;
        MoveSpeed = Rimebound ? 30f : 40f;
        AttackRange = 30f;
        AttackDamage = Rimebound ? 18f : 14f;
        TelegraphTime = Rimebound ? 0.55f : 0.45f;
        RecoverTime = 0.55f;
        AttackCooldown = 0.9f;
        AggroRadius = 150f;
        FlinchResistance = Rimebound ? 70f : 0f;
        CounterChance = 0.4f;
        Weakness = DamageType.Smashing;
        SoulReward = Rimebound ? 30 : 22;
        ResidueReward = Rimebound ? 22 : 16;
        if (Rimebound)
            Tint(new Color(0.55f, 0.8f, 1f), 0.45f);
    }

    protected override void AddStates(StateMachine<EnemyState> machine) =>
        machine.Add(EnemyState.Downed, EnterDowned, UpdateDowned, ExitDowned);

    protected override void TickEntity(float delta)
    {
        if (slamCooldown > 0f)
            slamCooldown -= delta;
        base.TickEntity(delta);
    }

    protected override void ChaseBehaviour(float delta)
    {
        SteerToward(FlankApproach(), MoveSpeed, delta);
        Play("Walk", 9f);
    }

    protected override bool WantsToAttack()
    {
        float distance = ToTarget.Length();
        slamQueued = false;
        if (distance <= AttackRange)
            return true;
        if (slamCooldown <= 0f && distance >= SLAM_MIN_RANGE && distance <= SLAM_MAX_RANGE && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z))
        {
            slamQueued = true;
            return true;
        }
        return false;
    }

    protected override float TelegraphFor() => slamQueued ? 0.7f : TelegraphTime;

    protected override void OnTelegraphStart()
    {
        slamming = slamQueued;
        blows = 0;
        Restart(slamming ? "slam" : "attack", slamming ? 5f : 4f);
        Sfx.Play("telegraph", -6f, slamming ? 0.6f : 0.9f);
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        if (slamming)
            Body.Scale = new Vector2(1f - 0.1f * ratio, 1f + 0.12f * ratio);
    }

    protected override void OnAttackStart()
    {
        if (slamming)
        {
            slamCooldown = SLAM_COOLDOWN;
            landed = false;
            float airTime = 2f * SLAM_VELOCITY / GRAVITY;
            Vector2 offset = PredictTarget(airTime) - GlobalPosition;
            float distance = Mathf.Min(offset.Length(), SLAM_MAX_RANGE);
            MoveVelocity = offset.Normalized() * (distance / airTime);
            Face(offset);
            Launch(SLAM_VELOCITY);
            Restart("slam", 12f / airTime * 0.8f);
            Sfx.Play("jump", -6f, 0.7f);
            return;
        }
        Restart("attack", 20f);
        MoveVelocity = FacingDirection * 70f;
        Sfx.Play("enemy_attack", -4f, 1.1f);
    }

    protected override bool UpdateAttack(float delta)
    {
        float t = StateMachine.TimeInState;
        if (slamming)
        {
            if (!landed)
                return false;
            Brake(delta, 1400f);
            return t - landedAt > 0.35f;
        }
        Brake(delta, 600f);
        // Two blows on the strip's own beats: frame 3 and frame 7.
        if (blows == 0 && t >= 0.15f)
            Blow();
        if (blows == 1 && t >= 0.27f && t < 0.3f)
            SignalDodge();
        if (blows == 1 && t >= 0.35f)
        {
            Face(PredictTarget(0.1f) - GlobalPosition);
            MoveVelocity = FacingDirection * 60f;
            Blow();
        }
        return t >= 0.55f;
    }

    private void Blow()
    {
        blows++;
        Vector2 center = GlobalPosition + FacingDirection * 16f;
        TryStrikePlayer(center, 15f, ScaledDamage * (blows == 1 ? 0.8f : 1f), 200f, FacingDirection);
        Vfx.Slash(World.Effects, center + new Vector2(0, -Z - 12f), FacingDirection.Angle(), 1.6f, 14f, 2f, new Color(0.9f, 0.88f, 0.8f, 0.8f), blows == 1, 0.14f);
    }

    protected override void OnLanded(float impactSpeed)
    {
        Body.Scale = new Vector2(1.3f, 0.75f);
        if (!StateMachine.Is(EnemyState.Attack) || !slamming || landed)
            return;
        landed = true;
        landedAt = StateMachine.TimeInState;
        MoveVelocity = Vector2.Zero;
        float radius = Rimebound ? 38f : 30f;
        Vector2 ground = GlobalPosition + new Vector2(0, -Z);
        TryStrikePlayer(GlobalPosition, radius, ScaledDamage * 1.2f, 300f, ToTarget);
        Vfx.Ring(World.Effects, ground, 6f, radius + 14f, Rimebound ? FrostColor : new Color(0.85f, 0.8f, 0.7f, 0.9f), 0.35f, 4f);
        Vfx.Burst(World.Effects, ground, 14, new Color(0.62f, 0.58f, 0.52f), 140f, Vector2.Up, 160f, 0.45f, 380f, 2.2f, 40f);
        World.Camera.Feedback.AddTrauma(0.22f);
        Sfx.Play("hit_heavy", -3f, 0.8f);
        if (Rimebound)
            GroundHazard.Spawn(World, GlobalPosition, 30f, 0.3f, 3f, 6f, FrostColor);
    }

    public override HitResponse ReceiveHit(HitInfo hit)
    {
        if (IsDead)
            return HitResponse.Ignored;
        // A blow to the pile scatters it for good.
        if (StateMachine.Is(EnemyState.Downed))
        {
            hit.Damage = Mathf.Max(hit.Damage, Health + 1f);
            Vfx.Burst(World.Effects, VisualCenter, 18, new Color(0.85f, 0.8f, 0.7f), 160f, hit.Direction, 90f, 0.5f, 380f, 2.2f, 30f);
            return base.ReceiveHit(hit);
        }
        bool lethal = hit.Damage >= Health;
        if (lethal && CanReassemble && hit.DamageType != DamageType.Smashing)
        {
            hit.Damage = Mathf.Max(0f, Health - 1f);
            HitResponse response = base.ReceiveHit(hit);
            if (!IsDead)
                StateMachine.Transition(EnemyState.Downed);
            return response;
        }
        return base.ReceiveHit(hit);
    }

    private void EnterDowned()
    {
        rising = false;
        Solid = false;
        MoveVelocity = Vector2.Zero;
        SetOutline(0f, Colors.White);
        Restart("death", 10f);
        Sfx.Play("break", -6f, 0.7f);
        World.Director.Release(this);
    }

    // The pile shivers and glows faintly while it gathers itself: the window to finish it.
    private void UpdateDowned(float delta)
    {
        Brake(delta, 1200f);
        float t = StateMachine.TimeInState;
        if (!rising)
        {
            float urgency = Mathf.Clamp(t / RISE_AFTER, 0f, 1f);
            SetOutline(urgency * (0.5f + 0.5f * Mathf.Sin(t * (6f + 14f * urgency))), RattleColor);
            if (GD.Randf() < delta * (3f + 9f * urgency))
                Vfx.Burst(World.Effects, GlobalPosition + new Vector2((float)GD.RandRange(-8, 8), -Z - 3f), 1, RattleColor, 20f, Vector2.Up, 40f, 0.5f, -10f, 1.5f, 10f, true);
            if (t >= RISE_AFTER)
            {
                rising = true;
                Restart("rise", 6f / RISE_TIME);
                Sfx.Play("telegraph", -6f, 0.5f);
            }
            return;
        }
        if (t >= RISE_AFTER + RISE_TIME)
        {
            reassembled = true;
            RestoreHealth(MaxHealth * 0.5f);
            Flash(0.3f, RattleColor);
            Vfx.Ring(World.Effects, VisualCenter, 4f, 26f, RattleColor, 0.35f, 2.5f);
            StateMachine.Transition(EnemyState.Chase);
        }
    }

    private void ExitDowned()
    {
        Solid = true;
        SetOutline(0f, Colors.White);
    }
}
