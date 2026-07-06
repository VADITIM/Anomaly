using Godot;


public partial class Weapon : Node2D
{
    private WeaponArc currentArc;
    private Sprite2D weaponSprite;
    public Sprite2D WeaponSprite => weaponSprite;
    [Export] private AnimationPlayer weaponAnimationPlayer;
    public AnimationPlayer AnimationPlayer => weaponAnimationPlayer;
    private Area2D weaponHitbox;
    public Area2D Hitbox => weaponHitbox;
    public WeaponArc CurrentArc => currentArc;

    private WeaponStats weaponStats = new WeaponStats();
    public WeaponStats Stats => weaponStats;

    private float[] attackDurations = new float[4] { 0.37f, 0.45f, 0.35f, 0.6f };
    private float[] attackDamageMultipliers = new float[4] { 1f, 1.15f, 1.3f, 1.5f };
    private int specialHitInterval = 4;
    private int hitCount = 0;
    private float currentTenacityDamageMultiplier = 1f;
    private float outsideKnockbackForce = 1f;

    private int attackSequenceIndex = 0;
    private const int MaxComboSteps = 4;

    private const float ComboFollowUpWindow = 0.2f;
    private const float ComboFinisherCooldown = 0.5f;
    private float comboWindowTimer = 0f;
    private float comboCooldownTimer = 0f;

    private bool queuedAttackFollowUp = false;

    public void UnslotArc() { currentArc = null; }
    public WeaponArc GetCurrentArc() { return currentArc; }

    public float Damage { get => weaponStats.GetCurrent(WeaponStatType.Damage); set => weaponStats.SetCurrent(WeaponStatType.Damage, value); }
    public float StaminaCost { get => weaponStats.GetCurrent(WeaponStatType.StaminaCost); set => weaponStats.SetCurrent(WeaponStatType.StaminaCost, value); }
    public float TenacityDamage { get => weaponStats.GetCurrent(WeaponStatType.TenacityDamage); set => weaponStats.SetCurrent(WeaponStatType.TenacityDamage, Mathf.Clamp(value, 0f, 100f)); }
    public float StaminaRestore { get => weaponStats.GetCurrent(WeaponStatType.StaminaRestore); set => weaponStats.SetCurrent(WeaponStatType.StaminaRestore, Mathf.Clamp(value, 0f, 10f)); }
    public float Penetration { get => weaponStats.GetCurrent(WeaponStatType.Penetration); set => weaponStats.SetCurrent(WeaponStatType.Penetration, Mathf.Clamp(value, 0f, 100f)); }
    public int SpecialHitInterval { get => specialHitInterval; set => specialHitInterval = value; }
    public int HitCount { get => hitCount; set => hitCount = value; }
    public float CurrentTenacityDamageMultiplier { get => currentTenacityDamageMultiplier; set => currentTenacityDamageMultiplier = value; }
    public float OutsideKnockbackForce { get => outsideKnockbackForce; set => outsideKnockbackForce = value; }
    public float PlayerPushForce  { get => currentArc?.PlayerPushForce ?? 0f; set { if (currentArc != null) currentArc.PlayerPushForce = value; }  }
    public int CurrentAttackSequenceIndex => attackSequenceIndex;

    public bool CanQueueAttackFollowUp => comboWindowTimer > 0f;

    public bool IsInComboCooldown => comboCooldownTimer > 0f;


    private StateMachine FindOwnerStateMachine()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is Entity entity && entity.StateMachine != null)
                return entity.StateMachine;

            current = current.GetParent();
        }

        return Player.Instance?.StateMachine;
    }



    public override void _Ready()
    {
        weaponSprite = GetNodeOrNull<Sprite2D>("Scythe");
        weaponHitbox = GetNodeOrNull<Area2D>("Hitbox Area");

        if (weaponHitbox != null)
        {
            weaponHitbox.BodyEntered += OnEnemyHit;
            weaponHitbox.BodyEntered += OnPropHit;
            weaponHitbox.AreaEntered += OnHurtboxHit;
            weaponHitbox.Monitoring = false;
        }

        ScytheArc scytheArc = new ScytheArc();
        AddChild(scytheArc);
        SlotArc(scytheArc);
    }

    public override void _Process(double delta)
    {
        UpdateComboTimers((float)delta);
        if (weaponHitbox != null)
        {
            StateMachine ownerSM = FindOwnerStateMachine();
            weaponHitbox.Monitoring = ownerSM != null && ownerSM.IsAttacking;
        }
    }

    public void SlotArc(WeaponArc newArc)
    {
        if (newArc == null)
            return;

        currentArc = newArc;
        currentArc.SetParentWeapon(this);
    }

    public void SetLayerRelativeToPlayer(int playerZIndex, bool above)
    {
        int offset = above ? 1 : -1;
        this.ZIndex = playerZIndex + offset;
    }

    private void OnHurtboxHit(Area2D area)
    {
        if (area is not Hurtbox hurtbox)
            return;

        Entity targetEntity = hurtbox.OwnerEntity;
        if (targetEntity == null)
            return;

        Player player = Player.Instance;
        if (player == null)
            return;

        if (targetEntity is Enemy enemy)
            CheckWeaknessExploited(enemy);

        bool wasStaggered = targetEntity is Enemy staggeredEnemy && staggeredEnemy.IsStaggered;
        targetEntity.TakeDamage(currentArc.Damage, GlobalPosition, currentArc);
        if (wasStaggered)
            ApplyStaggerHitStaminaRestore(player, currentArc.StaminaRestore);
        currentArc?.TriggerHitAnimation();

    }

    private void OnEnemyHit(Node2D body)
    {
        if (body is Enemy enemy)
        {
            Player player = Player.Instance;
            if (player == null)
                return;

            bool wasStaggered = enemy.IsStaggered;
            CheckWeaknessExploited(enemy);
            enemy.TakeDamage(currentArc.Damage, GlobalPosition, currentArc);
            if (wasStaggered)
                ApplyStaggerHitStaminaRestore(player, currentArc.StaminaRestore);
            currentArc?.TriggerHitAnimation();

        }
    }

    private void OnPropHit(Node2D body)
    {
        if (body is Prop prop)
        {
            Player player = Player.Instance;
            if (player == null)
                return;

            prop.TakeDamage(currentArc.Damage, GlobalPosition, currentArc);
            currentArc?.TriggerHitAnimation();

        }
    }

    private void ApplyStaggerHitStaminaRestore(Player player, float restoreAmount)
    {
        if (player == null || restoreAmount <= 0f)
            return;

        player.ResourceManager.SetStamina(player.ResourceManager.Stamina + restoreAmount);
    }
}