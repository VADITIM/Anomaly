using Godot;
using System;

public partial class Prop : Entity
{
    public float startingHealth { get; set; } = 90f;
    public bool Destroyable { get; set; } = true;
    
    private bool _isDead = false;
    private Tween _resourceBarTween;

    private const float ResourceBarVisibleSeconds = 8f;
    private const float ResourceBarFadeSeconds = 0.5f;

    protected override State GetCurrentAnimationState()
    {
        return _isDead ? State.Dead : State.Idle;
    }

    public override void _Ready()
    {
        base._Ready();

        SetMaxHealth(startingHealth);
        SetHealth(startingHealth);

        InitializeResourceBars();
        HideResourceBar();
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

        TriggerDamageFlash();

        float newHealth = GetHealth() - damage;
        SetHealth(newHealth);
        DamageNumber.Spawn(this, damage, DamageNumberStyle.Standard, this);
        ShowResourceBarForHit();

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

    private void ShowResourceBarForHit()
    {
        if (ResourceBarControl == null)
            return;

        _resourceBarTween?.Kill();
        ResourceBarControl.Visible = true;
        ResourceBarControl.Modulate = new Color(ResourceBarControl.Modulate.R, ResourceBarControl.Modulate.G, ResourceBarControl.Modulate.B, 1f);

        _resourceBarTween = CreateTween();
        _resourceBarTween.TweenInterval(ResourceBarVisibleSeconds);
        _resourceBarTween.TweenProperty(ResourceBarControl, "modulate:a", 0f, ResourceBarFadeSeconds);
        _resourceBarTween.TweenCallback(Callable.From(HideResourceBar));
    }

    private void HideResourceBar()
    {
        if (ResourceBarControl == null)
            return;

        ResourceBarControl.Visible = false;
        ResourceBarControl.Modulate = new Color(ResourceBarControl.Modulate.R, ResourceBarControl.Modulate.G, ResourceBarControl.Modulate.B, 0f);
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
            float length = AnimationPlayer.GetAnimation("Die_Down").Length;
            GetTree().CreateTimer(Mathf.Max(length, 0.1f)).Timeout += QueueFree;
        }
        else
        {
            QueueFree();
        }
    }

}