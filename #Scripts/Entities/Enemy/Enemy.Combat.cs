using Godot;

public enum EnemyAttackPhase
{
    None,
    WindUp,
    Active,
    Recovery
}

public abstract partial class Enemy
{
    // Non-weapon damage (base.TakeDamage) resolves death through this hook so the
    // state machine and reward flow fire the same way as weapon kills.
    protected override void OnDeath(Vector2 sourcePosition)
    {
        StateMachine?.RequestDeath();
    }

    public override void TakeDamage(float damage, Vector2 sourcePosition, WeaponArc weapon = null)
    {
        if (IsDead)
            return;

        if (weapon == null)
        {
            base.TakeDamage(damage, sourcePosition, weapon);
            return;
        }

        GetBehavior<CommonDamageFlash>()?.Flash();
        MarkCameraFocus();

        // The passed damage is the arc's raw value; the weapon owns the full
        // resolution (combo/heavy, weakness, penetration vs. armor).
        float calculatedDamage = weapon.ApplyDamage(this);

        bool weaknessExploit = IsWeakTo(weapon.AttackType);

        Camera camera = GetViewport().GetCamera2D() as Camera;
        if (weaknessExploit)
            CameraFeedback.TriggerWeaknessShake(camera);
        else
            CameraFeedback.TriggerNormalShake(camera);

        SetHealth(GetHealth() - calculatedDamage);
        DamageNumber.Spawn(
            this,
            calculatedDamage,
            weaknessExploit ? DamageNumberStyle.Weakness : DamageNumberStyle.Standard,
            this
        );
        Player?.ResourceManager?.AddVesselCharge(calculatedDamage, Player?.Stats?.GetCurrentMax(StatType.Health) ?? 1f);
        StateMachine?.NotifyDamageTaken(calculatedDamage);

        if (GetHealth() <= 0)
        {
            StateMachine?.RequestDeath();
            return;
        }

        _hitTimer = HitWindow;

        bool staggerTriggered = TenacitySystem != null && TenacitySystem.ProcessTenacitySystem(sourcePosition, weapon);

        if (!staggerTriggered)
            TakeKnockback(sourcePosition, weapon.Knockback, 0.1f);

        if (staggerTriggered)
        {
            CameraFeedback.TriggerTenacityBreakShake(camera);
            if (TenacityCooldownCue != null)
                TenacityCooldownCue.Visible = true;
        }
    }

    public override void TakeKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (!CanBeKnockedBack)
            return;

        float appliedDuration = duration > 0f ? duration : DefaultKnockbackDuration;

        float subtleForce = force > 0f ? force : 30f;

        if (IsInStaggerWindow)
        {
            float staggeredForce = force > 0f ? force * 2.5f : 75f;
            base.TakeKnockback(sourcePosition, staggeredForce, appliedDuration);
            return;
        }

        base.TakeKnockback(sourcePosition, subtleForce, appliedDuration);
    }
}
