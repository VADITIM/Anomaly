using Godot;

public partial class Entity : CharacterBody2D
{
    // ── Knockback ─────────────────────────────────────────────────────────────
    [Export] public bool  canBeKnockedBack  { get; set; } = true;
    [Export] public bool  canBeKnockbacked  { get => canBeKnockedBack; set => canBeKnockedBack = value; }
    [Export] public float weight            { get; set; } = 1f;
    [Export] public float knockbackDecay    { get; set; } = 2000f;

    protected Vector2 knockbackVelocity = Vector2.Zero;
    protected float   knockbackDuration = 0f;

    // ── Jump ───────────────────────────────────────────────────────────────────
    [Export] public float jumpImpulse         { get; set; } = 300f;
    [Export] public float jumpFallSpeed       { get; set; } = 1200f;
    [Export] public float shadowScaleWhenJump { get; set; } = 0.5f;

    protected float jumpTimer = 0f;
    protected float jumpVelocity = 0f;
    protected float jumpPosition = 0f;
    protected Vector2 spriteBasePosition = Vector2.Zero;
    protected Vector2 shadowBaseScale = Vector2.One;
    protected Sprite2D spriteShadow = null;
    protected Vector2 weaponNodeBasePosition = Vector2.Zero;
    protected Node2D WeaponNode = null;

    // ── Misc exports ──────────────────────────────────────────────────────────
    [Export] public AnimationPlayer AnimationPlayer { get; set; }

    // ── Health ────────────────────────────────────────────────────────────────
    [ExportGroup("Health")]
    [Export] public float maxHealth { get; set; } = 99999f;
    [Export] public float health    { get; set; }

    protected Control  ResourceBarControl;
    protected TextureProgressBar HealthBar;

    // ═════════════════════════════════════════════════════════════════════════
    //  Virtual hooks for subclasses
    // ═════════════════════════════════════════════════════════════════════════

