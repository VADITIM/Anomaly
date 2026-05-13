using Godot;
using System;

public partial class WeaponArc : Node2D
{
    [Export] public Area2D Hitbox;
    [Export] public Sprite2D Sprite;
    [Export] public AnimationPlayer AnimationPlayer;
    private Timer attackAnimationStopTimer;
    private string preparedDirection = "Down";
    private bool preparedHeavyAttack = false;
    private int preparedSequenceIndex = 0;
    private Weapon parentWeapon;

    // Properties that reference parent weapon stats
    public float Damage => parentWeapon?.Damage ?? throw new InvalidOperationException("Parent weapon not set.");
    public float Knockback => parentWeapon?.Knockback ?? throw new InvalidOperationException("Parent weapon not set.");
    public float StaminaCost => parentWeapon?.StaminaCost ?? throw new InvalidOperationException("Parent weapon not set.");
    public float TenacityDamage { get => parentWeapon?.TenacityDamage ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.TenacityDamage = value; } }
    public float Penetration { get => parentWeapon?.Penetration ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.Penetration = value; } }
    public float AttackDuration { get => parentWeapon?.AttackDuration ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.AttackDuration = value; } }
    public float HeavyAttackDuration { get => parentWeapon?.HeavyAttackDuration ?? throw new InvalidOperationException("Parent weapon not set."); set { if (parentWeapon != null) parentWeapon.HeavyAttackDuration = value; } }
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

        // Prefer the resolved canonical animation name from WeaponAnimations
        string animationToPlay = WeaponAnimations.GetAttackAnimationName(AnimationPlayer, preparedDirection, preparedHeavyAttack, preparedSequenceIndex);

        // Fallback legacy search if resolution failed
        if (string.IsNullOrEmpty(animationToPlay))
        {
            // sequenceNumber already computed above
            string[] animationNames = new[] {
                $"Weapon_Attack_{preparedDirection}_{sequenceNumber}",
                $"attack_{preparedDirection}_{sequenceNumber}",
                $"Weapon_Attack_{preparedDirection}", 
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

        float desiredDuration = AttackDuration;
        if (preparedHeavyAttack && HeavyAttackDuration > 0f)
            desiredDuration = HeavyAttackDuration;

        GD.Print($"[Animation] WeaponArc.TriggerHitAnimation -> sequence={sequenceNumber}, animation='{animationToPlay}', desiredDuration={desiredDuration}");

        float nativeLength = GetAnimationDuration(animationToPlay);
        float playbackSpeed = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        AnimationPlayer.SpeedScale = playbackSpeed;

        // Seek to frame 0 to avoid displaying stale frames from previous animations
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
        if (isHeavy && HeavyAttackDuration > 0f)
            return HeavyAttackDuration;
        return AttackDuration;
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

    public void SetLayerRelativeToPlayer(int playerZIndex, bool above)
    {
        int offset = above ? 1 : -1;
        this.ZIndex = playerZIndex + offset;
    }

    public void CheckWeaknessExploited(Enemy enemy)
    {
        // Types removed — default behavior (no special weakness multiplier)
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

