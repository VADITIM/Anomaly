using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
    public static Player Instance;
    public PlayerStateMachine StateMachine { get; private set; }
    public ResourceManager ResourceManager { get; private set; }
    [Export]public Weapon Weapon { get; set; }
    [Export] public Node2D WeaponSlot;
    [Export] public Node2D WeaponSprite;
    
    // Animation
    [Export]public AnimatedSprite2D AnimatedSprite { get; set; }
    private PlayerState lastAnimation = (PlayerState)(-1);
    private string lastAnimationDirection = "";

    public PlayerStats Stats { get; private set; }
    private float _staminaRegenerationCooldown = 0f;
    public float STAMINA_REGEN_COOLDOWN = 1.5f;

    public static bool canMove
    {
        get => PlayerStateMachine.Instance?.CanMove ?? true;
        set { if (PlayerStateMachine.Instance != null) PlayerStateMachine.Instance.CanMove = value; }
    }
    public static bool canAttack
    {
        get => PlayerStateMachine.Instance?.CanAttack ?? true;
        set { if (PlayerStateMachine.Instance != null) PlayerStateMachine.Instance.CanAttack = value; }
    }
    public static bool isPaused
    {
        get => PlayerStateMachine.Instance?.IsPaused ?? false;
        set { if (PlayerStateMachine.Instance != null) PlayerStateMachine.Instance.IsPaused = value; }
    }

    public void OnActionPerformed() { _staminaRegenerationCooldown = STAMINA_REGEN_COOLDOWN; }
    private void OnAttackStarted(bool isHeavy) { }
    private void OnAttackEnded() { }
    private void OnDodgeStarted(Vector2 direction) { }
    private void OnPlayerDied() { }

    public override void _Ready()
    {
        Instance = this;
        Stats = new PlayerStats();
        WeaponSlot = GetNode<Node2D>("Weapon Slot");
        AnimatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        ResourceManager = new ResourceManager(this);
        StateMachine = new PlayerStateMachine();

        Weapon = WeaponSlot.GetChild<Weapon>(0);
        StateMachine.Name = "PlayerStateMachine";
        AddChild(StateMachine);
        
        StateMachine.OnAttackStarted += OnAttackStarted;
        StateMachine.OnAttackEnded += OnAttackEnded;
        StateMachine.OnDodgeStarted += OnDodgeStarted;
        StateMachine.OnDied += OnPlayerDied;

        StateMachine.OnAttackStarted += (isHeavy) => Weapon?.PlayAttackAnimation();
        
        StateMachine.OnAttackStarted += (isHeavy) => OnActionPerformed();
        StateMachine.OnDodgeStarted += (direction) => OnActionPerformed();
    }

    public override void _Process(double delta)
    {
        if (Dodge.IsDodging())
        {
            Dodge.UseStamina();
        }

        if (WeaponSlot != null)
            WeaponSlot.Visible = true;
        
        UpdateAnimation();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (StateMachine.IsDead) return;
        
        PassiveStaminaRegeneration((float)delta);

        Player.Instance.ProcessMovement((float)delta);
        ProcessCombat((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F1)
            {
                // Debug key - to be implemented
            }
            else if (keyEvent.Keycode == Key.F2)
            {
                // Debug key - to be implemented
            }
        }
    }

    private void ProcessCombat(float delta)
    {
        if (Weapon == null || Weapon.Hitbox == null) return;
        
        bool shouldMonitor = StateMachine.IsAttacking;
        Weapon.Hitbox.Monitoring = shouldMonitor;
    }
    
    private void PassiveStaminaRegeneration(float delta)
    {
        _staminaRegenerationCooldown -= delta;

        if (_staminaRegenerationCooldown > 0) return;

        float currentStamina = Stats.GetCurrent("Stamina");
        float maxStamina = Stats.GetCurrentMax("Stamina");
        float regenRate = Stats.GetCurrentMax("Stamina Regen");
        
        if (currentStamina < maxStamina)
        {
            float newStamina = Mathf.Min(currentStamina + regenRate * delta, maxStamina);
            Stats.SetCurrent("Stamina", newStamina);
        }
    }

    public void TakeDamage(float damage, Vector2 sourcePosition)
    {
        float armor = Stats.GetCurrentMax("Armor");
        float effectiveDamage = damage * (1f - armor / 100f);
        Vector2 knockbackDir = (GlobalPosition - sourcePosition).Normalized();
        
        float currentHealth = Stats.GetCurrentMax("Health");
        float newHealth = currentHealth - effectiveDamage;
        
        // Update health through ResourceManager or PlayerStats
        if (newHealth <= 0)
            StateMachine.RequestDeath();
        else
            StateMachine.RequestKnockback(knockbackDir, 200f, 0.2f);
    }



   
    private string GetDirectionFromAngle(float angleDegrees, out bool flipH)
    {
        while (angleDegrees > 180) angleDegrees -= 360;
        while (angleDegrees < -180) angleDegrees += 360;

        flipH = false;

        if (angleDegrees >= -45f && angleDegrees < 45f)
            return "Right";
        if (angleDegrees >= 45f && angleDegrees < 135f)
            return "Down";
        if (angleDegrees >= -135f && angleDegrees < -45f)
            return "Up";

        return "Left";
    }
    
    private bool _lastFlipH = false;
    
    private string GetDirectionFromVector(Vector2 direction, out bool flipH)
    {
        flipH = false;
        if (direction == Vector2.Zero)
            return "Down"; 
        
        float angle = Mathf.RadToDeg(direction.Angle());
        return GetDirectionFromAngle(angle, out flipH);
    }
    
    private void UpdateAnimation()
    {
        if (AnimatedSprite == null || StateMachine == null) return;
        
        PlayerState currentState = StateMachine.CurrentState;
        string direction;
        bool flipH;
        
        if (currentState == PlayerState.Dodging)
        {
            Vector2 dodgeVel = Dodge.GetDodgeVelocity();
            direction = GetDirectionFromVector(dodgeVel, out flipH);
        }
        else
        {
            Vector2 mousePos = GetGlobalMousePosition();
            Vector2 toMouse = mousePos - GlobalPosition;
            direction = GetDirectionFromVector(toMouse, out flipH);
        }
        
        if (currentState == lastAnimation && direction == lastAnimationDirection && flipH == _lastFlipH)
            return;
        
        lastAnimation = currentState;
        lastAnimationDirection = direction;
        _lastFlipH = flipH;
        
        AnimatedSprite.FlipH = flipH;
        
        string stateName = GetStateAnimationName(currentState);

        if (!TryPlayStateAnimation(stateName, direction, currentState))
        {
            TryPlayStateAnimation("Idle", direction, PlayerState.Idle);
        }
    }
    
    private string GetStateAnimationName(PlayerState state)
    {
        return state switch
        {
            PlayerState.Idle => "Idle",
            PlayerState.Moving => "Move",
            PlayerState.Dodging => "Dodge",
            PlayerState.Attacking => "Attack",
            PlayerState.HeavyAttacking => "Attack",
            PlayerState.Healing => "Heal",
            PlayerState.Knockback => "Knockback",
            PlayerState.Dead => "Dead",
            _ => "Idle"
        };
    }

    private bool TryPlayStateAnimation(string stateName, string direction, PlayerState state)
    {
        if (AnimatedSprite == null) return false;

        string[] candidates;
        if (state == PlayerState.Attacking || state == PlayerState.HeavyAttacking)
        {
            candidates = new[]
            {
                $"{stateName}_{direction}",
                $"{stateName}{direction}",
                $"Idle_{direction}",
                $"Idle{direction}"
            };
        }
        else
        {
            candidates = new[]
            {
                $"{stateName}_{direction}",
                $"{stateName}{direction}"
            };
        }

        foreach (string animationName in candidates)
        {
            if (PlayAnimation(animationName))
            {
                return true;
            }
        }

        return false;
    }
    
    private bool PlayAnimation(string animationName)
    {
        if (AnimatedSprite == null) return false;
        
        var spriteFrames = AnimatedSprite.SpriteFrames;
        
        if (spriteFrames.HasAnimation(animationName))
        {
            AnimatedSprite.Play(animationName);
            return true;
        }

        return false;
    }
}