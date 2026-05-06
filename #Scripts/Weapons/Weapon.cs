using Godot;
using System;

public partial class Weapon : Node2D
{
    #region References
    private Camera Camera;
    [Export] public Area2D Hitbox;
    [Export] public Sprite2D WeaponSprite;
    [Export] public AnimationPlayer AnimationPlayer;
    private Timer _attackAnimationStopTimer;
    #endregion

#region Stats
    public WeaponType weaponType;
    public enum WeaponType { Melee, Ranged }
    public AttackType attackType;
    public enum AttackType { Piercing, Slashing, Smashing }

    public float damage, range, knockback, staminaCost;

    public float tenacityDamage { get => _tenacityDamage; set => _tenacityDamage = Mathf.Clamp(value, 0f, 100f); }
    public float attackSpeed { get => _attackSpeed; set => _attackSpeed = Mathf.Clamp(value, .1f, 5f); }
    public float penetration { get => _penetration; set => _penetration = Mathf.Clamp(value, 0f, 100f); }

    public float attackDuration { get => _attackDuration; set => _attackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public float heavyAttackDuration { get => _heavyAttackDuration; set => _heavyAttackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public float hitboxDelay { get; set; } = 0f;

    public float _tenacityDamage;
    private float _penetration, _attackSpeed, _attackDuration, _heavyAttackDuration;
#endregion
    
#region Tenacity Damage System
    public int specialHitInterval = 4;
    public int hitCount = 0;
    public float currentTenacityDamageMultiplier = 1f;
#endregion

#region Utility
    public static Timer QuickTimer(Node parent, float time)
    {
        Timer timer = new Timer();
        parent.AddChild(timer);
        timer.WaitTime = time;
        timer.OneShot = true;
        timer.Autostart = true;
        timer.Timeout += () => { timer.QueueFree(); };
        return timer;
    }
#endregion

#region Godot Lifecycle
    public override void _Ready()
    {
        Camera = GetViewport().GetCamera2D() as Camera;
        
        WeaponSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        AnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (HasNode("Area2D"))
            Hitbox = GetNode<Area2D>("Area2D");
        else if (HasNode("Hitbox"))
            Hitbox = GetNode<Area2D>("Hitbox");
        
        if (Hitbox != null)
        {
            Hitbox.BodyEntered += OnEnemyHit;
            Hitbox.AreaEntered += OnHurtboxHit;
            Hitbox.Monitoring = false;
        }
    }

    public override void _Process(double delta)
    {
        bool shouldMonitor = PlayerStateMachine.Instance != null && PlayerStateMachine.Instance.IsAttacking;
        if (Hitbox != null)
            Hitbox.Monitoring = shouldMonitor;
    }
#endregion


 
#region Hit Detection
    private void OnHurtboxHit(Area2D area)
    {
        if (area is not Hurtbox hurtbox)
            return;

        Entity targetEntity = hurtbox.OwnerEntity;
        if (targetEntity is not Enemy enemy)
            return;

        Player player = Player.Instance;
        if (player == null)
            return;

        CheckWeaknessExploited(enemy);
        enemy.TakeDamage(this, player.GlobalPosition);
    }

    private void OnEnemyHit(Node2D body)
    {
        if (body is Enemy enemy)
        {
            Player player = Player.Instance;
            if (player == null)
                return;
            CheckWeaknessExploited(enemy);
            enemy.TakeDamage(this, player.GlobalPosition);
        }
    }

    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        if (AnimationPlayer == null)
            return;

        string[] animationNames;
        if (isHeavy)
        {
            animationNames = new[] { "Weapon_Spin", "Sword_Attack_Spin", $"Sword_Attack_{direction}", "Attack1", "default" };
        }
        else
        {
            animationNames = new[] { $"Sword_Attack_{direction}", "Attack1", "default" };
        }

        string animationToPlay = null;

        foreach (string name in animationNames)
        {
            if (AnimationPlayer.HasAnimation(name))
            {
                animationToPlay = name;
                break;
            }
        }

        if (string.IsNullOrEmpty(animationToPlay))
            return;

        // Stop any current playback and play the chosen attack animation
        AnimationPlayer.Stop();

        // Determine desired attack duration from weapon attack speed (attacks per second)
        float desiredDuration = 1f / Mathf.Max(attackSpeed, 0.0001f);
        if (isHeavy && heavyAttackDuration > 0f)
            desiredDuration = heavyAttackDuration;

        // Native animation length
        float nativeLength = GetAnimationDuration(animationToPlay);
        // Compute playback speed so the animation finishes in desiredDuration
        float playbackSpeed = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        AnimationPlayer.SpeedScale = playbackSpeed;

        AnimationPlayer.Play(animationToPlay);

        // Schedule stopping after the desired duration and restore playback speed
        _attackAnimationStopTimer?.QueueFree();
        if (desiredDuration > 0f)
        {
            _attackAnimationStopTimer = QuickTimer(this, desiredDuration);
            _attackAnimationStopTimer.Timeout += () =>
            {
                if (IsInstanceValid(AnimationPlayer))
                {
                    AnimationPlayer.Stop();
                    AnimationPlayer.SpeedScale = 1f;
                }
            };
        }
    }

    private float GetAnimationDuration(string animationName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animationName))
            return 0f;

