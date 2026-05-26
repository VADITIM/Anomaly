using Godot;

public partial class Entity : CharacterBody2D
{
    private const string DAMAGE_FLASH_SHADER_PATH = "res://#Shaders/damageflash.gdshader";

    [Export] public bool  canBeKnockedBack  { get; set; } = true;
    [Export] public bool  canBeKnockbacked  { get => canBeKnockedBack; set => canBeKnockedBack = value; }
    [Export] public float weight            { get; set; } = 1f;
    [Export] public float knockbackDecay    { get; set; } = 2000f;
    [Export] public float outsideKnockbackForce { get; set; } = 1f;
    [Export] public float speed                 { get; set; } = 100f;
    [Export] public float damage                { get; set; } = 10f;
    [Export] public float armor                 { get; set; } = 0f;
    [Export] public float tenacity              { get; set; } = 5f;
    [Export] public float maxTenacity           { get; set; } = 10f;
    [Export] public int maxStaggers             { get; set; } = 0;
    [Export] public float cameraPriority        { get; set; } = 0f;

    protected Vector2 knockbackVelocity = Vector2.Zero;
    protected float   knockbackDuration = 0f;

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

    public AnimationPlayer AnimationPlayer { get; set; }
    public StateMachine StateMachine { get; protected set; }

    [Export] public float attackDuration { get; set; } = 1f;

    [Export] public float damageFlashDuration { get; set; } = 0.5f;
    [Export] public Color damageFlashColor { get; set; } = Colors.White;

    [ExportGroup("Health")]
    [Export] public float maxHealth { get; set; } = 99999f;
    [Export] public float health    { get; set; }

    protected Control             ResourceBarControl;
    protected TextureProgressBar  HealthBar;
    protected TextureProgressBar  HealthBarGhost;
    private HealthBarAnimator _healthBarAnimator = null;
    private Sprite2D _damageFlashSprite = null;
    private ShaderMaterial _damageFlashMaterial = null;
    private Tween _damageFlashTween = null;

    private State _lastAnimationState = (State)(-1);
    private string _lastAnimationDirection = "";
    private bool _lastAnimationFlipH = false;

