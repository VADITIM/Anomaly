using Godot;

namespace Anomaly;

// Hollowed sentinel wearing scavenged plate. Heavy, barely flinches; its armor yields to Piercing.
// Up close it smashes, chaining more blows the more it is hurt. Kiting it at range draws a shoulder charge.
public partial class Brute : Enemy
{
    private const float SMASH_FRAME_TIME = 0.12f;
    private const float CHARGE_SPEED = 235f;
    private const float CHARGE_DURATION = 0.55f;
    private const float CHARGE_MIN_RANGE = 66f;
    private const float CHARGE_MAX_RANGE = 155f;
    private const float CHARGE_COOLDOWN = 4f;
    private const float CHARGE_TELEGRAPH = 0.75f;

    private bool smashed;
    private int smashCount;
    private bool charging;
    private bool chargeQueued;
    private bool chargeStruck;
    private float chargeCooldown = 1.5f;
    private Vector2 chargeDirection;
    private Vector2 lastPosition;

    public override string DisplayName => "Hollow Brute";
    protected override string SpriteFolder => "2";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Brute.tres";
    protected override float SpriteFeetOffset => 14f;
    protected override float BarHeight => 26f;
    protected override float BaseCameraPriority => 3f;
    protected override Color IchorColor => new(0.45f, 0.15f, 0.55f);
    private int SmashLimit => HealthRatio < 0.35f ? 3 : HealthRatio < 0.6f ? 2 : 1;

    protected override void ConfigureEnemy()
    {
        Radius = 9f;
        HurtRadius = 13f;
        HeightSpan = 30f;
        MoveSpeed = 42f;
        AttackRange = 36f;
        AttackDamage = 32f;
        TelegraphTime = 0.6f;
        RecoverTime = 0.7f;
        AttackCooldown = 0.7f;
        AggroRadius = 165f;
        FlinchResistance = 110f;
        CounterChance = 0.6f;
        Weakness = DamageType.Piercing;
        SoulReward = 90;
        ResidueReward = 60;
        Impact.StaggerDuration = 2.2f;
        Sprite.Scale = Vector2.One * 1.4f;
        Sprite.Offset = new Vector2(0, -SpriteFeetOffset);
        EntityMaterial.SetShaderParameter(StringNames.Of("tint_strength"), 0.55f);
        EntityMaterial.SetShaderParameter(StringNames.Of("tint_color"), new Color(0.55f, 0.45f, 0.85f));
    }

    protected override void TickEntity(float delta)
    {
        if (chargeCooldown > 0f)
            chargeCooldown -= delta;
        base.TickEntity(delta);
    }

    protected override void ChaseBehaviour(float delta)
    {
        SteerToward(FlankApproach(), MoveSpeed, delta);
        Play("Walk", 6f);
    }

