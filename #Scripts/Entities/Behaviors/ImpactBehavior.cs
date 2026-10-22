using Godot;

namespace Anomaly;

public readonly record struct ImpactResult(float AppliedForce, bool TenacityShattered);

// Incoming Hit -> Calculate Force -> Apply Weight -> Resolve Tenacity -> Determine Stagger -> Apply Knockback.
public sealed class ImpactBehavior : IEntityBehavior
{
    private const float TENACITY_RECOVERY_DELAY = 2.2f;
    private const float TENACITY_RECOVERY_RATE = 0.35f;

    private readonly Entity owner;
    private float recoveryDelay;

    public Vector2 KnockbackVelocity { get; private set; }
    public float TenacityMeter { get; private set; }
    public float StaggerTimeRemaining { get; private set; }
    public float StaggerDuration { get; set; } = 1.7f;
    public bool IsStaggered => StaggerTimeRemaining > 0f;
    public float TenacityRatio => owner.Stats.UseTenacity && owner.Stats.Tenacity > 0f ? TenacityMeter / owner.Stats.Tenacity : 0f;

    public ImpactBehavior(Entity entity)
    {
        owner = entity;
    }

    public ImpactResult Resolve(HitInfo hit)
    {
        float force = Push.KnockbackSpeed(hit.Knockback, owner);
        if (owner.Stats.UseKnockback && force > 0f)
        {
            Vector2 impulse = hit.Direction.Normalized() * force;
            KnockbackVelocity = impulse.Length() > KnockbackVelocity.Length() ? impulse : KnockbackVelocity + impulse * 0.5f;
            if (hit.Lift > 0f && owner.IsGrounded && !owner.IsFlying)
                owner.Launch(Push.KnockbackSpeed(hit.Lift, owner));
        }

        bool shattered = false;
        if (owner.Stats.UseTenacity && !IsStaggered)
        {
            TenacityMeter += hit.TenacityDamage;
            recoveryDelay = TENACITY_RECOVERY_DELAY;
            if (TenacityMeter >= owner.Stats.Tenacity)
            {
                TenacityMeter = owner.Stats.Tenacity;
                StaggerTimeRemaining = StaggerDuration;
                shattered = true;
            }
        }
        return new ImpactResult(force, shattered);
    }

    public void AddKnockback(Vector2 impulse)
    {
        if (owner.Stats.UseKnockback)
            KnockbackVelocity += impulse;
    }

    public void ClearKnockback() => KnockbackVelocity = Vector2.Zero;

    public void Process(float delta)
    {
        KnockbackVelocity = Push.Decay(KnockbackVelocity, Push.KNOCKBACK_DECAY, delta);
        if (KnockbackVelocity.LengthSquared() < 1f)
            KnockbackVelocity = Vector2.Zero;

        if (IsStaggered)
        {
            StaggerTimeRemaining -= delta;
            if (StaggerTimeRemaining <= 0f)
            {
                StaggerTimeRemaining = 0f;
                TenacityMeter = 0f;
            }
            return;
        }

        if (recoveryDelay > 0f)
            recoveryDelay -= delta;
        else if (TenacityMeter > 0f)
            TenacityMeter = Mathf.Max(0f, TenacityMeter - owner.Stats.Tenacity * TENACITY_RECOVERY_RATE * delta);
    }
}
