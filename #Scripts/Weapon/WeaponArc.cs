using Godot;
using System;

public partial class WeaponArc : Node2D
{
    public enum WeaponAttackType { Slashing, Piercing, Smashing }

    [Export] public Area2D Hitbox;
    [Export] public Sprite2D Sprite;
    [Export] public AnimationPlayer AnimationPlayer;
    [Export] public float[] attackDurations = new float[4] { 0.2f, 0.2f, 0.2f, 0.6f };
    [Export] private float heavyAttackDuration = 1.5f;
    [Export] public WeaponAttackType AttackType { get; set; } = WeaponAttackType.Slashing;
    [Export] public float PlayerPushForce { get; set; } = 0f;
    [Export] public float StaminaRestoreMultiplier { get; set; } = 1f;

    private float knockback = 0f;

    public float DamageMultiplier { get; set; } = 1f;
    public float TenacityMultiplier { get; set; } = 1f;
    public float PenetrationMultiplier { get; set; } = 1f;
    private Timer attackAnimationStopTimer;
    private string preparedDirection = "Down";
    private bool preparedHeavyAttack = false;
    private int preparedSequenceIndex = 0;
    private Weapon parentWeapon;

    public float Damage => parentWeapon != null ? parentWeapon.Damage * DamageMultiplier : throw new InvalidOperationException("Parent weapon not set.");
    public float Knockback { get => knockback; set => knockback = value; }
    public float StaminaCost => parentWeapon != null ? parentWeapon.StaminaCost : throw new InvalidOperationException("Parent weapon not set.");
    public float TenacityDamage { get => parentWeapon != null ? parentWeapon.TenacityDamage * TenacityMultiplier : throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.TenacityDamage = value; } }
    public float StaminaRestore => parentWeapon != null ? parentWeapon.StaminaRestore * StaminaRestoreMultiplier : throw new InvalidOperationException("Parent weapon not set.");
    public float Penetration { get => parentWeapon != null ? parentWeapon.Penetration * PenetrationMultiplier : throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.Penetration = value; } }
    public float HeavyAttackDuration { get => heavyAttackDuration; set => heavyAttackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public int SpecialHitInterval { get => parentWeapon?.SpecialHitInterval ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.SpecialHitInterval = value; } }
    public int HitCount { get => parentWeapon?.HitCount ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.HitCount = value; } }
    public float CurrentTenacityDamageMultiplier { get => parentWeapon?.CurrentTenacityDamageMultiplier ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.CurrentTenacityDamageMultiplier = value; } }
    public float OutsideKnockbackForce { get => parentWeapon?.OutsideKnockbackForce ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.OutsideKnockbackForce = value; } }

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

    public virtual void SetParentWeapon(Weapon weapon)
    {
        parentWeapon = weapon;
    }

    public override void _Ready()
    {
        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        AnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        if (HasNode("Area2D"))
            Hitbox = GetNode<Area2D>("Area2D");
        else if (HasNode("Hitbox"))
            Hitbox = GetNode<Area2D>("Hitbox");
    }

    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        PrepareAttack(direction, isHeavy);
    }

    public void PrepareAttack(string direction = "Down", bool isHeavy = false, int sequenceIndex = 0)
    {
        preparedDirection = direction;
        preparedHeavyAttack = isHeavy;
        preparedSequenceIndex = Mathf.Clamp(sequenceIndex, 0, 3);

        if (AnimationPlayer == null)
            return;

        AnimationPlayer.Stop();
        AnimationPlayer.SpeedScale = 1f;
        Visible = false;
    }

    public void TriggerHitAnimation()
    {
        if (AnimationPlayer == null)
            return;

        int sequenceNumber = Mathf.Clamp(preparedSequenceIndex + 1, 1, 4);

        string animationToPlay = WeaponAnimations.GetAttackAnimationName(AnimationPlayer, preparedDirection, preparedHeavyAttack, preparedSequenceIndex);

        if (string.IsNullOrEmpty(animationToPlay))
        {
            string[] animationNames = new[] {
                $"Attack_{preparedDirection}_{sequenceNumber}",
                $"attack_{preparedDirection}_{sequenceNumber}",
                $"Attack_{preparedDirection}", 
                $"Sword_Attack_{preparedDirection}", 
                "Effect"
            };

            foreach (string name in animationNames)
            {
                if (AnimationPlayer.HasAnimation(name))
                {
                    animationToPlay = name;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(animationToPlay))
            return;

        Visible = true;

        AnimationPlayer.Stop();

        float desiredDuration = GetAttackAnimationDuration(preparedDirection, preparedHeavyAttack);

        float nativeLength = GetAnimationDuration(animationToPlay);
        float playbackSpeed = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        AnimationPlayer.SpeedScale = playbackSpeed;

        AnimationPlayer.Seek(0);
        AnimationPlayer.Play(animationToPlay);

        attackAnimationStopTimer?.QueueFree();
        if (desiredDuration > 0f)
        {
            attackAnimationStopTimer = QuickTimer(this, desiredDuration);
            attackAnimationStopTimer.Timeout += () =>
            {
                if (IsInstanceValid(AnimationPlayer))
                {
                    AnimationPlayer.Stop();
                    AnimationPlayer.SpeedScale = 1f;
                    Visible = false;
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
        return isHeavy && HeavyAttackDuration > 0f ? HeavyAttackDuration : GetAttackSequenceDuration(preparedSequenceIndex);
    }

    public float GetAttackSequenceDuration(int sequenceIndex)
    {
        if (attackDurations == null || attackDurations.Length == 0)
            return 0.37f;

        int clampedIndex = Mathf.Clamp(sequenceIndex, 0, attackDurations.Length - 1);
        return attackDurations[clampedIndex];
    }

    public void PlayStateAnimation(string animationName)
    {
        if (AnimationPlayer == null || string.IsNullOrEmpty(animationName))
            return;

        if (animationName.StartsWith("Sword_Attack") || animationName.StartsWith("Attack"))
            return;

        if (AnimationPlayer.HasAnimation(animationName))
        {
            AnimationPlayer.SpeedScale = 1f;
            if (AnimationPlayer.CurrentAnimation != animationName || !AnimationPlayer.IsPlaying())
                AnimationPlayer.Play(animationName);
            return;
        }

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

    public void CheckWeaknessExploited(Enemy enemy)
    {
        enemy.outsideKnockbackForce = 1f;
    }

    public bool IsEnemyHit()
    {
        if (Hitbox == null)
            return false;
        return Hitbox.GetOverlappingBodies().Count > 0;
    }

    public float ApplyDamage(Enemy enemy)
    {
        return parentWeapon != null ? parentWeapon.ApplyDamage(enemy) : 0f;
    }
    
    public float CalculateTenacityDamage(float baseTenacityDamage)
    {
        return parentWeapon != null ? parentWeapon.CalculateTenacityDamage(baseTenacityDamage) : 0f;
    }
    
    public void ResetTenacityDamage()
    {
        parentWeapon?.ResetTenacityDamage();
    }
}

