using Godot;
using System;

public partial class Player : Entity
{
    public static Player Instance;
    public PlayerStats Stats { get; private set; }
    public ResourceManager ResourceManager { get; private set; }
    public TenacityBehavior TenacityBehavior { get; private set; }
    public Weapon Weapon { get; set; }

    [Export] public float DefaultStaggerDuration { get; set; } = TenacityDefaults.DefaultStaggerDuration;
    [Export] public float DefaultRecoveryDuration { get; set; } = TenacityDefaults.DefaultRecoveryDuration;
    [Export] public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;
    
    public Sprite2D Sprite { get; set; }
    private string lastAnimationDirection = "";
    private string _lastDamageDirection = "S";
    private bool _lastFlipH = false;

    private Vector2 _bodySpriteBasePosition;
    
    protected override bool UsesDirectionalAnimations => true;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
        Stats = new PlayerStats();
        InitializeEntity.InitializeNodes(this);
        Sprite = GetNode<Sprite2D>("Sprite");
        Weapon = GetNodeOrNull<Weapon>("WEAPON") ?? new Weapon();
        if (Weapon.GetParent() == null)
            AddChild(Weapon);
        ResourceManager = new ResourceManager(this);
        AddBehavior(ResourceManager);

        TenacityBehavior = new TenacityBehavior
        {
            DefaultStaggerDuration = DefaultStaggerDuration,
            DefaultRecoveryDuration = DefaultRecoveryDuration,
            DefaultKnockbackDuration = DefaultKnockbackDuration,
            GetCurrentTenacity = () => Stats?.GetCurrent("Tenacity") ?? 0f,
            SetCurrentTenacity = value => Stats?.SetCurrent("Tenacity", value),
            GetMaxTenacity = () => Stats?.GetCurrentMax("Tenacity") ?? 0f,
            SetMaxTenacity = value => Stats?.SetCurrentMax("Tenacity", value)
        };
        AddBehavior(TenacityBehavior);

        var knockbackBehavior = new KnockbackBehavior
        {
            CanBeKnockedBack = canBeKnockedBack,
            Weight = weight,
            KnockbackDecay = knockbackDecay
        };
        AddBehavior(knockbackBehavior);

        var dodgeBehavior = new DodgeBehavior();
        dodgeBehavior.HasStamina = () => ResourceManager?.HasStamina(dodgeBehavior.DodgeStaminaCost) ?? false;
        dodgeBehavior.TryUseStamina = () => ResourceManager?.TryUseStamina(dodgeBehavior.DodgeStaminaCost) ?? false;
        AddBehavior(dodgeBehavior);

        var movementBehavior = new MovementBehavior
        {
            HealSpeedModifier = 0.2f,
            GetBaseSpeed = () => Stats?.GetCurrentMax("Speed") ?? speed,
            GetDodgeVelocity = () => dodgeBehavior.GetDodgeVelocity()
        };
        AddBehavior(movementBehavior);
        AddBehavior(new PlayerInputBehavior());

        _bodySpriteBasePosition = Sprite?.Position ?? Vector2.Zero;

        StateMachine.OnAttackStarted += OnAttackStarted;
        StateMachine.OnAttackEnded += OnAttackEnded;
        StateMachine.OnDodgeStarted += OnDodgeStarted;
        StateMachine.OnDied += OnPlayerDied;

        StateMachine.OnAttackStarted += (isHeavy) => PlayWeaponAttackAnimation(isHeavy);
        StateMachine.OnAttackStarted += (isHeavy) => ApplyWeaponPushback();
        
        StateMachine.OnAttackStarted += (isHeavy) => OnActionPerformed();
        StateMachine.OnDodgeStarted += (direction) => OnActionPerformed();
        StateMachine.OnHealStarted += (duration) => ResourceManager?.StartHealing(duration);
        StateMachine.OnHealEnded += () => ResourceManager?.EndHealing();

        SaveSystem.ApplyLoadedData();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        cameraPriority = Input.IsPhysicalKeyPressed(Key.Alt) ? 10f : 0f;

        if (Weapon != null)
            Weapon.Visible = true;
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
            else if (keyEvent.Keycode == Key.F3)
            {
                SaveSystem.SaveGame();
            }
            else if (keyEvent.Keycode == Key.F4)
            {
                SaveSystem.LoadGame();
            }
        }
    }

}