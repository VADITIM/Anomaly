using Godot;
using System.Collections.Generic;

public partial class Entity : CharacterBody2D
{
    private readonly List<IEntityBehavior> behaviors = new();
    public StateMachine StateMachine { get; protected set; }
    public AnimationPlayer AnimationPlayer { get; set; }
    public Sprite2D Sprite { get; set; }

    // Named after its type to leave the plain "Stats" name to subclass runtime
    // stat containers (Player.Stats is a PlayerStats).
    [Export] public EntityStats EntityStats { get; set; }

    [Export] public bool  CanBeKnockedBack     { get; set; } = true;
    [Export] public float Weight               { get; set; } = 1f;
    [Export] public float KnockbackDecay       { get; set; } = 2000f;
    [Export] public float Speed                { get; set; } = 100f;
    [Export] public float Damage               { get; set; } = 10f;
    [Export] public float Armor                { get; set; } = 0f;
    [Export] public float Tenacity             { get; set; } = 5f;
    [Export] public float MaxTenacity          { get; set; } = 10f;
    [Export] public int   MaxStaggers          { get; set; } = 3;
    [Export] public float CameraPriority       { get; set; } = 0f;
    [Export] public float JumpImpulse         { get; set; } = 300f;
    [Export] public float JumpFallSpeed       { get; set; } = 1200f;
    [Export] public float ShadowScaleWhenJump { get; set; } = 0.5f;

    protected ZAxisSystem ZAxis;

    [Export] public float AttackDuration { get; set; } = 1f;

    [ExportGroup("Health")]
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float Health    { get; set; } = 100f;

    private State lastAnimationState = (State)(-1);
    private string lastAnimationDirection = "";
    private bool lastAnimationFlipH = false;

