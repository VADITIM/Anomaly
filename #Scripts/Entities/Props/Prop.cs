using Godot;
using System;

public partial class Prop : Entity
{
    public float StartingHealth { get; set; } = 90f;
    public bool Destroyable { get; set; } = true;

    [Export] public float ImpactRadius { get; set; } = 24f;

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

    // A prop pushed off a ledge crushes whatever it lands on. Damage scales with the drop and the
    // prop's Weight (ElevationMath.FallImpactDamage) — see docs/design.md §3.2 "Z Axis Development".
    protected override void OnLanded(float elevation, float elevationsFallen)
    {
        float impactDamage = ElevationMath.FallImpactDamage(elevationsFallen, Weight);
        if (impactDamage <= 0f)
            return;

        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new CircleShape2D { Radius = ImpactRadius },
            Transform = new Transform2D(0f, GlobalPosition),
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = BaseCollisionMask
        };

        foreach (Godot.Collections.Dictionary hit in GetWorld2D().DirectSpaceState.IntersectShape(query))
        {
            if (hit["collider"].As<GodotObject>() is not Entity crushed || crushed == this)
                continue;

            if (ElevationMath.SharesPlane(elevation, crushed.Elevation))
                crushed.TakeDamage(impactDamage, GlobalPosition);
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
