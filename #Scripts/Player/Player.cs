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
    
    // Animation
    [Export] public Sprite2D BodySprite { get; set; }
    [Export] public AnimationPlayer AnimationPlayer { get; set; }
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
        WeaponSlot = GetNode<Node2D>("WEAPON");
        BodySprite = GetNode<Sprite2D>("Sprite2D");
        AnimationPlayer = GetNode<AnimationPlayer>("Animator");
        ResourceManager = new ResourceManager(this);
        StateMachine = new PlayerStateMachine();

        Weapon = WeaponSlot.GetChild<Weapon>(0);
 
        StateMachine.Name = "PlayerStateMachine";
        AddChild(StateMachine);
        
        StateMachine.OnAttackStarted += OnAttackStarted;
        StateMachine.OnAttackEnded += OnAttackEnded;
        StateMachine.OnDodgeStarted += OnDodgeStarted;
        StateMachine.OnDied += OnPlayerDied;

        StateMachine.OnAttackStarted += (isHeavy) => PlayWeaponAttackAnimation(isHeavy);
        
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
        if (Dodge.IsIFrameActive)
            return;

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
        if (AnimationPlayer == null || StateMachine == null) return;
        
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
        
        if (BodySprite != null)
            BodySprite.FlipH = flipH;

        foreach (string animationName in GetAnimationCandidates(currentState, direction))
        {
            if (AnimationPlayer.HasAnimation(animationName))
            {
                bool isAttackAnimation = animationName.StartsWith("Sword_Attack") || animationName.StartsWith("Attack");
                if (isAttackAnimation && Weapon != null)
                {
                    float desiredDuration = Weapon.GetAttackAnimationDuration(direction, currentState == PlayerState.HeavyAttacking);
                    float nativeLength = GetAnimationDuration(animationName);
                    float speedScale = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
                    AnimationPlayer.SpeedScale = speedScale;
                }
                else
                {
                    AnimationPlayer.SpeedScale = 1f;
                }

                if (PlayAnimation(animationName))
                {
                    // Mirror weapon animations (idle/move) to match the player
                    try
                    {
                        Weapon?.PlayStateAnimation(animationName);

                        int playerZ = BodySprite != null ? BodySprite.ZIndex : 0;
                        bool weaponAbove = direction == "Right" || direction == "Up";
                        if (Weapon != null)
                            Weapon.SetLayerRelativeToPlayer(playerZ, weaponAbove);
                    }
                    catch (Exception)
                    {
                        // Swallow any unexpected errors from optional weapon mirroring
                    }

                    return;
                }
            }
        }
    }

    private float GetAnimationDuration(string animationName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animationName))
            return 0.1f;

        Animation animation = AnimationPlayer.GetAnimation(animationName);
        if (animation == null)
            return 0.1f;

        return Mathf.Max(0.1f, (float)animation.Length);
    }
    
    private string[] GetAnimationCandidates(PlayerState state, string direction)
    {
        string idle = $"Weapon_Idle_{direction}";

        return state switch
        {
            PlayerState.Idle => new[] { idle },
            PlayerState.Moving => new[] { $"Weapon_Move_{direction}", idle },
            PlayerState.Attacking => new[] { $"Sword_Attack_{direction}", idle },
            PlayerState.HeavyAttacking => new[] { "Sword_Attack_Spin", $"Sword_Attack_{direction}", idle },
            PlayerState.Dodging => new[] { $"Weapon_Move_{direction}", idle },
            PlayerState.Healing => new[] { idle },
            PlayerState.Staggered => new[] { idle },
            PlayerState.Knockback => new[] { idle },
            PlayerState.Dead => new[] { "Weapon_Idle_Down", idle },
            _ => new[] { idle }
        };
    }

    private bool PlayAnimation(string animationName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animationName))
            return false;

        if (AnimationPlayer.CurrentAnimation != animationName || !AnimationPlayer.IsPlaying())
            AnimationPlayer.Play(animationName);

        return true;
    }

    private void PlayWeaponAttackAnimation(bool isHeavy)
    {
        if (Weapon == null) return;

        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        string direction = GetDirectionFromVector(toMouse, out _);

        Weapon.PlayAttackAnimation(direction, isHeavy);
    }

    public float GetCurrentAttackAnimationDuration(bool isHeavy)
    {
        if (AnimationPlayer == null || Weapon == null)
            return 0.5f;

        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        string direction = GetDirectionFromVector(toMouse, out _);

        return Weapon.GetAttackAnimationDuration(direction, isHeavy);
    }
}