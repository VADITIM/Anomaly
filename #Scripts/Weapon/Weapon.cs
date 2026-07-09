using Godot;
using System.Collections.Generic;

public partial class Weapon : Node2D
{
    private WeaponArc _currentArc;
    private Sprite2D _weaponSprite;
    private AnimationPlayer _animationPlayer;
    private Area2D _hitbox;
    private StateMachine _ownerStateMachine;

    public Sprite2D WeaponSprite => _weaponSprite;
    public AnimationPlayer AnimationPlayer => _animationPlayer;
    public Area2D Hitbox => _hitbox;
    public WeaponArc CurrentArc => _currentArc;

    private WeaponStats _weaponStats = new WeaponStats();
    public WeaponStats Stats => _weaponStats;

    private float[] _attackDurations = new float[4] { 0.37f, 0.45f, 0.35f, 0.6f };
    private float[] _attackDamageMultipliers = new float[4] { 1f, 1.15f, 1.3f, 1.5f };
    private int _specialHitInterval = 4;
    private int _hitCount = 0;
    private float _currentTenacityDamageMultiplier = 1f;

    private int _attackSequenceIndex = 0;
    private const int MaxComboSteps = 4;

    private const float ComboFollowUpWindow = 0.2f;
    private const float ComboFinisherCooldown = 0.5f;
    private float _comboWindowTimer = 0f;
    private float _comboCooldownTimer = 0f;

    private bool _queuedAttackFollowUp = false;

    // One damage application per entity per swing, even when both the body and
    // its Hurtbox overlap the hitbox during the same attack.
    private readonly HashSet<ulong> _entitiesHitThisSwing = new();

    public void UnslotArc() { _currentArc = null; }
    public WeaponArc GetCurrentArc() { return _currentArc; }

    public float Damage { get => _weaponStats.GetCurrent(WeaponStatType.Damage); set => _weaponStats.SetCurrent(WeaponStatType.Damage, value); }
    public float StaminaCost { get => _weaponStats.GetCurrent(WeaponStatType.StaminaCost); set => _weaponStats.SetCurrent(WeaponStatType.StaminaCost, value); }
    public float TenacityDamage { get => _weaponStats.GetCurrent(WeaponStatType.TenacityDamage); set => _weaponStats.SetCurrent(WeaponStatType.TenacityDamage, Mathf.Clamp(value, 0f, 100f)); }
    public float StaminaRestore { get => _weaponStats.GetCurrent(WeaponStatType.StaminaRestore); set => _weaponStats.SetCurrent(WeaponStatType.StaminaRestore, Mathf.Clamp(value, 0f, 10f)); }
    public float Penetration { get => _weaponStats.GetCurrent(WeaponStatType.Penetration); set => _weaponStats.SetCurrent(WeaponStatType.Penetration, Mathf.Clamp(value, 0f, 100f)); }
    public int SpecialHitInterval { get => _specialHitInterval; set => _specialHitInterval = value; }
    public int HitCount { get => _hitCount; set => _hitCount = value; }
    public float CurrentTenacityDamageMultiplier { get => _currentTenacityDamageMultiplier; set => _currentTenacityDamageMultiplier = value; }
    public int CurrentAttackSequenceIndex => _attackSequenceIndex;

    public bool CanQueueAttackFollowUp => _comboWindowTimer > 0f;

    public bool IsInComboCooldown => _comboCooldownTimer > 0f;

    private Player OwnerPlayer => OwnerStateMachine?.OwnerEntity as Player;

    private StateMachine OwnerStateMachine
    {
        get
        {
            _ownerStateMachine ??= FindOwnerStateMachine();
            return _ownerStateMachine;
        }
    }

    private StateMachine FindOwnerStateMachine()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is Entity entity && entity.StateMachine != null)
                return entity.StateMachine;

            current = current.GetParent();
        }

        return null;
    }

    public override void _Ready()
    {
        _weaponSprite = GetNodeOrNull<Sprite2D>("Scythe");
        _hitbox = GetNodeOrNull<Area2D>("Hitbox Area");
        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer")
                        ?? GetNodeOrNull<AnimationPlayer>("Animation Player")
                        ?? GetNodeOrNull<AnimationPlayer>("Animator");

        if (_animationPlayer == null)
            GD.PushWarning($"{Name}: no AnimationPlayer child found — weapon will not animate.");

        if (_hitbox != null)
        {
            _hitbox.BodyEntered += OnEnemyHit;
            _hitbox.BodyEntered += OnPropHit;
            _hitbox.AreaEntered += OnHurtboxHit;
            _hitbox.Monitoring = false;
        }

        ScytheArc scytheArc = new ScytheArc();
        AddChild(scytheArc);
        SlotArc(scytheArc);
    }

    public override void _Process(double delta)
    {
        UpdateComboTimers((float)delta);
        if (_hitbox != null)
            _hitbox.Monitoring = OwnerStateMachine?.IsAttacking ?? false;
    }

    public void SlotArc(WeaponArc newArc)
    {
        if (newArc == null)
            return;

        _currentArc = newArc;
        _currentArc.SetParentWeapon(this);
    }

    public void SetLayerRelativeToPlayer(int playerZIndex, bool above)
    {
        int offset = above ? 1 : -1;
        this.ZIndex = playerZIndex + offset;
    }

    private bool RegisterSwingHit(Entity targetEntity)
    {
        return _entitiesHitThisSwing.Add(targetEntity.GetInstanceId());
    }

    private void OnHurtboxHit(Area2D area)
    {
        if (area is not Hurtbox hurtbox)
            return;

        Entity targetEntity = hurtbox.OwnerEntity;
        if (targetEntity == null || targetEntity == OwnerPlayer || !RegisterSwingHit(targetEntity))
            return;

        bool wasStaggered = targetEntity is Enemy staggeredEnemy && staggeredEnemy.IsStaggered;
        targetEntity.TakeDamage(_currentArc.Damage, GlobalPosition, _currentArc);
        if (wasStaggered)
            ApplyStaggerHitStaminaRestore(_currentArc.StaminaRestore);
        _currentArc.TriggerHitAnimation();
    }

    private void OnEnemyHit(Node2D body)
    {
        if (body is not Enemy enemy || !RegisterSwingHit(enemy))
            return;

        bool wasStaggered = enemy.IsStaggered;
        enemy.TakeDamage(_currentArc.Damage, GlobalPosition, _currentArc);
        if (wasStaggered)
            ApplyStaggerHitStaminaRestore(_currentArc.StaminaRestore);
        _currentArc.TriggerHitAnimation();
    }

    private void OnPropHit(Node2D body)
    {
        if (body is not Prop prop || !RegisterSwingHit(prop))
            return;

        prop.TakeDamage(_currentArc.Damage, GlobalPosition, _currentArc);
        _currentArc.TriggerHitAnimation();
    }

    private void ApplyStaggerHitStaminaRestore(float restoreAmount)
    {
        Player player = OwnerPlayer;
        if (player == null || restoreAmount <= 0f)
            return;

        player.ResourceManager.SetStamina(player.ResourceManager.Stamina + restoreAmount);
    }
}
