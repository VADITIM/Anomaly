using Godot;

namespace Anomaly;

// Lean predator that circles before committing to a lunge aimed where the hunter is heading. Exposed hide yields to
// Slashing. It often snaps back into a second lunge, and always does once wounded.
public partial class Hound : Enemy
{
    private const float LUNGE_SPEED = 300f;
    private const float LUNGE_DURATION = 0.26f;
    private const float RELUNGE_WINDUP = 0.16f;
    private const float ORBIT_DISTANCE = 58f;

    private Vector2 lungeDirection;
    private bool struck;
    private bool secondLunge;
    private bool secondStarted;
    private bool relungeCued;
    private float lungeStart;
    private float orbitSign = 1f;
    private float orbitSwitchTimer;

    public override string DisplayName => "Husk Hound";
    protected override string SpriteFolder => "3";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Hound.tres";
    protected override float SpriteFeetOffset => 14f;
    protected override float BarHeight => 16f;
    protected override Color IchorColor => new(0.6f, 0.1f, 0.18f);
    protected override float StandoffDistance => ORBIT_DISTANCE + 24f;

    protected override void ConfigureEnemy()
    {
        Radius = 6f;
        HurtRadius = 9f;
        HeightSpan = 16f;
        MoveSpeed = 92f;
        Acceleration = 800f;
        AttackRange = 82f;
        AttackDamage = 18f;
        TelegraphTime = 0.34f;
        RecoverTime = 0.45f;
        AttackCooldown = 0.75f;
        AggroRadius = 175f;
        EvadeChance = 0.35f;
        EvadeSpeed = 200f;
        CounterChance = 0.45f;
        Weakness = DamageType.Slashing;
        SoulReward = 18;
        ResidueReward = 14;
        orbitSign = GD.Randf() < 0.5f ? -1f : 1f;
    }

    protected override void ChaseBehaviour(float delta)
    {
        Vector2 offset = ToTarget;
        float distance = offset.Length();
        if (CooldownTimer > 0f && distance < ORBIT_DISTANCE * 1.6f)
            Orbit(offset, ORBIT_DISTANCE, delta);
        else
            SteerToward(FlankApproach(), MoveSpeed, delta);
        Play("Walk", 12f);
    }

    protected override void HoldStandoff(float delta)
    {
        Orbit(ToTarget, StandoffDistance, delta);
        Play("Walk", 12f);
    }

    private void Orbit(Vector2 offset, float radius, float delta)
    {
        orbitSwitchTimer -= delta;
        if (orbitSwitchTimer <= 0f)
        {
            orbitSwitchTimer = (float)GD.RandRange(0.9, 2.0);
            orbitSign = -orbitSign;
        }
        float distance = offset.Length();
        Vector2 radial = offset.Normalized();
        Vector2 tangent = radial.Orthogonal() * orbitSign;
        float correction = (distance - radius) / radius;
        SteerDirection((tangent + radial * correction * 1.5f).Normalized(), MoveSpeed * 0.8f, delta);
        Face(offset);
    }

    protected override bool WantsToAttack()
    {
        float distance = ToTarget.Length();
        return distance <= AttackRange && distance > 20f && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z);
    }

    protected override void OnTelegraphStart()
    {
        Restart("Attack", 0f);
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        Body.Scale = new Vector2(1f + 0.2f * ratio, 1f - 0.2f * ratio);
        BodyOffset = new Vector2(Mathf.Sin(ratio * 60f) * 0.6f, 0f);
    }

    protected override void OnAttackStart()
    {
        secondLunge = HealthRatio < 0.6f || GD.Randf() < 0.35f;
        secondStarted = false;
        relungeCued = false;
        CarefulMovement = true;
        BeginLunge(0f);
    }

    private void BeginLunge(float at)
    {
        BodyOffset = Vector2.Zero;
        lungeStart = at;
        float distance = ToTarget.Length();
        Vector2 aim = PredictTarget(distance / LUNGE_SPEED) - GlobalPosition;
        lungeDirection = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
        Face(lungeDirection);
        struck = false;
        Momentum = 1f;
        Restart("Attack", 18f);
        Sfx.Play("enemy_attack", -4f, 0.9f);
        Body.Scale = new Vector2(1.3f, 0.8f);
    }

    protected override bool UpdateAttack(float delta)
    {
        float t = StateMachine.TimeInState - lungeStart;
        if (t < LUNGE_DURATION)
        {
            MoveVelocity = lungeDirection * LUNGE_SPEED * Momentum;
            if (!struck)
                struck = TryStrikePlayer(GlobalPosition + lungeDirection * 8f, 10f, ScaledDamage, 230f, lungeDirection);
            if ((int)(t * 60f) % 3 == 0)
                Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 1, new Color(0.7f, 0.68f, 0.6f, 0.6f), 20f, -lungeDirection, 40f, 0.3f, 0f, 1.5f, 30f);
            return false;
        }
        Brake(delta, 1100f);
        if (secondLunge && !secondStarted && TargetAvailable && ToTarget.Length() < AttackRange)
        {
            // A short crouch flash marks the snap-back so the second lunge stays readable.
            Face(ToTarget);
            SetOutline(0.8f, new Color(1f, 0.15f, 0.25f));
            if (!relungeCued)
            {
                relungeCued = true;
                SignalDodge();
            }
            if (t >= LUNGE_DURATION + RELUNGE_WINDUP)
            {
                secondStarted = true;
                SetOutline(0f, Colors.White);
                BeginLunge(StateMachine.TimeInState);
            }
            return false;
        }
        return t >= LUNGE_DURATION + 0.18f;
    }

    protected override void OnAttackEnd()
    {
        BodyOffset = Vector2.Zero;
        SetOutline(0f, Colors.White);
    }
}
