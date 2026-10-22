using Godot;

namespace Anomaly;

// Airborne carrion feeder: spits corrosive residue where the hunter will be, cycling between a low pass the Scythe
// can reach from the ground and a high one that only a hovering combo reaches.
// Thin chitin yields to Piercing. Wounded or stronger Wasps spit a three-shot fan and keep further away.
public partial class Wasp : Enemy
{
    private const float LOW_ALTITUDE = 14f;
    private const float HIGH_ALTITUDE = 36f;
    private const float ALTITUDE_HOLD = 2.6f;
    private const float ALTITUDE_SHIFT = 0.9f;
    private const float PREFERRED_DISTANCE = 92f;
    private const float PROJECTILE_SPEED = 125f;
    private const float FAN_SPREAD = 0.24f;

    private float altitudeClock;
    private float bobTime;
    private float groundZ;
    private bool fired;
    private float strafeSign = 1f;

    public override string DisplayName => "Carrion Wasp";
    public override bool IsRanged => true;
    protected override string SpriteFolder => "Bee";
    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Wasp.tres";
    protected override float SpriteFeetOffset => 13f;
    protected override float BarHeight => 14f;
    protected override Color IchorColor => new(0.95f, 0.7f, 0.2f);
    protected override float StandoffDistance => PreferredDistance + 30f;
    private float PreferredDistance => HealthRatio < 0.4f ? PREFERRED_DISTANCE + 30f : PREFERRED_DISTANCE;
    private bool FiresFan => HealthRatio < 0.6f || EnemyLevel >= 2;

    protected override void ConfigureEnemy()
    {
        Radius = 5f;
        HurtRadius = 9f;
        HeightSpan = 14f;
        IsFlying = true;
        CarefulMovement = false;
        MoveSpeed = 60f;
        Acceleration = 320f;
        AttackRange = 140f;
        AttackDamage = 13f;
        TelegraphTime = 0.45f;
        RecoverTime = 0.6f;
        AttackCooldown = 1.1f;
        AggroRadius = 175f;
        LeashRadius = 380f;
        EvadeChance = 0.45f;
        EvadeSpeed = 180f;
        Weakness = DamageType.Piercing;
        SoulReward = 14;
        ResidueReward = 12;
        altitudeClock = GD.Randf() * 2f * (ALTITUDE_HOLD + ALTITUDE_SHIFT);
        groundZ = SupportZ;
        Z = groundZ + CycleAltitude();
        bobTime = GD.Randf() * 10f;
        strafeSign = GD.Randf() < 0.5f ? 1f : -1f;
    }

    protected override void TickEntity(float delta)
    {
        bobTime += delta;
        altitudeClock += delta;
        if (!IsDead)
        {
            // Over a pit the ground reference is lost; hold the last known one.
            if (!float.IsNegativeInfinity(SupportZ))
                groundZ = SupportZ;
            Z = Mathf.Lerp(Z, groundZ + CycleAltitude() + Mathf.Sin(bobTime * 3.2f) * 2.5f, 1f - Mathf.Exp(-6f * delta));
        }
        base.TickEntity(delta);
    }

    // Hold low, climb, hold high, dive: a smoothstepped square wave over the ground beneath.
    private float CycleAltitude()
    {
        float period = 2f * (ALTITUDE_HOLD + ALTITUDE_SHIFT);
        float t = Mathf.PosMod(altitudeClock, period);
        float high;
        if (t < ALTITUDE_HOLD)
            high = 0f;
        else if (t < ALTITUDE_HOLD + ALTITUDE_SHIFT)
            high = Mathf.SmoothStep(0f, 1f, (t - ALTITUDE_HOLD) / ALTITUDE_SHIFT);
        else if (t < 2f * ALTITUDE_HOLD + ALTITUDE_SHIFT)
            high = 1f;
        else
            high = 1f - Mathf.SmoothStep(0f, 1f, (t - 2f * ALTITUDE_HOLD - ALTITUDE_SHIFT) / ALTITUDE_SHIFT);
        return Mathf.Lerp(LOW_ALTITUDE, HIGH_ALTITUDE, high);
    }

    protected override void ChaseBehaviour(float delta) => Strafe(PreferredDistance, delta);

    protected override void HoldStandoff(float delta) => Strafe(StandoffDistance, delta);

    private void Strafe(float preferred, float delta)
    {
        Vector2 offset = ToTarget;
        float distance = offset.Length();
        Vector2 radial = distance > 0.1f ? offset / distance : Vector2.Down;
        Vector2 desire = radial.Orthogonal() * strafeSign * 0.6f;
        if (distance > preferred + 20f)
            desire += radial;
        else if (distance < preferred - 25f)
            desire -= radial * 1.2f;
        if (GD.Randf() < delta * 0.5f)
            strafeSign = -strafeSign;
        SteerDirection(desire.Normalized(), MoveSpeed, delta);
        Face(offset);
        Play("Walk", 14f);
    }

    protected override bool WantsToAttack() => ToTarget.Length() <= AttackRange && World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z);

    protected override void OnTelegraphStart() => Play("Walk", 22f);

    protected override void UpdateTelegraph(float delta, float ratio)
    {
        Face(ToTarget);
        if (GD.Randf() < 0.5f)
            Vfx.Burst(World.Effects, VisualCenter, 1, new Color(0.7f, 1f, 0.3f), 25f, -ToTarget, 30f, 0.25f, 0f, 1.5f, 10f, true);
    }

    protected override void OnAttackStart()
    {
        fired = false;
    }

    protected override bool UpdateAttack(float delta)
    {
        if (!fired)
        {
            fired = true;
            Vector2 aim = PredictTarget(ToTarget.Length() / PROJECTILE_SPEED) - GlobalPosition;
            Vector2 direction = aim.LengthSquared() > 1f ? aim.Normalized() : ToTarget.Normalized();
            Spit(direction);
            if (FiresFan)
            {
                Spit(direction.Rotated(FAN_SPREAD));
                Spit(direction.Rotated(-FAN_SPREAD));
            }
            MoveVelocity = -direction * 60f;
            Sfx.Play("spit", -4f, 0.85f);
        }
        Brake(delta, 300f);
        return StateMachine.TimeInState > 0.2f;
    }

    private void Spit(Vector2 direction)
    {
        var projectile = new Projectile();
        // Aimed down (or up) at Kio's chest, so the high pass still threatens the ground.
        float flightTime = Mathf.Max(ToTarget.Length(), 1f) / PROJECTILE_SPEED;
        float climb = (Target.Z + 10f - (Z + 4f)) / flightTime;
        projectile.Setup(this, GlobalPosition + direction * 6f, Z + 4f, direction, PROJECTILE_SPEED, ScaledDamage, climb);
        World.Actors.AddChild(projectile);
    }

    protected override void OnDeath(HitInfo hit)
    {
        IsFlying = false;
        base.OnDeath(hit);
    }
}
