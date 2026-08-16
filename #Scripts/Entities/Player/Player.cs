using Godot;
using System;

public partial class Player : Entity
{
    public PlayerStats Stats { get; private set; }
    public new PlayerStateMachine StateMachine => (PlayerStateMachine)base.StateMachine;
    public ResourceManager ResourceManager { get; private set; }

    protected override StateMachine CreateStateMachine() => new PlayerStateMachine();
    public Weapon Weapon { get; set; }

    [Export] public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;

    private string lastAnimationDirection = "";
    private string lastDamageDirection = "S";
    private bool lastFlipH = false;

    private Vector2 bodySpriteBasePosition;

    protected override bool UsesDirectionalAnimations => true;

    public override void _Ready()
    {
        base._Ready();
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
        AddBehavior(new KnockbackBehavior());

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

        StateMachine.OnDied += OnPlayerDied;

        StateMachine.OnAttackStarted += (isHeavy) => PlayWeaponAttackAnimation(isHeavy);
        StateMachine.OnAttackStarted += (isHeavy) => ApplyWeaponPushback();

        StateMachine.OnAttackStarted += (isHeavy) => OnActionPerformed();
        StateMachine.OnDodgeStarted  += (direction) => OnActionPerformed();
        StateMachine.OnHealStarted   += (duration) => ResourceManager?.StartHealing();
        StateMachine.OnHealEnded     += () => ResourceManager?.EndHealing();

        SaveManager.ApplyTo(this);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        CameraPriority = Input.IsPhysicalKeyPressed(Key.Alt) ? 10f : 0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (StateMachine.IsDead) return;

        if (Input.IsActionJustPressed(Keybinds.Jump) && StateMachine.CanMove)
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
                SaveManager.Save(this, SavePoint.None);
            }
            else if (keyEvent.Keycode == Key.F4)
            {
                SaveManager.ReloadFromDisk();
            }
        }
    }
}