    protected virtual bool  CanTakeDamage(float damage, Vector2 sourcePosition)                                          => true;
    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition)                                   => damage;
    protected virtual void  OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth)   { }
    protected virtual void  OnDeath(Vector2 sourcePosition)                                                              { }
    protected virtual void  OnKnockbackFinished()                                                                        { }

    protected virtual float GetHealth()               => health;
    protected virtual void  SetHealth(float value)    { health = Mathf.Clamp(value, 0f, GetMaxHealth()); UpdateResourceBars(); }
    protected virtual float GetMaxHealth()            => maxHealth;
    protected virtual void  SetMaxHealth(float value) { maxHealth = Mathf.Max(0f, value); UpdateResourceBars(); }

    // ═════════════════════════════════════════════════════════════════════════
    //  Godot lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        InitializeJump();
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessKnockback((float)delta);
        UpdateJump((float)delta);
        YSortSystem.Update(this);
    }


    // ═════════════════════════════════════════════════════════════════════════
    //  Public jump API
    // ═════════════════════════════════════════════════════════════════════════

    public virtual void Jump()
    {
        if (jumpTimer <= 0f)
        {
            jumpVelocity = jumpImpulse;
            jumpTimer = 1f; // Will continue until landing (y position <= 0)
        }
    }

    public virtual bool IsJumping => jumpTimer > 0f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Knockback
    // ═════════════════════════════════════════════════════════════════════════

    protected void ProcessKnockback(float delta)
    {
        if (!canBeKnockbacked) return;

        if (knockbackDuration > 0 || knockbackVelocity.Length() > 0.1f)
        {
            knockbackDuration -= delta;
            Velocity           = knockbackVelocity;
            MoveAndSlide();
            knockbackVelocity  = knockbackVelocity.MoveToward(Vector2.Zero, knockbackDecay * delta);

            if (knockbackDuration <= 0 && knockbackVelocity.Length() < 1f)
            {
                knockbackVelocity = Vector2.Zero;
                OnKnockbackFinished();
            }
        }
    }

    public virtual void ApplyKnockback(Vector2 direction, float force, float duration = 0.2f)
    {
        float effectiveForce = force / Mathf.Max(weight, 0.1f);
        knockbackVelocity    = direction.Normalized() * effectiveForce;
        knockbackDuration    = duration;
    }

    public virtual void TakeKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (!canBeKnockbacked) return;
        Vector2 dir = (GlobalPosition - sourcePosition).Normalized();
        ApplyKnockback(dir, force, duration);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Damage
    // ═════════════════════════════════════════════════════════════════════════

    public virtual void TakeDamage(float damage, Vector2 sourcePosition)
    {
        if (damage <= 0f || !CanTakeDamage(damage, sourcePosition))
            return;

        float effectiveDamage = Mathf.Max(0f, ApplyDamageModifiers(damage, sourcePosition));
        if (effectiveDamage <= 0f)
            return;

        float currentHealth = GetHealth();
        float newHealth     = Mathf.Max(0f, currentHealth - effectiveDamage);

        SetHealth(newHealth);
        SpawnDamageNumber(effectiveDamage, DamageNumberStyle.Standard);
        OnDamageTaken(effectiveDamage, sourcePosition, currentHealth, newHealth);

        if (newHealth <= 0f)
            OnDeath(sourcePosition);
    }

    public virtual void TakeDamage(WeaponArc weapon, Node2D damageSource)
    {
        if (weapon == null) return;
        Vector2 sourcePosition  = damageSource?.GlobalPosition ?? GlobalPosition;
        TakeDamage(weapon.Damage, sourcePosition);
        TakeKnockback(sourcePosition, weapon.Knockback);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Resource bars
    // ═════════════════════════════════════════════════════════════════════════

    public virtual void InitializeBars() => InitializeResourceBars();

    public void InitializeResourceBars()
    {
        (ResourceBarControl, HealthBar) = InitializeEntity.InitializeResourceBars(this);
        UpdateResourceBars();
    }

    protected virtual void UpdateResourceBars()
    {
        if (HealthBar == null) return;
        float max       = Mathf.Max(GetMaxHealth(), 1f);
        HealthBar.MaxValue = max;
        HealthBar.Value    = Mathf.Clamp(GetHealth(), 0f, max);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Misc
    // ═════════════════════════════════════════════════════════════════════════

    protected void SpawnDamageNumber(float damageAmount, DamageNumberStyle style)
        => DamageNumberSpawner.Spawn(this, damageAmount, style);

    public virtual void PlayAnimation(string animName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animName)) return;
        if (AnimationPlayer.CurrentAnimation != animName)
            AnimationPlayer.Play(animName);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jump
    // ═════════════════════════════════════════════════════════════════════════

    private void InitializeJump()
    {
        // Cache the sprite's base position
        if (HasNode("Sprite"))
        {
            var sprite = GetNode<Sprite2D>("Sprite");
            if (sprite != null)
            {
                spriteBasePosition = sprite.Position;
            }
        }

        // Cache the shadow sprite if it exists
        if (HasNode("Sprite Shadow"))
        {
            spriteShadow = GetNode<Sprite2D>("Sprite Shadow");
            if (spriteShadow != null)
            {
                shadowBaseScale = spriteShadow.Scale;
            }
        }

        // Cache the scythe sprite if it exists (in WEAPON node for Player)
        if (HasNode("WEAPON"))
        {
            WeaponNode = GetNode<Node2D>("WEAPON");
            if (WeaponNode != null)
            {
                weaponNodeBasePosition = WeaponNode.Position;
            }
        }
    }

    private void UpdateJump(float delta)
    {
        if (jumpTimer <= 0f) return;

        // Apply gravity to velocity
        jumpVelocity -= jumpFallSpeed * delta;

        // Update position
        jumpPosition += jumpVelocity * delta;
        jumpPosition = Mathf.Max(0f, jumpPosition);

        UpdateSpriteOffset(jumpPosition);

        // Calculate shadow scale based on height ratio
        float maxHeight = (jumpImpulse * jumpImpulse) / (2f * jumpFallSpeed);
        float heightRatio = maxHeight > 0f ? jumpPosition / maxHeight : 0f;
        heightRatio = Mathf.Clamp(heightRatio, 0f, 1f);
        UpdateShadowScale(heightRatio);

        // Land when position reaches ground
        if (jumpPosition <= 0f && jumpVelocity < 0f)
        {
            jumpTimer = 0f;
            jumpVelocity = 0f;
            jumpPosition = 0f;
            UpdateSpriteOffset(0f);
            if (spriteShadow != null)
                spriteShadow.Scale = shadowBaseScale;
        }
    }

    private void UpdateSpriteOffset(float offset)
    {
        if (!HasNode("Sprite")) return;
        var sprite = GetNode<Sprite2D>("Sprite");
        if (sprite != null)
        {
            sprite.Position = spriteBasePosition + new Vector2(0, -offset);
        }

        if (WeaponNode != null)
        {
            WeaponNode.Position = weaponNodeBasePosition + new Vector2(0, -offset);
        }
    }

    private void UpdateShadowScale(float heightRatio)
    {
        if (spriteShadow == null) return;
        
        // Scale down shadow based on how high the jump is
        // At ground level (0): full scale, at peak (1): minimum scale
        float shadowScale = Mathf.Lerp(1f, shadowScaleWhenJump, heightRatio);
        spriteShadow.Scale = shadowBaseScale * shadowScale;
    }
}