    protected virtual bool  CanTakeDamage(float damage, Vector2 sourcePosition)                                         => true;
    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition)                                  => damage;
    protected virtual void  OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth)  { }
    protected virtual void  OnDeath(Vector2 sourcePosition)                                                             { }
    protected virtual void  OnKnockbackFinished()                                                                       { }

    public void NotifyKnockbackFinished()
    {
        OnKnockbackFinished();
    }

    protected virtual float GetHealth()               => Health;
    protected virtual void  SetHealth(float value)    { Health = Mathf.Clamp(value, 0f, GetMaxHealth()); NotifyResourceBars(); }
    protected virtual float GetMaxHealth()            => MaxHealth;
    protected virtual void  SetMaxHealth(float value) { MaxHealth = Mathf.Max(0f, value); NotifyResourceBars(); }

    public float CurrentHealth    => GetHealth();
    public float CurrentMaxHealth => GetMaxHealth();

    protected virtual bool UsesDirectionalAnimations => false;

    public virtual float GetAttackDuration() => AttackDuration;
    public bool IsJumping => ZAxis?.IsJumping ?? false;

    public override void _Ready()
    {
        ApplyEntityStats();
        InitializeEntity.InitializeNodes(this);
        EnsureStateMachine();
        InitializeZAxis();
        for (int i = 0; i < behaviors.Count; i++)
            behaviors[i].OnReady(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        ZAxis.Update((float)delta);
        YSortSystem.Update(this);
        for (int i = 0; i < behaviors.Count; i++)
            behaviors[i].OnPhysicsProcess(delta);
    }

    public override void _Process(double delta)
    {
        UpdateAnimation(UsesDirectionalAnimations);
        for (int i = 0; i < behaviors.Count; i++)
            behaviors[i].OnProcess(delta);
    }

    public override void _ExitTree()
    {
        for (int i = 0; i < behaviors.Count; i++)
            behaviors[i].OnExitTree();
        base._ExitTree();
    }

    public void AddBehavior(IEntityBehavior behavior)
    {
        if (behavior == null || behaviors.Contains(behavior))
            return;
        behaviors.Add(behavior);
        if (IsInsideTree())
            behavior.OnReady(this);
    }

    public void RemoveBehavior(IEntityBehavior behavior)
    {
        if (behavior == null)
            return;
        behaviors.Remove(behavior);
    }

    public T GetBehavior<T>() where T : class, IEntityBehavior
    {
        for (int i = 0; i < behaviors.Count; i++)
        {
            if (behaviors[i] is T typed)
                return typed;
        }
        return null;
    }

    public virtual void Jump()
    {
        ZAxis.Jump();
    }

    public virtual void TakeKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (!CanBeKnockedBack) return;
        Vector2 direction = (GlobalPosition - sourcePosition).Normalized();
        GetBehavior<KnockbackBehavior>()?.ApplyKnockbackFromDirection(direction, force, duration);
    }

    public virtual void TakeDamage(float damage, Vector2 sourcePosition, WeaponArc weapon = null)
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

    protected void NotifyResourceBars()
    {
        GetBehavior<ResourceBarBehavior>()?.OnHealthChanged(GetHealth(), GetMaxHealth());
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
            State.Moving        => new[] { $"Move_{resolvedDirection}", idle },
            State.Chasing       => new[] { $"Move_{resolvedDirection}", idle },
            State.Airborne      => new[] { "Jump_S", idle },
            State.Attacking     => new[] { $"Attack_{resolvedDirection}_1", $"attack_{resolvedDirection}_1", $"Attack_{resolvedDirection}", idle },
            State.HeavyAttacking => new[] { "Attack_Spin", $"Attack_{resolvedDirection}", idle },
            State.AirAttacking  => new[] { $"Air_Attack_{resolvedDirection}", $"Attack_{resolvedDirection}_1", $"Attack_{resolvedDirection}", idle },
            State.Dodging       => new[] { $"Dodge_{resolvedDirection}", $"Move_{resolvedDirection}", idle },
            State.Healing       => new[] { idle },
            State.Staggered     => new[] { idle },
            State.Knockback     => new[] { idle },
            State.Dead          => new[] { $"Die_{resolvedDirection}", "Die_S", "Idle_S", idle },
            _                   => new[] { idle }
        };
    }

    protected virtual bool PlayPlayerAnimation(string animationName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animationName))
            return false;

        PlayAnimation(animationName);
        return true;
    }

    protected virtual void ApplyFacing(bool flipH)
    {
        if (Sprite != null)
            Sprite.FlipH = flipH;
    }

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

        if (currentState == lastAnimationState &&
            direction == lastAnimationDirection &&
            flipH == lastAnimationFlipH)
        {
            return;
        }

        lastAnimationState = currentState;
        lastAnimationDirection = direction;
        lastAnimationFlipH = flipH;

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

    // NOTE: An assigned EntityStats resource is the stat authority — it overrides any
    // scene-set exported values below. Leave it unassigned to tune per-instance exports.
    private void ApplyEntityStats()
    {
        if (EntityStats == null) return;
        MaxHealth        = EntityStats.MaxHealth;
        Health           = EntityStats.MaxHealth;
        Speed            = EntityStats.Speed;
        Damage           = EntityStats.Damage;
        Armor            = EntityStats.Armor;
        Weight           = EntityStats.Weight;
        CanBeKnockedBack = EntityStats.UseKnockback;
        Tenacity         = EntityStats.Tenacity;
        MaxTenacity      = EntityStats.MaxTenacity;
        MaxStaggers      = EntityStats.MaxStaggers;
        CameraPriority   = EntityStats.CameraPriority;
        AttackDuration   = EntityStats.AttackDuration;
    }

    private void InitializeZAxis()
    {
        ZAxis = new ZAxisSystem
        {
            JumpImpulse         = JumpImpulse,
            JumpFallSpeed       = JumpFallSpeed,
            ShadowScaleWhenJump = ShadowScaleWhenJump
        };
        ZAxis.Initialize(this);
    }

    protected virtual StateMachine CreateStateMachine() => new StateMachine();

    private void EnsureStateMachine()
    {
        StateMachine = GetNodeOrNull<StateMachine>("StateMachine");

        if (StateMachine == null)
        {
            StateMachine = CreateStateMachine();
            StateMachine.Name = "StateMachine";
            AddChild(StateMachine);
        }
    }
}
