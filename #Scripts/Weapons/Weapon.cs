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
    private float heavyAttackDuration = 1.5f;
    private int specialHitInterval = 4;
    private int hitCount = 0;
    private float currentTenacityDamageMultiplier = 1f;
    private float outsideKnockbackForce = 1f;

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

    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false) { currentArc?.PrepareAttack(direction, isHeavy); }

    public override void _Process(double delta)
    {
        weaponHitbox.Monitoring = PlayerStateMachine.Instance != null && PlayerStateMachine.Instance.IsAttacking;
    }

    public void PlayStateAnimation(string animationName)
    {
        WeaponAnimations.PlayStateAnimation(weaponAnimationPlayer, animationName, attackDuration, heavyAttackDuration);
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
        return WeaponAnimations.GetDesiredAttackDuration(attackDuration, heavyAttackDuration, isHeavy);
    }

    public float GetNativeAnimationLength(string direction, bool isHeavy)
    {
        return WeaponAnimations.GetNativeAnimationLength(weaponAnimationPlayer, direction, isHeavy);
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