        Animation animation = AnimationPlayer.GetAnimation(animationName);
        if (animation == null)
            return 0f;

        return Mathf.Max(0.1f, (float)animation.Length);
    }

    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        // Desired duration is driven by attacks-per-second (attackSpeed)
        float baseDuration = 1f / Mathf.Max(attackSpeed, 0.0001f);
        if (isHeavy && heavyAttackDuration > 0f)
            return heavyAttackDuration;
        return baseDuration;
    }

    // Play state/idle/move animations to mirror the player's animator
    public void PlayStateAnimation(string animationName)
    {
        if (AnimationPlayer == null || string.IsNullOrEmpty(animationName))
            return;

        // Don't override attack animations here; attacks use PlayAttackAnimation
        if (animationName.StartsWith("Sword_Attack") || animationName.StartsWith("Attack"))
            return;

        if (AnimationPlayer.HasAnimation(animationName))
        {
            // Ensure normal state animations play at normal speed
            AnimationPlayer.SpeedScale = 1f;
            if (AnimationPlayer.CurrentAnimation != animationName || !AnimationPlayer.IsPlaying())
                AnimationPlayer.Play(animationName);
            return;
        }

        // Try stripping common prefixes (e.g. "Weapon_") and fallback to a generic idle
        string alt = animationName;
        if (animationName.StartsWith("Weapon_"))
            alt = animationName.Substring("Weapon_".Length);

        if (!string.IsNullOrEmpty(alt) && AnimationPlayer.HasAnimation(alt))
        {
            AnimationPlayer.SpeedScale = 1f;
            AnimationPlayer.Play(alt);
            return;
        }

        if (AnimationPlayer.HasAnimation("idle"))
        {
            AnimationPlayer.SpeedScale = 1f;
            AnimationPlayer.Play("idle");
        }
    }

    // Adjust Z layering relative to the player's sprite ZIndex
    public void SetLayerRelativeToPlayer(int playerZIndex, bool above)
    {
        int offset = above ? 1 : -1;
        this.ZIndex = playerZIndex + offset;
    }

    public void CheckWeaknessExploited(Enemy enemy)
    {
        if (enemy.weaknessType == Enemy.WeaknessType.Slashing && attackType == AttackType.Slashing)
            enemy.outsideKnockbackForce = 2f;
        else
            enemy.outsideKnockbackForce = 1f;
    }

    public bool IsEnemyHit()
    {
        if (Hitbox == null)
            return false;
        return Hitbox.GetOverlappingBodies().Count > 0;
    }
#endregion

#region Damage Calculation
    public float ApplyDamage(Enemy enemy)
    {
        float rawDamage = damage;
        
        if (PlayerStateMachine.Instance?.IsHeavyAttacking ?? false)
        {
            float heavyMultiplier = 1f + (2f * (PlayerStateMachine.Instance?.HeavyChargeProgress ?? 0f));
            rawDamage *= heavyMultiplier;
        }
        
        if (enemy.weaknessType.ToString() == attackType.ToString())
        {
            rawDamage *= 1.5f;
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
#endregion
}

