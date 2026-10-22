using Godot;

namespace Anomaly;

// Gelatinous husk: bounces between hops, then leaps onto where the hunter is going. Soft mass yields to Smashing.
// A wounded Rotling chains a second leap straight off the landing.
public partial class Rotling : Enemy
{
    private const float HOP_INTERVAL = 0.5f;
    private const float LEAP_VELOCITY = 330f;
    private const float CHAIN_DELAY = 0.14f;

    private float hopTimer;
    private bool leapLanded;
    private bool struck;
    private bool chained;
    private float landedAt;

    public override string DisplayName => "Rotling";
    protected override string SpriteFolder => "1";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Rotling.tres";
    protected override float SpriteFeetOffset => 13f;
    protected override float BarHeight => 14f;
    protected override Color IchorColor => new(0.62f, 0.85f, 0.28f);

    protected override void ConfigureEnemy()
    {
        Radius = 6f;
        HurtRadius = 9f;
        HeightSpan = 14f;
        MoveSpeed = 52f;
        AttackRange = 66f;
        AttackDamage = 15f;
        TelegraphTime = 0.42f;
        RecoverTime = 0.45f;
        AttackCooldown = 0.8f;
        AggroRadius = 150f;
        EvadeChance = 0.12f;
        EvadeSpeed = 150f;
        Weakness = DamageType.Smashing;
        SoulReward = 12;
        ResidueReward = 10;
        hopTimer = (float)GD.RandRange(0.0, HOP_INTERVAL);
    }

    protected override void ChaseBehaviour(float delta) => HopToward(FlankApproach(), delta, 1.9f);

    protected override void HoldStandoff(float delta)
    {
        HopToward(StandoffPoint(), delta, 1.3f);
        Face(ToTarget);
    }

    private void HopToward(Vector2 destination, float delta, float speedScale)
    {
        hopTimer -= delta;
        if (IsGrounded)
        {
            Brake(delta, 500f);
            Vector2 offset = destination - GlobalPosition;
            if (hopTimer <= 0f && offset.Length() > 6f)
            {
                hopTimer = HOP_INTERVAL * (float)GD.RandRange(0.85, 1.15);
                Vector2 direction = offset.Normalized().Rotated((float)GD.RandRange(-0.35, 0.35));
                if (IsPathClear(direction))
                {
                    Launch(150f);
                    MoveVelocity = direction * MoveSpeed * speedScale;
                    Face(direction);
                    Body.Scale = new Vector2(0.8f, 1.25f);
                    Sfx.Play("slime_hop", -14f, 0.85f);
                }
            }
        }
        Play("Walk", 9f);
    }

    protected override void OnTelegraphStart()
    {
        Restart("Special", 3f);
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        Body.Scale = new Vector2(1f + 0.3f * ratio, 1f - 0.3f * ratio);
    }

    protected override void OnAttackStart()
    {
        chained = HealthRatio > 0.6f;
        StartLeap();
    }

    private void StartLeap()
    {
        leapLanded = false;
        struck = false;
        float airTime = 2f * LEAP_VELOCITY / GRAVITY;
        Vector2 offset = PredictTarget(airTime) - GlobalPosition;
        float distance = Mathf.Min(offset.Length(), AttackRange + 14f);
        MoveVelocity = offset.Normalized() * (distance / airTime);
        Face(offset);
        Launch(LEAP_VELOCITY);
        Body.Scale = new Vector2(0.7f, 1.4f);
        Restart("Special", 6f);
        Sfx.Play("slime_hop", -6f, 0.7f);
    }

    protected override bool UpdateAttack(float delta)
    {
        if (!leapLanded)
        {
            if (!struck && ZVelocity < 0f && Z - SupportZ < 10f)
                struck = TryStrikePlayer(GlobalPosition, 14f, ScaledDamage, 180f, MoveVelocity);
            return false;
        }
        Brake(delta, 1200f);
        float sinceLanding = StateMachine.TimeInState - landedAt;
        if (!chained && sinceLanding >= CHAIN_DELAY && TargetAvailable && ToTarget.Length() <= AttackRange)
        {
            chained = true;
            SignalDodge();
            StartLeap();
            return false;
        }
        return sinceLanding > 0.12f;
    }

    protected override void OnLanded(float impactSpeed)
    {
        Body.Scale = new Vector2(1.4f, 0.65f);
        if (!StateMachine.Is(EnemyState.Attack) || leapLanded)
            return;
        leapLanded = true;
        landedAt = StateMachine.TimeInState;
        if (!struck)
            struck = TryStrikePlayer(GlobalPosition, 18f, ScaledDamage, 200f, MoveVelocity);
        MoveVelocity *= 0.2f;
        Vfx.Ring(World.Effects, GlobalPosition + new Vector2(0, -Z), 4f, 24f, new Color(0.7f, 0.9f, 0.35f, 0.8f), 0.3f, 2.5f);
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 8, IchorColor, 70f, Vector2.Up, 120f, 0.35f, 200f, 1.8f, 40f);
        Sfx.Play("land", -4f, 0.9f);
    }
}
