using Godot;

public abstract partial class Enemy
{
    public override void TakeDamage(WeaponArc weapon, Node2D damageSource)
    {
        if (IsDead || weapon == null)
            return;

        Vector2 sourcePosition = damageSource?.GlobalPosition ?? GlobalPosition;

        PlayAnimation("Take_Damage_Down");
        MarkCameraFocus();

        Camera camera = GetViewport().GetCamera2D() as Camera;
        camera?.ShakeCamera(0.5f);

        float calculatedDamage = weapon.ApplyDamage(this);
        // weapon attack types removed — no weakness exploitation by attack type
        bool weaknessExploit = false;

        SetHealth(GetHealth() - calculatedDamage);
        SpawnDamageNumber(
            calculatedDamage,
            TenacitySystem?.IsInStaggerWindow ?? false
                ? DamageNumberStyle.Staggered
                : weaknessExploit
                    ? DamageNumberStyle.Weakness
                    : DamageNumberStyle.Standard
        );

        StateMachine?.NotifyDamageTaken(calculatedDamage);

        if (GetHealth() <= 0)
        {
            StateMachine?.RequestDeath();
            return;
        }

        hitTimer = HIT_WINDOW;

        bool staggerTriggered = TenacitySystem != null && TenacitySystem.ProcessTenacitySystem(sourcePosition, weapon);

        if (!staggerTriggered)
            TakeKnockback(sourcePosition, weapon.Knockback, 0.1f, weapon);

        if (staggerTriggered)
        {
            camera?.ShakeCamera(5f);
            if (TenacityCooldownCue != null)
                TenacityCooldownCue.Visible = true;
        }

        UpdateResourceBars();
    }

    public override void TakeKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        TakeKnockback(sourcePosition, force, duration, null);
    }

    private void TakeKnockback(Vector2 sourcePosition, float force, float duration, WeaponArc weapon)
    {
        if (!canBeKnockbacked)
            return;

        float subtleForce = force > 0f ? force : 30f;

        if (IsInStaggerWindow)
        {
            float staggeredForce = force > 0f ? force * 2.5f : 75f;
            float weaknessMultiplier = 1f;

            if (weapon != null)
            {
                // attack types removed — no weakness multiplier
                weaknessMultiplier = 1f;
            }

            Vector2 knockbackDirection = (GlobalPosition - sourcePosition).Normalized();
            TenacitySystem?.RequestKnockback(knockbackDirection, staggeredForce * weaknessMultiplier, 0.2f);
            return;
        }

        float defaultMultiplier = 1f;
        if (weapon != null)
        {
            // attack types removed — no weakness multiplier
            defaultMultiplier = 1f;
        }

        Vector2 normalDirection = (GlobalPosition - sourcePosition).Normalized();
        TenacitySystem?.RequestKnockback(normalDirection, subtleForce * defaultMultiplier, duration);
    }
}
