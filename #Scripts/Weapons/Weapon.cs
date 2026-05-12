using Godot;

/// <summary>
/// Main weapon that owns base stats, hit detection, and animation display.
/// Can slot a WeaponArc for hit-confirmed visual effects.
/// </summary>
public partial class Weapon : Node2D
{
    private WeaponArc currentArc;
    private Sprite2D weaponSprite;
    [Export] private AnimationPlayer weaponAnimationPlayer;
    private Area2D weaponHitbox;

    // Base weapon stats
    private float damage = 20f;
    private float knockback = 100f;
    private float staminaCost = 2f;
    private float tenacityDamage = 10f;
    private float attackSpeed = 2.5f;
    private float penetration = 0f;
    private float attackDuration = 0.2f;
    private float heavyAttackDuration = 1.5f;
    private int specialHitInterval = 4;
    private int hitCount = 0;
    private float currentTenacityDamageMultiplier = 1f;
    private float outsideKnockbackForce = 1f;

    public WeaponArc CurrentArc => currentArc;

    public override void _Ready()
    {
        weaponSprite = GetNodeOrNull<Sprite2D>("Scythe");
        weaponHitbox = GetNodeOrNull<Area2D>("Hitbox Area");

        GD.Print($"[Weapon] Ready - Sprite: {(weaponSprite != null ? "FOUND" : "NULL")}, AnimPlayer: {(weaponAnimationPlayer != null ? "FOUND" : "NULL")}, Hitbox: {(weaponHitbox != null ? "FOUND" : "NULL")}");

        if (weaponHitbox != null)
        {
            weaponHitbox.BodyEntered += OnEnemyHit;
            weaponHitbox.BodyEntered += OnPropHit;
            weaponHitbox.AreaEntered += OnHurtboxHit;
            weaponHitbox.Monitoring = false;
        }

        // Create and slot ScytheArc as the default weapon
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

    public void UnslotArc()
    {
        currentArc = null;
    }

    public WeaponArc GetCurrentArc()
    {
        return currentArc;
    }

    // Weapon base stats - owned by this weapon, accessed by arc
    public Area2D Hitbox => weaponHitbox;
    public Sprite2D WeaponSprite => weaponSprite;
    public AnimationPlayer AnimationPlayer => weaponAnimationPlayer;

    public float Damage { get => damage; set => damage = value; }
    public float Knockback { get => knockback; set => knockback = value; }
    public float StaminaCost { get => staminaCost; set => staminaCost = value; }
    public float TenacityDamage { get => tenacityDamage; set => tenacityDamage = Mathf.Clamp(value, 0f, 100f); }
    public float AttackSpeed { get => attackSpeed; set => attackSpeed = Mathf.Clamp(value, 0.1f, 5f); }
    public float Penetration { get => penetration; set => penetration = Mathf.Clamp(value, 0f, 100f); }
    public float AttackDuration { get => attackDuration; set => attackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public float HeavyAttackDuration { get => heavyAttackDuration; set => heavyAttackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public int SpecialHitInterval { get => specialHitInterval; set => specialHitInterval = value; }
    public int HitCount { get => hitCount; set => hitCount = value; }
    public float CurrentTenacityDamageMultiplier { get => currentTenacityDamageMultiplier; set => currentTenacityDamageMultiplier = value; }
    public float OutsideKnockbackForce { get => outsideKnockbackForce; set => outsideKnockbackForce = value; }

    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        currentArc?.PrepareAttack(direction, isHeavy);
    }

    public override void _Process(double delta)
    {
        if (weaponHitbox != null)
            weaponHitbox.Monitoring = PlayerStateMachine.Instance != null && PlayerStateMachine.Instance.IsAttacking;
    }

    public void PlayStateAnimation(string animationName)
    {
        if (weaponAnimationPlayer.HasAnimation(animationName))
        {
            weaponAnimationPlayer.SpeedScale = 1f;
            if (weaponAnimationPlayer.CurrentAnimation != animationName || !weaponAnimationPlayer.IsPlaying())
            {
                GD.Print($"[Weapon.PlayStateAnimation] Playing: {animationName}");
                weaponAnimationPlayer.Play(animationName);
            }
            return;
        }

        string alt = animationName;
        if (animationName.StartsWith("Weapon_"))
            alt = animationName.Substring("Weapon_".Length);

        if (!string.IsNullOrEmpty(alt) && weaponAnimationPlayer.HasAnimation(alt))
        {
            weaponAnimationPlayer.SpeedScale = 1f;
            GD.Print($"[Weapon.PlayStateAnimation] Playing alt: {alt}");
            weaponAnimationPlayer.Play(alt);
            return;
        }

        if (weaponAnimationPlayer.HasAnimation("Weapon_Idle_Down"))
        {
            weaponAnimationPlayer.SpeedScale = 1f;
            GD.Print($"[Weapon.PlayStateAnimation] Fallback to idle");
            weaponAnimationPlayer.Play("Weapon_Idle_Down");
        }
        else
        {
            GD.Print($"[Weapon.PlayStateAnimation] NO ANIMATION FOUND for: {animationName}");
        }
    }

    public void SetLayerRelativeToPlayer(int playerZIndex, bool above)
    {
        int offset = above ? 1 : -1;
        this.ZIndex = playerZIndex + offset;
    }

    public void CheckWeaknessExploited(Enemy enemy)
    {
        // Default behavior - no special weakness multiplier
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

    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        if (weaponAnimationPlayer == null)
            return isHeavy ? heavyAttackDuration : (1f / attackSpeed);

        string[] animationNames = GetAttackAnimationCandidates(direction, isHeavy);
        foreach (string animationName in animationNames)
        {
            if (weaponAnimationPlayer.HasAnimation(animationName))
                return GetAnimationDuration(animationName);
        }

        return isHeavy ? heavyAttackDuration : (1f / attackSpeed);
    }

    private string GetAttackAnimationName(string direction, bool isHeavy)
    {
        string[] animationNames = GetAttackAnimationCandidates(direction, isHeavy);
        foreach (string animationName in animationNames)
        {
            if (weaponAnimationPlayer != null && weaponAnimationPlayer.HasAnimation(animationName))
                return animationName;
        }

        return null;
    }

    private string[] GetAttackAnimationCandidates(string direction, bool isHeavy)
    {
        if (isHeavy)
        {
            return new[] { "Weapon_Spin", "Weapon_Attack_Spin", $"Weapon_Attack_{direction}", "Weapon_Attack_Down", "Weapn_Attack_Up" };
        }

        return new[] { $"Weapon_Attack_{direction}", "Weapon_Attack_Down", "Weapon_Attack_Left", "Weapon_Attack_Right", "Weapon_Attack_Up", "Weapn_Attack_Up" };
    }

    private float GetAnimationDuration(string animationName)
    {
        if (weaponAnimationPlayer == null || !weaponAnimationPlayer.HasAnimation(animationName))
            return 0f;

        Animation animation = weaponAnimationPlayer.GetAnimation(animationName);
        if (animation == null)
            return 0f;

        return Mathf.Max(0.1f, (float)animation.Length);
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
