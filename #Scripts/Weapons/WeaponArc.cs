using Godot;
public partial class WeaponArc : Node2D
{
    [Export] public Area2D Hitbox;
    [Export] public Sprite2D Sprite;
    [Export] public AnimationPlayer AnimationPlayer;
    private Timer attackAnimationStopTimer;
    private string preparedDirection = "Down";
    private bool preparedHeavyAttack = false;
    private Weapon parentWeapon;

    // Properties that reference parent weapon stats
    public float Damage => parentWeapon != null ? parentWeapon.Damage : 0f;
    public float Knockback => parentWeapon != null ? parentWeapon.Knockback : 0f;
    public float StaminaCost => parentWeapon != null ? parentWeapon.StaminaCost : 0f;
    public float TenacityDamage { get => parentWeapon != null ? parentWeapon.TenacityDamage : 0f; set { if (parentWeapon != null) parentWeapon.TenacityDamage = value; } }
    public float AttackSpeed { get => parentWeapon != null ? parentWeapon.AttackSpeed : 1f; set { if (parentWeapon != null) parentWeapon.AttackSpeed = value; } }
    public float Penetration { get => parentWeapon != null ? parentWeapon.Penetration : 0f; set { if (parentWeapon != null) parentWeapon.Penetration = value; } }
    public float AttackDuration { get => parentWeapon != null ? parentWeapon.AttackDuration : 0.5f; set { if (parentWeapon != null) parentWeapon.AttackDuration = value; } }
    public float HeavyAttackDuration { get => parentWeapon != null ? parentWeapon.HeavyAttackDuration : 0.8f; set { if (parentWeapon != null) parentWeapon.HeavyAttackDuration = value; } }
    public int SpecialHitInterval { get => parentWeapon != null ? parentWeapon.SpecialHitInterval : 4; set { if (parentWeapon != null) parentWeapon.SpecialHitInterval = value; } }
    public int HitCount { get => parentWeapon != null ? parentWeapon.HitCount : 0; set { if (parentWeapon != null) parentWeapon.HitCount = value; } }
    public float CurrentTenacityDamageMultiplier { get => parentWeapon != null ? parentWeapon.CurrentTenacityDamageMultiplier : 1f; set { if (parentWeapon != null) parentWeapon.CurrentTenacityDamageMultiplier = value; } }
    public float OutsideKnockbackForce { get => parentWeapon != null ? parentWeapon.OutsideKnockbackForce : 1f; set { if (parentWeapon != null) parentWeapon.OutsideKnockbackForce = value; } }

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

    public void PrepareAttack(string direction = "Down", bool isHeavy = false)
    {
        preparedDirection = direction;
        preparedHeavyAttack = isHeavy;

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

        string[] animationNames = new[] { $"Sword_Attack_{preparedDirection}", $"Weapon_Attack_{preparedDirection}", "Effect", "Attack1", "default" };
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

        Visible = true;

        AnimationPlayer.Stop();

        float desiredDuration = 1f / Mathf.Max(AttackSpeed, 0.0001f);
        if (preparedHeavyAttack && HeavyAttackDuration > 0f)
            desiredDuration = HeavyAttackDuration;

        float nativeLength = GetAnimationDuration(animationToPlay);
        float playbackSpeed = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        AnimationPlayer.SpeedScale = playbackSpeed;

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
        float baseDuration = 1f / Mathf.Max(AttackSpeed, 0.0001f);
        if (isHeavy && HeavyAttackDuration > 0f)
            return HeavyAttackDuration;
        return baseDuration;
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

