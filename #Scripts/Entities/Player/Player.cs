using Godot;
using System;

public partial class Player : Entity
{
    public enum MovementDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8
    }

    public static Player Instance;
    public PlayerStateMachine StateMachine { get; private set; }
    public ResourceManager ResourceManager { get; private set; }
    public Weapon Weapon { get; set; }
    [Export] public Node2D WeaponSlot;
    
    [Export] public Sprite2D BodySprite { get; set; }
    private PlayerState lastAnimation = (PlayerState)(-1);
    private string lastAnimationDirection = "";
    private bool _lastAirborne = false;
    private string _lastDamageDirection = "Down";

    private Vector2 _bodySpriteBasePosition;
    private Vector2 _weaponSlotBasePosition;

    public PlayerStats Stats { get; private set; }
    private float _staminaRegenerationCooldown = 0f;
    public float STAMINA_REGEN_COOLDOWN = 1.5f;

    private bool wasAirborneLastFrame = false;

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
    private void OnAttackStarted(bool isHeavy)
    {
        lastAnimation = (PlayerState)(-1);
    }
    private void OnAttackEnded() { }
    private void OnDodgeStarted(Vector2 direction) { }
    private void OnPlayerDied() { }

    protected override bool CanTakeDamage(float damage, Vector2 sourcePosition)
    {
        return !Dodge.IsIFrameActive;
    }

    protected override float GetHealth() => Stats?.GetCurrent("Health") ?? base.GetHealth();

    protected override float GetMaxHealth() => Stats?.GetCurrentMax("Health") ?? base.GetMaxHealth();

    protected override void SetHealth(float value)
    {
        if (Stats != null)
            Stats.SetCurrent("Health", value);
        else
            base.SetHealth(value);
    }

    protected override void SetMaxHealth(float value)
    {
        if (Stats != null)
            Stats.SetCurrentMax("Health", value);
        else
            base.SetMaxHealth(value);
    }

    protected override float ApplyDamageModifiers(float damage, Vector2 sourcePosition)
    {
        float armor = Stats?.GetCurrentMax("Armor") ?? 0f;
        return damage * (1f - armor / 100f);
    }

    protected override void OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth)
    {
        _lastDamageDirection = GetDirectionFromVector(sourcePosition - GlobalPosition, out _);

        if (newHealth <= 0f)
            return;

        Vector2 knockbackDir = (GlobalPosition - sourcePosition).Normalized();
        StateMachine.RequestKnockback(knockbackDir, 200f, 0.2f);
    }

    protected override void OnDeath(Vector2 sourcePosition)
    {
        StateMachine.RequestDeath();
    }

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
        Stats = new PlayerStats();
        WeaponSlot = GetNode<Node2D>("WEAPON");
        BodySprite = GetNode<Sprite2D>("Sprite");
        AnimationPlayer = GetNode<AnimationPlayer>("Animator");
        ResourceManager = new ResourceManager(this);
        StateMachine = new PlayerStateMachine();

        Node firstChild = WeaponSlot.GetChildCount() > 0 ? WeaponSlot.GetChild(0) : null;
        if (firstChild is Weapon weaponNode)
        {
            Weapon = weaponNode;
        }
        else if (firstChild is WeaponArc arcNode)
        {
            // Load weapon from scene file
            PackedScene weaponScene = GD.Load<PackedScene>("res://#Scenes/Entities/Weapon.tscn");
            Weapon weaponContainer = weaponScene.Instantiate<Weapon>();
            // Move existing arc under new container and slot it
            WeaponSlot.RemoveChild(arcNode);
            weaponContainer.AddChild(arcNode);
            weaponContainer.SlotArc(arcNode);  // Slot the arc into the container
            WeaponSlot.AddChild(weaponContainer);
            Weapon = weaponContainer;
        }
        else
        {
            // No valid child found; load weapon from scene file
            PackedScene weaponScene = GD.Load<PackedScene>("res://#Scenes/Entities/Weapon.tscn");
            Weapon weaponContainer = weaponScene.Instantiate<Weapon>();
            WeaponSlot.AddChild(weaponContainer);
            Weapon = weaponContainer;
        }

    _bodySpriteBasePosition = BodySprite?.Position ?? Vector2.Zero;
    _weaponSlotBasePosition = WeaponSlot?.Position ?? Vector2.Zero;
 
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
        base._PhysicsProcess(delta);
        if (StateMachine.IsDead) return;
        
        if (Input.IsActionJustPressed(Keybinds.Jump) && canMove)
        {
            Jump();
        }

        PassiveStaminaRegeneration((float)delta);
        Player.Instance.ProcessMovement((float)delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F1)
            {
            }
            else if (keyEvent.Keycode == Key.F2)
            {
            }
        }
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
        else if (currentState == PlayerState.Staggered || currentState == PlayerState.Knockback || currentState == PlayerState.Dead)
        {
            direction = _lastDamageDirection;
            flipH = false;
        }
        else if (StateMachine.IsAirborne)
        {
            direction = "Down";
            flipH = false;
        }
        else if (currentState == PlayerState.Attacking || currentState == PlayerState.HeavyAttacking)
        {
            direction = lastAnimationDirection;
            flipH = _lastFlipH;
        }
        else
        {
            Vector2 mousePos = GetGlobalMousePosition();
            Vector2 toMouse = mousePos - GlobalPosition;
            direction = GetDirectionFromVector(toMouse, out flipH);
        }
        
        if (currentState == lastAnimation && direction == lastAnimationDirection && flipH == _lastFlipH == _lastAirborne)
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
                bool isAttackAnimation = animationName.StartsWith("Attack") ||
                                         animationName.StartsWith("attack") ||
                                         animationName == "Attack_Spin";
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

                if (PlayPlayerAnimation(animationName))
                {
                    Weapon?.PlayStateAnimation(animationName);

                    int playerZ = BodySprite != null ? BodySprite.ZIndex : 0;
                    bool weaponAbove = direction == "Right" || direction == "Up";
                    if (Weapon != null)
                        Weapon.SetLayerRelativeToPlayer(playerZ, weaponAbove);

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
        string idle = $"Idle_{direction}";

        return state switch
        {
            PlayerState.Moving => new[] { $"Move_{direction}", idle },
            PlayerState.Airborne => new[] { "Jump_Down", idle },
            PlayerState.Attacking => new[] { $"Attack_{direction}_1", $"attack_{direction}_1", $"Attack_{direction}", idle },
            PlayerState.HeavyAttacking => new[] { "Attack_Spin", $"Attack_{direction}", idle },
            PlayerState.AirAttacking => new[] { $"Air_Attack_{direction}", $"Attack_{direction}_1", $"Attack_{direction}", idle },
            PlayerState.Dodging => new[] { $"Dodge_{direction}", $"Move_{direction}", idle },
            PlayerState.Healing => new[] { idle },
            PlayerState.Staggered => new[] { $"Take_Damage_{direction}", idle },
            PlayerState.Knockback => new[] { $"Take_Damage_{direction}", idle },
            PlayerState.Dead => new[] { "Die", "Idle_Down", idle },
            _ => new[] { idle }
        };
    }

    private bool PlayPlayerAnimation(string animationName)
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