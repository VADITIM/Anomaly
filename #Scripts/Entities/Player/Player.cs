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
    public ResourceManager ResourceManager { get; private set; }
    public Weapon Weapon { get; set; }
    [Export] public Node2D WeaponSlot;
    
    [Export] public Sprite2D BodySprite { get; set; }
    private string lastAnimationDirection = "";
    private string _lastDamageDirection = "Down";
    private bool _lastFlipH = false;

    private Vector2 _bodySpriteBasePosition;
    private Vector2 _weaponSlotBasePosition;

    public PlayerStats Stats { get; private set; }
    private float _staminaRegenerationCooldown = 0f;
    public float STAMINA_REGEN_COOLDOWN = 1.5f;

    public static bool canMove
    {
        get => Player.Instance?.StateMachine?.CanMove ?? true;
        set { if (Player.Instance?.StateMachine != null) Player.Instance.StateMachine.CanMove = value; }
    }
    public static bool canAttack
    {
        get => Player.Instance?.StateMachine?.CanAttack ?? true;
        set { if (Player.Instance?.StateMachine != null) Player.Instance.StateMachine.CanAttack = value; }
    }
    public static bool isPaused
    {
        get => Player.Instance?.StateMachine?.IsPaused ?? false;
        set { if (Player.Instance?.StateMachine != null) Player.Instance.StateMachine.IsPaused = value; }
    }

    public void OnActionPerformed() { _staminaRegenerationCooldown = STAMINA_REGEN_COOLDOWN; }
    private void OnAttackStarted(bool isHeavy)
    {
    }
    private void OnAttackEnded() { }
    private void OnDodgeStarted(Vector2 direction) { }
    private void OnPlayerDied() { }

    protected override bool CanTakeDamage(float damage, Vector2 sourcePosition)
    {
        return !Dodge.IsIFrameActive;
    }

    public override void TakeDamage(float damage, Vector2 sourcePosition)
    {
        if (StateMachine != null && StateMachine.IsDead)
            return;

        base.TakeDamage(damage, sourcePosition);
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

        Node firstChild = WeaponSlot.GetChildCount() > 0 ? WeaponSlot.GetChild(0) : null;
        if (firstChild is Weapon weaponNode)
        {
            Weapon = weaponNode;
        }
        else if (firstChild is WeaponArc arcNode)
        {
            PackedScene weaponScene = GD.Load<PackedScene>("res://#Scenes/Entities/Weapon.tscn");
            Weapon weaponContainer = weaponScene.Instantiate<Weapon>();
            WeaponSlot.RemoveChild(arcNode);
            weaponContainer.AddChild(arcNode);
            weaponContainer.SlotArc(arcNode);
            WeaponSlot.AddChild(weaponContainer);
            Weapon = weaponContainer;
        }
        else
        {
            PackedScene weaponScene = GD.Load<PackedScene>("res://#Scenes/Entities/Weapon.tscn");
            Weapon weaponContainer = weaponScene.Instantiate<Weapon>();
            WeaponSlot.AddChild(weaponContainer);
            Weapon = weaponContainer;
        }

        _bodySpriteBasePosition = BodySprite?.Position ?? Vector2.Zero;
        _weaponSlotBasePosition = WeaponSlot?.Position ?? Vector2.Zero;

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
        base._Process(delta);

        if (Dodge.IsDodging())
        {
            Dodge.UseStamina();
        }

        WeaponSlot.Visible = true;
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
                float debugDamage = 10f;
                Vector2 sourcePos = GlobalPosition + new Vector2(0f, -50f);
                TakeDamage(debugDamage, sourcePos);
            }
            else if (keyEvent.Keycode == Key.F2)
            {
                float debugHeal = 10f;
                SetHealth(GetHealth() + debugHeal);
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

    protected override bool UsesDirectionalAnimations => true;

    protected override State GetCurrentAnimationState()
    {
        return StateMachine?.CurrentState ?? State.Idle;
    }

    protected override string GetCurrentAnimationDirection(bool useDirectionalAnimations, out bool flipH)
    {
        State currentState = StateMachine?.CurrentState ?? State.Idle;

        if (currentState == PlayerState.Dodging)
        {
            Vector2 dodgeVel = Dodge.GetDodgeVelocity();
            return GetDirectionFromVector(dodgeVel, out flipH);
        }

        if (currentState == PlayerState.Staggered || currentState == PlayerState.Knockback || currentState == PlayerState.Dead)
        {
            flipH = false;
            return _lastDamageDirection;
        }

        if (StateMachine != null && StateMachine.IsAirborne)
        {
            flipH = false;
            return "Down";
        }

        if (currentState == PlayerState.Attacking || currentState == PlayerState.HeavyAttacking)
        {
            flipH = _lastFlipH;
            return lastAnimationDirection;
        }

        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        return GetDirectionFromVector(toMouse, out flipH);
    }

    protected override void ApplyFacing(bool flipH)
    {
        BodySprite.FlipH = flipH;
    }

    protected override void OnAnimationPlayed(string animationName, State state, string direction, bool flipH)
    {
        lastAnimationDirection = direction;

        if (animationName.StartsWith("Attack") ||
            animationName.StartsWith("attack") ||
            animationName == "Attack_Spin")
        {
            float desiredDuration = Weapon.GetAttackAnimationDuration(direction, state == PlayerState.HeavyAttacking);
            float nativeLength = Mathf.Max(0.1f, (float)AnimationPlayer.GetAnimation(animationName).Length);

            AnimationPlayer.SpeedScale = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        }

        Weapon.PlayStateAnimation(animationName);

        int playerZ = BodySprite.ZIndex;
        bool weaponAbove = direction == "Right" || direction == "Up";
        Weapon.SetLayerRelativeToPlayer(playerZ, weaponAbove);
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
    
    private string GetDirectionFromVector(Vector2 direction, out bool flipH)
    {
        flipH = false;
        if (direction == Vector2.Zero)
            return "Down"; 
        
        float angle = Mathf.RadToDeg(direction.Angle());
        return GetDirectionFromAngle(angle, out flipH);
    }
    
    private void PlayWeaponAttackAnimation(bool isHeavy)
    {
        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        string direction = GetDirectionFromVector(toMouse, out _);

        Weapon.PlayAttackAnimation(direction, isHeavy);
    }

    public float GetCurrentAttackAnimationDuration(bool isHeavy)
    {
        if (AnimationPlayer == null)
            return 0.5f;

        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        string direction = GetDirectionFromVector(toMouse, out _);

        return Weapon.GetAttackAnimationDuration(direction, isHeavy);
    }

    public override float GetAttackDuration()
    {
        return GetCurrentAttackAnimationDuration(false);
    }

}