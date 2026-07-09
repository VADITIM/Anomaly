using Godot;
using System;

public partial class Prop : Entity
{
    public float StartingHealth { get; set; } = 90f;
    public bool Destroyable { get; set; } = true;

    private bool _isDead = false;

    protected override State GetCurrentAnimationState()
    {
        return _isDead ? State.Dead : State.Idle;
    }

    public override void _Ready()
    {
        base._Ready();

        AddBehavior(new KnockbackBehavior());
        AddBehavior(new CommonDamageFlash());

        SetMaxHealth(StartingHealth);
        SetHealth(StartingHealth);

        AddBehavior(new PropResourceBarBehavior());
        PlayAnimation("Idle_Down");
    }

    public override void TakeDamage(float damage, Vector2 sourcePosition, WeaponArc weapon = null)
    {
        if (_isDead || !Destroyable) return;

        if (weapon == null)
        {
            base.TakeDamage(damage, sourcePosition, weapon);
            return;
        }

        GetBehavior<CommonDamageFlash>()?.Flash();

        float newHealth = GetHealth() - damage;
        SetHealth(newHealth);
        DamageNumber.Spawn(this, damage, DamageNumberStyle.Standard, this);
        GetBehavior<PropResourceBarBehavior>()?.ShowForHit();

        TakeKnockback(sourcePosition, weapon.Knockback);

        if (GetHealth() <= 0)
        {
            Die();
        }
        else
        {
            SceneTreeTimer timer = GetTree().CreateTimer(0.2f);
            timer.Timeout += () => {
                if (!_isDead && !(GetBehavior<KnockbackBehavior>()?.IsKnockbackActive ?? false))
                    PlayAnimation("Idle_Down");
            };
        }
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        CollisionLayer = 0;
        CollisionMask = 0;

        PlayAnimation("Die_Down");

        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Die_Down"))
        {
            float length = (float)AnimationPlayer.GetAnimation("Die_Down").Length;
            GetTree().CreateTimer(Mathf.Max(length, 0.1f)).Timeout += QueueFree;
        }
        else
        {
            QueueFree();
        }
    }
}
