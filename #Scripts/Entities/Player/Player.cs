using Godot;
using System;

public partial class Player : Entity
{
    public static Player Instance;
    public new PlayerStats Stats { get; private set; }
    public new PlayerStateMachine StateMachine => (PlayerStateMachine)base.StateMachine;
    public ResourceManager ResourceManager { get; private set; }

    protected override StateMachine CreateStateMachine() => new PlayerStateMachine();
    public TenacityBehavior TenacityBehavior { get; private set; }
    public Weapon Weapon { get; set; }

    [Export] public float DefaultStaggerDuration  { get; set; } = TenacityDefaults.DefaultStaggerDuration;
    [Export] public float DefaultRecoveryDuration { get; set; } = TenacityDefaults.DefaultRecoveryDuration;
    [Export] public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;

    private string lastAnimationDirection = "";
    private string lastDamageDirection = "S";
    private bool lastFlipH = false;

    private Vector2 bodySpriteBasePosition;

    protected override bool UsesDirectionalAnimations => true;

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
        Stats = new PlayerStats();
        Weapon = GetNodeOrNull<Weapon>("WEAPON");
        if (Weapon == null)
        {
            GD.PushError("Player: child node 'WEAPON' not found — creating an empty Weapon. Check Player.tscn.");
            Weapon = new Weapon();
            AddChild(Weapon);
        }
        ResourceManager = new ResourceManager(this);
        AddBehavior(ResourceManager);

        TenacityBehavior = new TenacityBehavior
        {
            DefaultStaggerDuration  = DefaultStaggerDuration,
            DefaultRecoveryDuration = DefaultRecoveryDuration,
            DefaultKnockbackDuration = DefaultKnockbackDuration,
            GetCurrentTenacity = () => Stats?.GetCurrent(StatType.Tenacity) ?? 0f,
            SetCurrentTenacity = value => Stats?.SetCurrent(StatType.Tenacity, value),
            GetMaxTenacity     = () => Stats?.GetCurrentMax(StatType.Tenacity) ?? 0f,
            SetMaxTenacity     = value => Stats?.SetCurrentMax(StatType.Tenacity, value)
        };
        AddBehavior(TenacityBehavior);

        var knockbackBehavior = new KnockbackBehavior
        {
            CanBeKnockedBack = CanBeKnockedBack,
            Weight           = Weight,
            KnockbackDecay   = KnockbackDecay
        };
        AddBehavior(knockbackBehavior);

        var dodgeBehavior = new DodgeBehavior();
        dodgeBehavior.HasStamina    = () => ResourceManager?.HasStamina(dodgeBehavior.DodgeStaminaCost) ?? false;
        dodgeBehavior.TryUseStamina = () => ResourceManager?.TryUseStamina(dodgeBehavior.DodgeStaminaCost) ?? false;
        AddBehavior(dodgeBehavior);

        var movementBehavior = new MovementBehavior
        {
            HealSpeedModifier = 0.2f,
            GetBaseSpeed      = () => Stats?.GetCurrentMax(StatType.Speed) ?? Speed,
            GetDodgeVelocity  = () => dodgeBehavior.GetDodgeVelocity()
        };
        AddBehavior(movementBehavior);
        AddBehavior(new PlayerInputBehavior());

        bodySpriteBasePosition = Sprite?.Position ?? Vector2.Zero;

        StateMachine.OnAttackStarted += OnAttackStarted;
        StateMachine.OnAttackEnded   += OnAttackEnded;
        StateMachine.OnDodgeStarted  += OnDodgeStarted;
        StateMachine.OnDied          += OnPlayerDied;

        StateMachine.OnAttackStarted += (isHeavy) => PlayWeaponAttackAnimation(isHeavy);
        StateMachine.OnAttackStarted += (isHeavy) => ApplyWeaponPushback();

        StateMachine.OnAttackStarted += (isHeavy) => OnActionPerformed();
        StateMachine.OnDodgeStarted  += (direction) => OnActionPerformed();
        StateMachine.OnHealStarted   += (duration) => ResourceManager?.StartHealing(duration);
        StateMachine.OnHealEnded     += () => ResourceManager?.EndHealing();

        SaveSystem.ApplyLoadedData();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        CameraPriority = Input.IsPhysicalKeyPressed(Key.Alt) ? 10f : 0f;

        if (Weapon != null)
            Weapon.Visible = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (StateMachine.IsDead) return;

        if (Input.IsActionJustPressed(Keybinds.Jump) && CanMove)
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
