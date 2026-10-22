using Godot;

namespace Anomaly;

// A shade the Fractures shed: half there while it stalks, it folds out of sight and steps back in behind Kio, then runs
// a spear thrust straight through. The step-in costs it a moment fully solid (smoke, red edge), and after the thrust it is
// left exposed. Shadow yields to Slashing.
public partial class Shadeflit : SheetEnemy
{
    private const float STALK_DISTANCE = 78f;
    private const float BLINK_DISTANCE = 54f;
    private const float THRUST_SPEED = 290f;
    private const float THRUST_TIME = 0.3f;
    private const float STALK_ALPHA = 0.45f;
    private static readonly Color ShadeColor = new(0.25f, 0.95f, 0.6f);
    private static readonly Color SmokeColor = new(0.06f, 0.04f, 0.1f, 0.85f);

    private float alpha = STALK_ALPHA;
    private float circleSign = 1f;
    private bool struck;
    private Vector2 thrustDirection;

    public override string DisplayName => "Shadeflit";
    protected override string SpriteFolder => "Shadeflit";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Shadeflit.tres";
    protected override Vector2I FrameSize => new(96, 80);
    protected override float FeetInFrame => 24f;
    protected override float SpriteScale => 0.75f;
    protected override float SpriteFeetOffset => 16f;
    protected override float BarHeight => 18f;
    protected override float BaseCameraPriority => 1.6f;
    protected override Color IchorColor => new(0.12f, 0.08f, 0.18f);

    protected override void ConfigureSheet()
    {
        Define("idle", "idle", 6, 8f);
        Define("move", "run", 6, 12f);
        Define("thrust", "run", 6, 22f);
        Define("turn", "turn", 1, 1f);
        Radius = 6f;
        HurtRadius = 9f;
        HeightSpan = 26f;
        MoveSpeed = 64f;
        Acceleration = 600f;
        AttackRange = 120f;
        AttackDamage = 17f;
        TelegraphTime = 0.55f;
        RecoverTime = 0.85f;
        AttackCooldown = 1.6f;
        AggroRadius = 165f;
        LeashRadius = 360f;
        EvadeChance = 0.3f;
        EvadeSpeed = 200f;
        CounterChance = 0.25f;
        Weakness = DamageType.Slashing;
        SoulReward = 26;
        ResidueReward = 18;
        circleSign = GD.Randf() < 0.5f ? 1f : -1f;
    }

    protected override void TickEntity(float delta)
    {
        base.TickEntity(delta);
        bool solid = StateMachine.Current is EnemyState.Telegraph or EnemyState.Attack or EnemyState.Recover or EnemyState.Flinch or EnemyState.Staggered or EnemyState.Dead;
        float shimmer = solid ? 1f : STALK_ALPHA + 0.12f * Mathf.Sin(StateMachine.TimeInState * 5f);
        alpha = Mathf.MoveToward(alpha, shimmer, delta * 4f);
        if (!StateMachine.Is(EnemyState.Dead))
            Sprite.Modulate = new Color(1f, 1f, 1f, alpha);
        if (!solid && GD.Randf() < delta * 6f)
            Vfx.Burst(World.Effects, VisualCenter + new Vector2((float)GD.RandRange(-5, 5), 4f), 1, SmokeColor, 12f, Vector2.Up, 40f, 0.7f, -15f, 2.2f, 8f);
    }

    // Circles at the edge of the hunter's reach, drifting in and out like smoke.
    protected override void ChaseBehaviour(float delta)
    {
        Vector2 offset = ToTarget;
        float distance = offset.Length();
        Vector2 radial = distance > 0.1f ? offset / distance : Vector2.Down;
        Vector2 desire = radial.Orthogonal() * circleSign * 0.7f + radial * Mathf.Clamp((distance - STALK_DISTANCE) / 25f, -1f, 1f);
        if (GD.Randf() < delta * 0.35f)
            circleSign = -circleSign;
        SteerDirection(desire.Normalized(), MoveSpeed, delta);
        Face(offset);
        Play("Walk", 12f);
    }

    protected override bool WantsToAttack() => ToTarget.Length() <= AttackRange && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z);

    // Folds away and steps back in behind Kio. The smoke where it vanished and where it lands is the tell.
    protected override void OnTelegraphStart()
    {
        Vector2 from = GlobalPosition;
        if (TryFindBlinkSpot(out Vector2 spot))
        {
            Puff(from);
            PlaceAt(spot);
            Puff(spot);
            Sfx.Play("dash", -8f, 0.6f);
        }
        Face(ToTarget);
        alpha = 0.2f;
        Restart("idle", 4f);
    }

    private bool TryFindBlinkSpot(out Vector2 spot)
    {
        Vector2 behind = Target.AimDirection.LengthSquared() > 0.01f ? -Target.AimDirection.Normalized() : Vector2.FromAngle(FlankAngle);
        foreach (float angle in new[] { 0f, 0.7f, -0.7f, 1.4f, -1.4f })
        {
            Vector2 candidate = Target.GlobalPosition + behind.Rotated(angle) * BLINK_DISTANCE;
            float surface = World.Grid.SurfaceZAt(candidate);
            if (float.IsNegativeInfinity(surface) || Mathf.Abs(surface - Target.SupportZ) > 1f || World.Grid.Blocks(candidate, Radius + 2f, surface))
                continue;
            if (!World.Grid.HasLineOfSight(candidate, Target.GlobalPosition, surface))
                continue;
            spot = candidate;
            return true;
        }
        spot = GlobalPosition;
        return false;
    }

    private void Puff(Vector2 at)
    {
        float surface = World.Grid.SurfaceZAt(at);
        Vector2 screen = at + new Vector2(0, -(float.IsNegativeInfinity(surface) ? 0f : surface) - 12f);
        Vfx.Burst(World.Effects, screen, 16, SmokeColor, 70f, Vector2.Up, 180f, 0.55f, -30f, 3f, 40f);
        Vfx.Burst(World.Effects, screen, 6, ShadeColor, 50f, Vector2.Up, 180f, 0.4f, 0f, 1.5f, 30f, true);
    }

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        BodyOffset = -ToTarget.Normalized() * 3f * ratio;
    }

    protected override void OnAttackStart()
    {
        struck = false;
        BodyOffset = Vector2.Zero;
        Vector2 aim = PredictTarget(0.12f) - GlobalPosition;
        thrustDirection = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
        Face(thrustDirection);
        Restart("thrust", 22f, true);
        Sfx.Play("enemy_attack", -4f, 1.3f);
    }

    protected override bool UpdateAttack(float delta)
    {
        float t = StateMachine.TimeInState;
        if (t < THRUST_TIME)
        {
            MoveVelocity = thrustDirection * THRUST_SPEED * Momentum;
            if (!struck)
                struck = TryStrikePlayer(GlobalPosition + thrustDirection * 12f, 11f, ScaledDamage, 220f, thrustDirection);
            if ((int)(t * 60f) % 3 == 0)
                Vfx.AfterImage(World.Actors, this, Sprite, Z, new Color(ShadeColor, 0.35f), 0.22f);
            return false;
        }
        Brake(delta, 1100f);
        Play("idle", 6f);
        return t >= THRUST_TIME + 0.2f;
    }

    protected override void OnAttackEnd() => BodyOffset = Vector2.Zero;
}