    protected override bool WantsToAttack()
    {
        float distance = ToTarget.Length();
        chargeQueued = false;
        if (distance <= AttackRange)
            return true;
        if (chargeCooldown <= 0f && distance >= CHARGE_MIN_RANGE && distance <= CHARGE_MAX_RANGE && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z))
        {
            chargeQueued = true;
            return true;
        }
        return false;
    }

    protected override float TelegraphFor() => chargeQueued ? CHARGE_TELEGRAPH : TelegraphTime;

    protected override void OnTelegraphStart()
    {
        smashCount = 0;
        charging = chargeQueued;
        Restart("Attack", charging ? 2f : 4.5f);
        Sfx.Play("telegraph", -4f, 0.55f);
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        if (charging)
        {
            Body.Scale = new Vector2(1f + 0.12f * ratio, 1f - 0.1f * ratio);
            BodyOffset = -ToTarget.Normalized() * 3f * ratio;
            if (GD.Randf() < 0.4f)
                Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 1, new Color(0.62f, 0.58f, 0.52f, 0.7f), 30f, -ToTarget, 50f, 0.3f, 0f, 1.6f, 30f);
            return;
        }
        Body.Scale = new Vector2(1f - 0.08f * ratio, 1f + 0.1f * ratio);
    }

    protected override void OnAttackStart()
    {
        BodyOffset = Vector2.Zero;
        if (charging)
        {
            chargeCooldown = CHARGE_COOLDOWN;
            chargeStruck = false;
            Vector2 aim = PredictTarget(ToTarget.Length() / CHARGE_SPEED) - GlobalPosition;
            chargeDirection = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
            Face(chargeDirection);
            lastPosition = GlobalPosition;
            Restart("Walk", 14f, true);
            Sfx.Play("enemy_attack", 0f, 0.5f);
            World.Camera.Feedback.AddTrauma(0.12f);
            return;
        }
        smashed = false;
        Restart("Attack", 1f / SMASH_FRAME_TIME);
        Sfx.Play("enemy_attack", 0f, 0.6f);
        MoveVelocity = FacingDirection * 120f;
    }

    protected override bool UpdateAttack(float delta) => charging ? UpdateCharge(delta) : UpdateSmash(delta);

    private bool UpdateCharge(float delta)
    {
        float t = StateMachine.TimeInState;
        bool blocked = t > 0.08f && GlobalPosition.DistanceSquaredTo(lastPosition) < 0.25f;
        lastPosition = GlobalPosition;
        if (t < CHARGE_DURATION && !blocked)
        {
            MoveVelocity = chargeDirection * CHARGE_SPEED * Momentum;
            if (!chargeStruck)
                chargeStruck = TryStrikePlayer(GlobalPosition + chargeDirection * 10f, 14f, ScaledDamage * 0.8f, 380f, chargeDirection);
            if ((int)(t * 60f) % 2 == 0)
                Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 2, new Color(0.62f, 0.58f, 0.52f, 0.7f), 40f, -chargeDirection, 60f, 0.35f, 0f, 1.8f, 40f);
            return false;
        }
        if (blocked && t < CHARGE_DURATION)
        {
            // Slamming into a wall rattles it: a free opening.
            World.Camera.Feedback.AddTrauma(0.2f);
            Sfx.Play("hit_heavy", -4f, 0.55f);
            Body.Scale = new Vector2(1.3f, 0.75f);
            CooldownTimer = AttackCooldown * 1.5f;
            t = CHARGE_DURATION;
        }
        Brake(delta, 900f);
        return t >= CHARGE_DURATION + 0.25f;
    }

    private bool UpdateSmash(float delta)
    {
        Brake(delta, 700f);
        float t = StateMachine.TimeInState;
        if (!smashed && t >= SMASH_FRAME_TIME * 3f)
        {
            smashed = true;
            smashCount++;
            Vector2 center = GlobalPosition + FacingDirection * 20f;
            TryStrikePlayer(center, 30f, ScaledDamage, 340f, FacingDirection);
            Vfx.Ring(World.Effects, center + new Vector2(0, -Z), 6f, 48f, new Color(0.85f, 0.7f, 1f, 0.9f), 0.4f, 5f);
            Vfx.Burst(World.Effects, center + new Vector2(0, -Z), 18, new Color(0.62f, 0.58f, 0.52f), 160f, Vector2.Up, 160f, 0.5f, 380f, 2.4f, 40f);
            Vfx.ImpactStar(World.Effects, center + new Vector2(0, -Z - 4f), 20f, new Color(0.9f, 0.8f, 1f));
            World.Camera.Feedback.AddTrauma(0.32f);
            Sfx.Play("hit_heavy", -1f, 0.6f);
            Body.Scale = new Vector2(1.25f, 0.8f);
        }
        if (smashed && t >= SMASH_FRAME_TIME * 6f && smashCount < SmashLimit && ToTarget.Length() < AttackRange * 1.6f)
        {
            Face(PredictTarget(0.2f) - GlobalPosition);
            smashed = false;
            SignalDodge();
            StateMachine.Transition(EnemyState.Attack);
            return false;
        }
        return t >= SMASH_FRAME_TIME * 6f + 0.1f;
    }

    protected override void OnAttackEnd() => BodyOffset = Vector2.Zero;
}
