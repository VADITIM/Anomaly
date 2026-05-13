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

    // Combo state
    // attackSequenceIndex is 0-based internally; animation number = index + 1 (so 1-4).
    private int attackSequenceIndex = 0;
    private const int MaxComboSteps = 4;

    // Timer modes:
    //   comboWindowTimer > 0  =>  waiting for the player to press attack again (0.2s window)
    //   comboCooldownTimer > 0 =>  full-combo cooldown after the final hit (0.5s)
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

    /// True while the 0.2s follow-up input window is open.
    public bool CanQueueAttackFollowUp => comboWindowTimer > 0f;

    /// True while either the combo window or post-combo cooldown is running.
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
        // Resolve the exact animation name here so the arc never has to guess.
        string resolvedAnim = WeaponAnimations.GetAttackAnimationName(
            weaponAnimationPlayer, direction, isHeavy, attackSequenceIndex);

        GD.Print($"[Animation] Weapon.PlayAttackAnimation -> sequenceIndex={attackSequenceIndex}, direction={direction}, isHeavy={isHeavy}, resolved='{resolvedAnim ?? "NULL"}'");

        if (resolvedAnim == null)
        {
            GD.PrintErr($"[Weapon] No animation found for direction={direction} index={attackSequenceIndex} — check AnimationPlayer has 'Weapon_Attack_{direction}_{attackSequenceIndex + 1}'");
        }
        else
        {
            float duration = isHeavy ? heavyAttackDuration : GetCurrentAttackSequenceDuration(false);
            WeaponAnimations.PlayAttackAnimation(weaponAnimationPlayer, resolvedAnim, duration);
        }

        // Arc handles hitbox timing and visual effects; animation is already set above.
        currentArc?.PrepareAttack(direction, isHeavy, attackSequenceIndex);
    }

    public override void _Process(double delta)
    {
        UpdateComboTimers((float)delta);
        weaponHitbox.Monitoring = PlayerStateMachine.Instance != null && PlayerStateMachine.Instance.IsAttacking;
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

        if (PlayerStateMachine.Instance?.IsHeavyAttacking ?? false)
        {
            float heavyMultiplier = 1f + (2f * (PlayerStateMachine.Instance?.HeavyChargeProgress ?? 0f));
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

    // -------------------------------------------------------------------------
    // Combo API — called by the PlayerStateMachine
    // -------------------------------------------------------------------------

    /// Called when the state machine begins a new attack swing.
    public void StartAttackSequence(bool isHeavy)
    {
        if (isHeavy)
        {
            ResetAttackSequence();
            return;
        }

        // Close the follow-up window; it will reopen once this swing finishes.
        queuedAttackFollowUp = false;
        comboWindowTimer = 0f;
        GD.Print($"[Weapon] StartAttackSequence -> attackSequenceIndex={attackSequenceIndex}");
    }

    /// Called by the state machine when an attack animation finishes.
    /// Opens the 0.2s follow-up window so the player can chain the next hit.
    public void OnAttackAnimationFinished()
    {
        bool isLastComboStep = attackSequenceIndex >= MaxComboSteps - 1;

        if (isLastComboStep)
        {
            // Full combo finished — start cooldown, reset sequence.
            GD.Print($"[Weapon] OnAttackAnimationFinished -> full combo finished, entering {ComboFinisherCooldown}s cooldown");
            comboCooldownTimer = ComboFinisherCooldown;
            ResetAttackSequence();
        }
        else
        {
            // Open the window during which the player can queue the next hit.
            comboWindowTimer = ComboFollowUpWindow;
            GD.Print($"[Weapon] OnAttackAnimationFinished -> follow-up window opened ({ComboFollowUpWindow}s), sequenceIndex={attackSequenceIndex}");
        }
    }

    /// Records that the player pressed attack while the follow-up window is open.
    public void QueueAttackFollowUp()
    {
        if (comboWindowTimer > 0f)
        {
            queuedAttackFollowUp = true;
            GD.Print($"[Weapon] QueueAttackFollowUp -> queued (sequenceIndex={attackSequenceIndex}, windowRemaining={comboWindowTimer:F3})");
        }
    }

    /// Called by the state machine to check if it should start the next swing.
    /// Advances the sequence index on consumption.
    public bool TryConsumeQueuedAttack(bool isHeavy, out float duration)
    {
        duration = 0f;

        if (!queuedAttackFollowUp)
            return false;

        queuedAttackFollowUp = false;
        comboWindowTimer = 0f; // consumed — close the window

        if (isHeavy)
        {
            duration = heavyAttackDuration;
            return true;
        }

        // Advance the sequence index now that the follow-up is confirmed.
        attackSequenceIndex = Mathf.Min(attackSequenceIndex + 1, MaxComboSteps - 1);

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDurations.Length - 1);
        duration = attackDurations[clampedIndex];
        GD.Print($"[Weapon] TryConsumeQueuedAttack -> advancing to sequenceIndex={attackSequenceIndex}, duration={duration}");
        return true;
    }

    public void ResetAttackSequence()
    {
        attackSequenceIndex = 0;
        comboWindowTimer = 0f;
        queuedAttackFollowUp = false;
    }

    // -------------------------------------------------------------------------
    // Internal timer management
    // -------------------------------------------------------------------------

    private void UpdateComboTimers(float delta)
    {
        if (comboCooldownTimer > 0f)
        {
            comboCooldownTimer = Mathf.Max(comboCooldownTimer - delta, 0f);
            if (comboCooldownTimer <= 0f)
                GD.Print("[Weapon] UpdateComboTimers -> post-combo cooldown expired, ready to attack");
            return; // cooldown takes priority; window can't tick during it
        }

        if (comboWindowTimer > 0f)
        {
            comboWindowTimer = Mathf.Max(comboWindowTimer - delta, 0f);
            if (comboWindowTimer <= 0f)
            {
                // Window expired without input — reset the combo.
                GD.Print($"[Weapon] UpdateComboTimers -> follow-up window expired, resetting sequence (was={attackSequenceIndex})");
                ResetAttackSequence();
            }
        }
    }

    // -------------------------------------------------------------------------
    // Duration / animation helpers
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Hit callbacks — do NOT advance the sequence index here.
    // Sequence advancement happens in TryConsumeQueuedAttack when the player
    // actually presses attack again. Hits only trigger damage / arc effects.
    // -------------------------------------------------------------------------

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

        GD.Print($"[Weapon] OnHurtboxHit -> damage applied, sequenceIndex={attackSequenceIndex}");
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

            GD.Print($"[Weapon] OnEnemyHit -> damage applied, sequenceIndex={attackSequenceIndex}");
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

            GD.Print($"[Weapon] OnPropHit -> damage applied, sequenceIndex={attackSequenceIndex}");
        }
    }
}