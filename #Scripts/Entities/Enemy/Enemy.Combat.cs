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

        float calculatedDamage = weapon.ApplyDamage(this);

        bool weaknessExploit = weapon.AttackType switch
        {
            WeaponArc.WeaponAttackType.Slashing => this.WeaknessType == EnemyWeaknessType.Slashing,
            WeaponArc.WeaponAttackType.Piercing => this.WeaknessType == EnemyWeaknessType.Piercing,
            WeaponArc.WeaponAttackType.Smashing => this.WeaknessType == EnemyWeaknessType.Smashing,
            _ => false
        };

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
        Player.Instance?.ResourceManager?.AddVesselCharge(calculatedDamage, Player.Instance?.Stats?.GetCurrentMax("Health") ?? 1f);
        StateMachine?.NotifyDamageTaken(calculatedDamage);

        if (GetHealth() <= 0)
        {
            StateMachine?.RequestDeath();
            return;
        }

        hitTimer = HIT_WINDOW;

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

        float appliedDuration = duration > 0f
            ? duration
            : (TenacityBehavior?.DefaultKnockbackDuration ?? DefaultKnockbackDuration);

        float subtleForce = force > 0f ? force : 30f;

        if (IsInStaggerWindow)
        {
            float staggeredForce = force > 0f ? force * 2.5f : 75f;
            float weaknessMultiplier = 1f;

            base.TakeKnockback(sourcePosition, staggeredForce * weaknessMultiplier, appliedDuration);
            return;
        }

        float defaultMultiplier = 1f;

        base.TakeKnockback(sourcePosition, subtleForce * defaultMultiplier, appliedDuration);
    }
}
