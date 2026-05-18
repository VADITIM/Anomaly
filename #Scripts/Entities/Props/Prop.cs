using Godot;
using System;

public partial class Prop : Entity
{
    public float health { get; set; } = 90f;
    public bool Destroyable { get; set; } = true;
    
    private bool _isDead = false;

    protected override State GetCurrentAnimationState()
    {
        return _isDead ? State.Dead : State.Idle;
    }

    public override void _Ready()
    {
        base._Ready();

        SetMaxHealth(health);
        SetHealth(health);

        InitializeBars();
        PlayAnimation("Idle_Down");
    }

    public override void TakeDamage(WeaponArc weapon, Node2D damageSource)
    {
        if (_isDead || !Destroyable) return;
        if (weapon == null) return;

        Vector2 sourcePosition = damageSource?.GlobalPosition ?? GlobalPosition;

        TriggerDamageFlash();

        float newHealth = GetHealth() - weapon.Damage;
        SetHealth(newHealth);
        SpawnDamageNumber(weapon.Damage, DamageNumberStyle.Standard);

        TakeKnockback(sourcePosition, weapon.Knockback);

        if (GetHealth() <= 0)
        {
            Die();
        }
        else
        {
            SceneTreeTimer timer = GetTree().CreateTimer(0.2f);
            timer.Timeout += () => {
                if (!_isDead && knockbackVelocity.Length() < 10f) 
                    PlayAnimation("Idle_Down");
            };
        }
    }

    public override void ApplyKnockback(Vector2 direction, float force, float duration = 0.2f)
    {
        // Standard knockback calculation based on weight
        float effectiveForce = force / Mathf.Max(weight, 0.1f);
        knockbackVelocity = direction.Normalized() * effectiveForce;
        knockbackDuration = duration;
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // Disable interaction layers (matching Prop layer logic)
        CollisionLayer = 0;
        CollisionMask = 0;
        
        PlayAnimation("Die_Down");

        // Wait for the Die_Down animation to finish before removing from tree
        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Die_Down"))
        {
            float length = AnimationPlayer.GetAnimation("Die_Down").Length;
            GetTree().CreateTimer(Mathf.Max(length, 0.1f)).Timeout += QueueFree;
        }
        else
        {
            QueueFree();
        }
    }

}