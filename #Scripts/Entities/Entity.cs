using Godot;

public partial class Entity : CharacterBody2D
{
    [Export] public bool canBeKnockedBack { get; set; } = true;
    [Export] public bool canBeKnockbacked
    {
        get => canBeKnockedBack;
        set => canBeKnockedBack = value;
    }
    [Export] public float weight { get; set; } = 1f;
    [Export] public float knockbackDecay { get; set; } = 2000f;
    protected Vector2 knockbackVelocity = Vector2.Zero;
    protected float knockbackDuration = 0f;
    [Export] public AnimationPlayer AnimationPlayer { get; set; }

    [ExportGroup("Health")]
    [Export] public float maxHealth { get; set; } = 99999f;
    [Export] public float health { get; set; }

    [ExportGroup("Z Axis")]
    [Export(PropertyHint.Range, "0,256,1")] public float JumpHeight { get => zAxis?.JumpHeight ?? 20f; set { if (zAxis != null) zAxis.JumpHeight = value; } }
    [Export(PropertyHint.Range, "0,4096,1")] public float Gravity { get => zAxis?.Gravity ?? 900f; set { if (zAxis != null) zAxis.Gravity = value; } }
    [Export(PropertyHint.Range, "0,8192,1")] public float TerminalVelocity { get => zAxis?.TerminalVelocity ?? 3000f; set { if (zAxis != null) zAxis.TerminalVelocity = value; } }
    public float FloorZ => zAxis?.FloorZ ?? 0f;

    [ExportGroup("Z Collision")]
    [Export] public bool IgnoreWallsWhenAirborne { get => zAxis?.IgnoreWallsWhenAirborne ?? true; set { if (zAxis != null) zAxis.IgnoreWallsWhenAirborne = value; } }
    [Export(PropertyHint.Range, "1,32,1")] public int WallCollisionLayerNumber { get => zAxis?.WallCollisionLayerNumber ?? 6; set { if (zAxis != null) zAxis.WallCollisionLayerNumber = value; } }
    [Export(PropertyHint.Range, "0,4096,0.1")] public float AirborneEpsilon { get => zAxis?.AirborneEpsilon ?? 0.1f; set { if (zAxis != null) zAxis.AirborneEpsilon = value; } }

    protected ZAxis zAxis;
    protected Control ResourceBarControl;
    protected TextureProgressBar HealthBar;

    public float Z => zAxis?.Z ?? 0f;
    public float ZVelocity => zAxis?.ZVelocity ?? 0f;

    public bool IsGrounded => zAxis?.IsGrounded ?? false;
    public bool IsAirborne => zAxis?.IsAirborne ?? false;

    protected virtual bool CanTakeDamage(float damage, Vector2 sourcePosition) => true;
    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition) => damage;
    protected virtual void OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth) { }
    protected virtual void OnDeath(Vector2 sourcePosition) { }

    public virtual void OnZChanged() { }

    protected virtual void OnKnockbackFinished() { }

    protected virtual float GetHealth() => health;
    protected virtual void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, GetMaxHealth());
        UpdateResourceBars();
    }

    protected virtual float GetMaxHealth() => maxHealth;
    protected virtual void SetMaxHealth(float value)
    {
        maxHealth = Mathf.Max(0f, value);
        UpdateResourceBars();
    }

    public override void _PhysicsProcess(double delta)
    {
        zAxis?.ProcessVertical((float)delta);
        ProcessKnockback((float)delta);
        YSortSystem.Update(this);
    }

    public override void _Ready()
    {
        zAxis = new ZAxis(this);
    }

    protected void ProcessVertical(float delta)
    {
        zAxis?.ProcessVertical(delta);
    }

    protected void ProcessKnockback(float delta)
    {
        if (!canBeKnockbacked) return;

        if (knockbackDuration > 0 || knockbackVelocity.Length() > 0.1f)
        {
            knockbackDuration -= delta;
            
            Velocity = knockbackVelocity;
            MoveAndSlide();

            knockbackVelocity = knockbackVelocity.MoveToward(Vector2.Zero, knockbackDecay * delta);
            
            if (knockbackDuration <= 0 && knockbackVelocity.Length() < 1f)
            {
                knockbackVelocity = Vector2.Zero;
                OnKnockbackFinished();
            }
        }
    }


    public virtual void InitializeBars()
    {
        InitializeResourceBars();
    }

    public void InitializeResourceBars()
    {
        (ResourceBarControl, HealthBar) = InitializeEntity.InitializeResourceBars(this);
        UpdateResourceBars();
    }

    protected virtual void UpdateResourceBars()
    {
        if (HealthBar == null)
            return;

        float maxHealth = Mathf.Max(GetMaxHealth(), 1f);
        HealthBar.MaxValue = maxHealth;
        HealthBar.Value = Mathf.Clamp(GetHealth(), 0f, maxHealth);
    }


    public virtual void TakeDamage(float damage, Vector2 sourcePosition)
    {
        if (damage <= 0f || !CanTakeDamage(damage, sourcePosition))
            return;

        float effectiveDamage = Mathf.Max(0f, ApplyDamageModifiers(damage, sourcePosition));
        if (effectiveDamage <= 0f)
            return;

        float currentHealth = GetHealth();
        float newHealth = Mathf.Max(0f, currentHealth - effectiveDamage);

        SetHealth(newHealth);
        SpawnDamageNumber(effectiveDamage, DamageNumberStyle.Standard);
        OnDamageTaken(effectiveDamage, sourcePosition, currentHealth, newHealth);

        if (newHealth <= 0f)
            OnDeath(sourcePosition);
    }

    public virtual void TakeDamage(WeaponArc weapon, Node2D damageSource)
    {
        if (weapon == null)
            return;

        Vector2 sourcePosition = damageSource?.GlobalPosition ?? GlobalPosition;
        float calculatedDamage = weapon.Damage;
        TakeDamage(calculatedDamage, sourcePosition);
        TakeKnockback(sourcePosition, weapon.Knockback);
    }

    public virtual void ApplyKnockback(Vector2 direction, float force, float duration = 0.2f)
    {
        // Weight influence: force is divided by weight (Weight 1.0 = 100% force, 2.0 = 50%)
        float effectiveForce = force / Mathf.Max(weight, 0.1f);
        
        knockbackVelocity = direction.Normalized() * effectiveForce;
        knockbackDuration = duration;
    }

    public virtual void TakeKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (!canBeKnockbacked)
            return;

        Vector2 knockbackDirection = (GlobalPosition - sourcePosition).Normalized();
        ApplyKnockback(knockbackDirection, force, duration);
    }


    public void SetFloorZ(float newFloorZ)
    {
        zAxis?.SetFloorZ(newFloorZ);
    }

    protected void SpawnDamageNumber(float damageAmount, DamageNumberStyle style)
    {
        DamageNumberSpawner.Spawn(this, damageAmount, style);
    }

    public bool TryJump(float? impulse = null)
    {
        return zAxis?.TryJump(impulse) ?? false;
    }



    public virtual void PlayAnimation(string animName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animName))
            return;

        if (AnimationPlayer.CurrentAnimation != animName)
        {
            AnimationPlayer.Play(animName);
        }
    }
}
