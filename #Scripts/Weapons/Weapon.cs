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

    private float damage = 20f;
    private float knockback = 100f;
    private float staminaCost = 2f;
    private float tenacityDamage = 10f;
    private float penetration = 50f;
    private float attackDuration = 0.37f;
    private float[] attackDurations = new float[4] { 0.37f, 0.45f, 0.35f, 0.6f };
    private float[] attackDamageMultipliers = new float[4] { 1f, 1.15f, 1.3f, 1.5f };
    private float heavyAttackDuration = 1.5f;
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

    public float Damage { get => damage; set => damage = value; }
    public float Knockback { get => knockback; set => knockback = value; }
    public float StaminaCost { get => staminaCost; set => staminaCost = value; }
    public float TenacityDamage { get => tenacityDamage; set => tenacityDamage = Mathf.Clamp(value, 0f, 100f); }
    public float Penetration { get => penetration; set => penetration = Mathf.Clamp(value, 0f, 100f); }
    public float AttackDuration { get => attackDuration; set => attackDuration = Mathf.Clamp(value, 0.1f, 1.5f); }
    public float HeavyAttackDuration { get => heavyAttackDuration; set => heavyAttackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public int SpecialHitInterval { get => specialHitInterval; set => specialHitInterval = value; }
    public int HitCount { get => hitCount; set => hitCount = value; }
    public float CurrentTenacityDamageMultiplier { get => currentTenacityDamageMultiplier; set => currentTenacityDamageMultiplier = value; }
    public float OutsideKnockbackForce { get => outsideKnockbackForce; set => outsideKnockbackForce = value; }
    public int CurrentAttackSequenceIndex => attackSequenceIndex;

    public bool CanQueueAttackFollowUp => comboWindowTimer > 0f;

    public bool IsInComboCooldown => comboCooldownTimer > 0f;

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

    public void SlotArc(WeaponArc newArc)
    {
        if (newArc == null)
            return;

        currentArc = newArc;
        currentArc.SetParentWeapon(this);
    }

    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        string resolvedAnim = WeaponAnimations.GetAttackAnimationName(
            weaponAnimationPlayer, direction, isHeavy, attackSequenceIndex);


        float duration = isHeavy ? heavyAttackDuration : GetCurrentAttackSequenceDuration(false);
        WeaponAnimations.PlayAttackAnimation(weaponAnimationPlayer, resolvedAnim, duration);

        currentArc?.PrepareAttack(direction, isHeavy, attackSequenceIndex);
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

    public void PlayStateAnimation(string animationName)
    {
        if (weaponAnimationPlayer == null)
            return;

        float desiredDuration = GetStateAnimationDuration(animationName);
        WeaponAnimations.PlayStateAnimation(weaponAnimationPlayer, animationName, desiredDuration);
    }

    public void SetLayerRelativeToPlayer(int playerZIndex, bool above)
    {
        int offset = above ? 1 : -1;
        this.ZIndex = playerZIndex + offset;
    }

    public void CheckWeaknessExploited(Enemy enemy)
    {
        enemy.outsideKnockbackForce = 1f;
    }

    public bool IsEnemyHit()
    {
        return weaponHitbox != null && weaponHitbox.GetOverlappingBodies().Count > 0;
    }

    public float ApplyDamage(Enemy enemy)
    {
        float rawDamage = damage;

        if (Player.Instance?.StateMachine?.IsHeavyAttacking ?? false)
        {
            float heavyMultiplier = 1f + (2f * (Player.Instance?.StateMachine?.HeavyChargeProgress ?? 0f));
            rawDamage *= heavyMultiplier;
        }
        else
        {
            rawDamage *= GetCurrentAttackDamageMultiplier();
        }

        float penetrationPercent = penetration / 100f;
        float effectiveArmor = enemy.armor * (1f - penetrationPercent);
        float damageReductionPercent = effectiveArmor / 200f;
        float damageMultiplier = 1f - damageReductionPercent;
        float calculatedDamage = rawDamage * damageMultiplier;

        return Mathf.Max(calculatedDamage, 0);
    }

    public float CalculateTenacityDamage(float baseTenacityDamage)
    {
        hitCount++;
        bool isSpecialHit = (hitCount % specialHitInterval) == 0;

        float tenacityDamageValue = baseTenacityDamage * currentTenacityDamageMultiplier / 10f;

        if (isSpecialHit)
            tenacityDamageValue *= 1.2f;

        currentTenacityDamageMultiplier -= 0.003f;
        currentTenacityDamageMultiplier = Mathf.Max(currentTenacityDamageMultiplier, 0.1f);

        return tenacityDamageValue;
    }

    public void ResetTenacityDamage()
    {
        currentTenacityDamageMultiplier = 1f;
        hitCount = 0;
    }

    public void StartAttackSequence(bool isHeavy)
    {
        if (isHeavy)
        {
            ResetAttackSequence();
            return;
        }

        queuedAttackFollowUp = false;
        comboWindowTimer = 0f;
    }

    public void OnAttackAnimationFinished()
    {
        bool isLastComboStep = attackSequenceIndex >= MaxComboSteps - 1;

        if (isLastComboStep)
        {
            comboCooldownTimer = ComboFinisherCooldown;
            ResetAttackSequence();
        }
        else
        {
            comboWindowTimer = ComboFollowUpWindow;
        }
    }

    public void QueueAttackFollowUp()
    {
        if (comboWindowTimer > 0f)
        {
            queuedAttackFollowUp = true;
        }
    }

    public bool TryConsumeQueuedAttack(bool isHeavy, out float duration)
    {
        duration = 0f;

        if (!queuedAttackFollowUp)
            return false;

        queuedAttackFollowUp = false;
        comboWindowTimer = 0f; 

        if (isHeavy)
        {
            duration = heavyAttackDuration;
            return true;
        }

        attackSequenceIndex = Mathf.Min(attackSequenceIndex + 1, MaxComboSteps - 1);

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDurations.Length - 1);
        duration = attackDurations[clampedIndex];
        return true;
    }

    public void ResetAttackSequence()
    {
        attackSequenceIndex = 0;
        comboWindowTimer = 0f;
        queuedAttackFollowUp = false;
    }

    private void UpdateComboTimers(float delta)
    {
        if (comboCooldownTimer > 0f)
        {
            comboCooldownTimer = Mathf.Max(comboCooldownTimer - delta, 0f);
            if (comboCooldownTimer <= 0f)
                    return;
        }

        if (comboWindowTimer > 0f)
        {
            comboWindowTimer = Mathf.Max(comboWindowTimer - delta, 0f);
            if (comboWindowTimer <= 0f)
            {
                ResetAttackSequence();
            }
        }
    }

    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        return GetCurrentAttackSequenceDuration(isHeavy);
    }

    public float GetNativeAnimationLength(string direction, bool isHeavy)
    {
        return WeaponAnimations.GetNativeAnimationLength(weaponAnimationPlayer, direction, isHeavy, attackSequenceIndex);
    }

    private float GetCurrentAttackSequenceDuration(bool isHeavy)
    {
        if (isHeavy)
            return heavyAttackDuration;

        if (attackDurations == null || attackDurations.Length == 0)
            return attackDuration;

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDurations.Length - 1);
        return attackDurations[clampedIndex];
    }

    private float GetStateAnimationDuration(string animationName)
    {
        if (!WeaponAnimations.IsAttackAnimation(animationName))
            return 0f;

        return WeaponAnimations.IsHeavyAttack(animationName)
            ? heavyAttackDuration
            : GetCurrentAttackSequenceDuration(false);
    }

    private float GetCurrentAttackDamageMultiplier()
    {
        if (attackDamageMultipliers == null || attackDamageMultipliers.Length == 0)
            return 1f;

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDamageMultipliers.Length - 1);
        return attackDamageMultipliers[clampedIndex];
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

        targetEntity.TakeDamage(currentArc, this);
        currentArc?.TriggerHitAnimation();

    }

    private void OnEnemyHit(Node2D body)
    {
        if (body is Enemy enemy)
        {
            Player player = Player.Instance;
            if (player == null)
                return;

            CheckWeaknessExploited(enemy);
            enemy.TakeDamage(currentArc, this);
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

            prop.TakeDamage(currentArc, this);
            currentArc?.TriggerHitAnimation();

        }
    }
}