using Godot;
using System;

public partial class Weapon : Node2D
{
    #region References
    private Camera Camera;
    [Export] public Area2D Hitbox;
    [Export] public AnimatedSprite2D AnimatedSprite;
    [Export] public AnimatedSprite2D AttackAnimationSprite;
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
        
        AnimatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        AttackAnimationSprite = GetNodeOrNull<AnimatedSprite2D>("AttackAnimationSprite");
        if (HasNode("Area2D"))
            Hitbox = GetNode<Area2D>("Area2D");
        else if (HasNode("Hitbox"))
            Hitbox = GetNode<Area2D>("Hitbox");
        
        if (Hitbox != null)
        {
            Hitbox.BodyEntered += OnEnemyHit;
            Hitbox.Monitoring = false;
        }
    }

    public override void _Process(double delta)
    {
        bool shouldMonitor = PlayerStateMachine.Instance != null && PlayerStateMachine.Instance.IsAttacking;
        if (Hitbox != null)
            Hitbox.Monitoring = shouldMonitor;
        // UpdateWeaponSlotPosition();
    }
#endregion

    public void UpdateWeaponSlotPosition()
    {
        if (Player.Instance.WeaponSlot == null) return;
        
        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 directionToCursor = (mousePos - Player.Instance.WeaponSlot.GlobalPosition).Normalized();
        
        Vector2 toMouse = mousePos - GlobalPosition;
        float angle = Mathf.RadToDeg(toMouse.Angle());
        
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        
        Player.Instance.WeaponSprite.Position = GetWeaponSpriteOffset(angle);
        if (Hitbox != null)
            Hitbox.Position = directionToCursor * range;
        if (AnimatedSprite != null)
            AnimatedSprite.Position = GetWeaponSpriteOffset(angle);
        
        if ((angle >= -112.5f && angle < -67.5f) || (angle >= -67.5f && angle < -22.5f))
        {
            Player.Instance.WeaponSprite.ZIndex = -1;
            if (AnimatedSprite != null)
                AnimatedSprite.ZIndex = -1;
        }
        else
        {
            Player.Instance.WeaponSprite.ZIndex = 0;
            if (AnimatedSprite != null)
                AnimatedSprite.ZIndex = 0;
        }
    }

    private Vector2 GetWeaponSpriteOffset(float angleDegrees)
    {
        if (angleDegrees >= -112.5f && angleDegrees < -67.5f)
            return new Vector2(-7, 4); // Up
        else if (angleDegrees >= -67.5f && angleDegrees < -22.5f)
            return new Vector2(-3, 2); // UpRight
        else if (angleDegrees >= -22.5f && angleDegrees < 22.5f)
            return new Vector2(0, 0); // Right
        else if (angleDegrees >= 22.5f && angleDegrees < 67.5f)
            return new Vector2(9, 4); // DownRight
        else if (angleDegrees >= 67.5f && angleDegrees < 112.5f)
            return new Vector2(0, 0); // Down
        else if (angleDegrees >= 112.5f && angleDegrees < 157.5f)
            return new Vector2(0, 0); // DownLeft
        else if (angleDegrees >= 157.5f || angleDegrees < -157.5f)
            return new Vector2(0, 0); // Left
        else // angleDegrees >= -157.5f && angleDegrees < -112.5f
            return new Vector2(0, 0); // UpLeft
    }

#region Hit Detection
    private void OnEnemyHit(Node2D body)
    {
        if (body is Enemy enemy)
        {
            Player player = GetTree().Root.FindChild("Player", true, false) as Player;
            if (player == null)
                return;
            CheckWeaknessExploited(enemy);
            enemy.TakeDamage(this, player.GlobalPosition);
        }
    }

    public void PlayAttackAnimation()
    {
        if (AttackAnimationSprite == null)
            return;

        SpriteFrames spriteFrames = AttackAnimationSprite.SpriteFrames;
        if (spriteFrames == null)
            return;

        string animationName = "Attack1";
        if (!spriteFrames.HasAnimation(animationName))
        {
            var animationNames = spriteFrames.GetAnimationNames();
            if (animationNames.Length == 0)
                return;
            animationName = animationNames[0].ToString();
        }

        AttackAnimationSprite.Stop();
        AttackAnimationSprite.Frame = 0;
        AttackAnimationSprite.FrameProgress = 0f;
        AttackAnimationSprite.Play(animationName);

        _attackAnimationStopTimer?.QueueFree();
        float duration = GetAnimationDuration(spriteFrames, animationName, AttackAnimationSprite.SpeedScale);
        if (duration > 0f)
        {
            _attackAnimationStopTimer = QuickTimer(this, duration);
            _attackAnimationStopTimer.Timeout += () =>
            {
                if (IsInstanceValid(AttackAnimationSprite))
                    AttackAnimationSprite.Stop();
            };
        }
    }

    private static float GetAnimationDuration(SpriteFrames spriteFrames, string animationName, float speedScale)
    {
        float animationSpeed = (float)spriteFrames.GetAnimationSpeed(animationName);
        int frameCount = spriteFrames.GetFrameCount(animationName);

        if (animationSpeed <= 0f || frameCount <= 0)
            return 0f;

        float finalSpeed = animationSpeed * Mathf.Max(speedScale, 0.001f);
        return frameCount / finalSpeed;
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