    protected virtual bool  CanTakeDamage(float damage, Vector2 sourcePosition)                                          => true;
    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition)                                   => damage;
    protected virtual void  OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth)   { }
    protected virtual void  OnDeath(Vector2 sourcePosition)                                                              { }
    protected virtual void  OnKnockbackFinished()                                                                        { }

    protected virtual float GetHealth()               => health;
    protected virtual void  SetHealth(float value)    { health = Mathf.Clamp(value, 0f, GetMaxHealth()); UpdateResourceBars(); }
    protected virtual float GetMaxHealth()            => maxHealth;
    protected virtual void  SetMaxHealth(float value) { maxHealth = Mathf.Max(0f, value); UpdateResourceBars(); }

    protected virtual bool UsesDirectionalAnimations => false;

    public virtual float GetAttackDuration() => attackDuration;

    protected void TriggerDamageFlash()
    {
        EnsureDamageFlashMaterial();

        if (_damageFlashMaterial == null)
            return;

        _damageFlashTween?.Kill();
        _damageFlashMaterial.SetShaderParameter("flash_color", damageFlashColor);
        _damageFlashMaterial.SetShaderParameter("flash_value", 1f);

        _damageFlashTween = CreateTween();
        _damageFlashTween.TweenProperty(_damageFlashMaterial, "shader_parameter/flash_value", 0f, Mathf.Max(0.01f, damageFlashDuration));
    }

    public override void _Ready()
    {
        EnsureStateMachine();
        InitializeJump();
        EnsureDamageFlashMaterial();
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessKnockback((float)delta);
        UpdateJump((float)delta);
        YSortSystem.Update(this);
    }

    public override void _Process(double delta)
    {
        _healthBarAnimator?.Update((float)delta);
        UpdateAnimation(UsesDirectionalAnimations);
    }

    public virtual void Jump()
    {
        if (jumpTimer <= 0f)
        {
            jumpVelocity = jumpImpulse;
            jumpTimer = 1f;
        }
    }

    public virtual bool IsJumping => jumpTimer > 0f;

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
            DamageNumber.Spawn(this, effectiveDamage, DamageNumberStyle.Standard);
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

    public virtual void InitializeBars() => InitializeResourceBars();

    public void InitializeResourceBars()
    {
        (ResourceBarControl, HealthBar) = InitializeEntity.InitializeResourceBars(this);

        if (ResourceBarControl != null)
        {
            HealthBarGhost = ResourceBarControl.GetNodeOrNull<TextureProgressBar>("Health Bar Ghost")
                          ?? ResourceBarControl.GetNodeOrNull<TextureProgressBar>("HealthBarGhost");
        }

        UpdateResourceBars();

        _healthBarAnimator = new HealthBarAnimator();
        _healthBarAnimator.Initialize(ResourceBarControl, HealthBar, HealthBarGhost, GetHealth());
    }

    protected virtual void UpdateResourceBars()
    {
        _healthBarAnimator?.OnHealthChanged(GetHealth(), GetMaxHealth());
    }

    protected virtual State GetCurrentAnimationState()
        => IsEntityDead() ? State.Dead : State.Idle;

    protected virtual string GetCurrentAnimationDirection(bool useDirectionalAnimations, out bool flipH)
    {
        flipH = false;
        return "S";
    }

    protected virtual bool IsEntityDead() => false;

    protected virtual float GetAnimationDuration(string animationName, bool useDirectionalAnimations)
        => GetNativeAnimationDuration(animationName);

    protected virtual string[] GetAnimationCandidates(State state, string direction, bool useDirectionalAnimations)
    {
        string resolvedDirection = useDirectionalAnimations ? direction : "S";
        string idle = $"Idle_{resolvedDirection}";

        return state switch
        {
            State.Moving => new[] { $"Move_{resolvedDirection}", idle },
            State.Chasing => new[] { $"Move_{resolvedDirection}", idle },
            State.Airborne => new[] { "Jump_S", idle },
            State.Attacking => new[] { $"Attack_{resolvedDirection}_1", $"attack_{resolvedDirection}_1", $"Attack_{resolvedDirection}", idle },
            State.HeavyAttacking => new[] { "Attack_Spin", $"Attack_{resolvedDirection}", idle },
            State.AirAttacking => new[] { $"Air_Attack_{resolvedDirection}", $"Attack_{resolvedDirection}_1", $"Attack_{resolvedDirection}", idle },
            State.Dodging => new[] { $"Dodge_{resolvedDirection}", $"Move_{resolvedDirection}", idle },
            State.Healing => new[] { idle },
            State.Staggered => new[] { idle },
            State.Knockback => new[] { idle },
            State.Dead => new[] { $"Die_{resolvedDirection}", "Die_S", "Idle_S", idle },
            _ => new[] { idle }
        };
    }

    protected virtual bool PlayPlayerAnimation(string animationName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animationName))
            return false;

        PlayAnimation(animationName);
        return true;
    }

    protected virtual void ApplyFacing(bool flipH) { }

    protected virtual void OnAnimationPlayed(string animationName, State state, string direction, bool flipH) { }

    protected virtual bool IsAttackAnimation(string animationName)
    {
        return animationName.StartsWith("Attack") ||
               animationName.StartsWith("attack") ||
               animationName == "Attack_Spin";
    }

    public virtual void UpdateAnimation()
    {
        UpdateAnimation(UsesDirectionalAnimations);
    }

    public virtual void UpdateAnimation(bool useDirectionalAnimations)
    {
        if (AnimationPlayer == null)
            return;

        State currentState = GetCurrentAnimationState();
        string direction = GetCurrentAnimationDirection(useDirectionalAnimations, out bool flipH);

        if (currentState == _lastAnimationState &&
            direction == _lastAnimationDirection &&
            flipH == _lastAnimationFlipH)
        {
            return;
        }

        _lastAnimationState = currentState;
        _lastAnimationDirection = direction;
        _lastAnimationFlipH = flipH;

        ApplyFacing(flipH);

        foreach (string animationName in GetAnimationCandidates(currentState, direction, useDirectionalAnimations))
        {
            if (!AnimationPlayer.HasAnimation(animationName))
                continue;

            if (IsAttackAnimation(animationName))
            {
                float desiredDuration = GetAnimationDuration(animationName, useDirectionalAnimations);
                float nativeLength = GetNativeAnimationDuration(animationName);
                AnimationPlayer.SpeedScale = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
            }
            else
            {
                AnimationPlayer.SpeedScale = 1f;
            }

            if (PlayPlayerAnimation(animationName))
            {
                OnAnimationPlayed(animationName, currentState, direction, flipH);
                return;
            }
        }
    }

    public virtual void PlayAnimation(string animName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animName)) return;
        if (AnimationPlayer.CurrentAnimation != animName)
            AnimationPlayer.Play(animName);
    }

    private float GetNativeAnimationDuration(string animationName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animationName))
            return 0.1f;

        Animation animation = AnimationPlayer.GetAnimation(animationName);
        if (animation == null)
            return 0.1f;

        return Mathf.Max(0.1f, (float)animation.Length);
    }

    private void EnsureStateMachine()
    {
        StateMachine = GetNodeOrNull<StateMachine>("StateMachine");

        if (StateMachine == null)
        {
            StateMachine = new StateMachine();
            StateMachine.Name = "StateMachine";
            AddChild(StateMachine);
        }
    }

    private void EnsureDamageFlashMaterial()
    {
        if (_damageFlashMaterial != null)
            return;

        _damageFlashSprite = GetNodeOrNull<Sprite2D>("Sprite");
        if (_damageFlashSprite == null)
            return;

        Shader shader = GD.Load<Shader>(DAMAGE_FLASH_SHADER_PATH);
        if (shader == null)
            return;

        if (_damageFlashSprite.Material is ShaderMaterial existingMaterial && existingMaterial.Shader == shader)
        {
            _damageFlashMaterial = existingMaterial;
        }
        else
        {
            _damageFlashMaterial = new ShaderMaterial
            {
                Shader = shader
            };
            _damageFlashSprite.Material = _damageFlashMaterial;
        }

        _damageFlashMaterial.SetShaderParameter("flash_color", damageFlashColor);
        _damageFlashMaterial.SetShaderParameter("flash_value", 0f);
    }

    private void InitializeJump()
    {
        if (HasNode("Sprite"))
        {
            var sprite = GetNode<Sprite2D>("Sprite");
            if (sprite != null)
                spriteBasePosition = sprite.Position;
        }

        if (HasNode("Sprite Shadow"))
        {
            spriteShadow = GetNode<Sprite2D>("Sprite Shadow");
            if (spriteShadow != null)
                shadowBaseScale = spriteShadow.Scale;
        }

        if (HasNode("WEAPON"))
        {
            WeaponNode = GetNode<Node2D>("WEAPON");
            if (WeaponNode != null)
                weaponNodeBasePosition = WeaponNode.Position;
        }
    }

    private void UpdateJump(float delta)
    {
        if (jumpTimer <= 0f) return;

        jumpVelocity -= jumpFallSpeed * delta;
        jumpPosition += jumpVelocity * delta;
        jumpPosition  = Mathf.Max(0f, jumpPosition);

        UpdateSpriteOffset(jumpPosition);

        float maxHeight   = (jumpImpulse * jumpImpulse) / (2f * jumpFallSpeed);
        float heightRatio = maxHeight > 0f ? jumpPosition / maxHeight : 0f;
        heightRatio       = Mathf.Clamp(heightRatio, 0f, 1f);
        UpdateShadowScale(heightRatio);

        if (jumpPosition <= 0f && jumpVelocity < 0f)
        {
            jumpTimer    = 0f;
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
            sprite.Position = spriteBasePosition + new Vector2(0, -offset);

        if (WeaponNode != null)
            WeaponNode.Position = weaponNodeBasePosition + new Vector2(0, -offset);
    }

    private void UpdateShadowScale(float heightRatio)
    {
        if (spriteShadow == null) return;
        float shadowScale  = Mathf.Lerp(1f, shadowScaleWhenJump, heightRatio);
        spriteShadow.Scale = shadowBaseScale * shadowScale;
    }
}