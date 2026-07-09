using Godot;
using System;

public partial class WeaponArc : Node2D
{
    public enum WeaponAttackType { Slashing, Piercing, Smashing }

    public Area2D Hitbox { get; private set; }
    public Sprite2D Sprite { get; private set; }
    public AnimationPlayer AnimationPlayer { get; private set; }

    [Export] public float[] AttackDurations = new float[4] { 0.2f, 0.2f, 0.2f, 0.6f };
    [Export] private float _heavyAttackDuration = 1.5f;
    [Export] private float _specialCooldownDuration = 0f;
    [Export] public WeaponAttackType AttackType { get; set; } = WeaponAttackType.Slashing;
    [Export] public float PlayerPushForce { get; set; } = 0f;
    [Export] public float StaminaRestoreMultiplier { get; set; } = 1f;
    [Export] public float StaminaCostMultiplier { get; set; } = 1f;

    public float DamageMultiplier { get; set; } = 1f;
    public float TenacityMultiplier { get; set; } = 1f;
    public float PenetrationMultiplier { get; set; } = 1f;

    private float _knockback = 0f;
    private Timer _attackAnimationStopTimer;
    private string _preparedDirection = "Down";
    private bool _preparedHeavyAttack = false;
    private int _preparedSequenceIndex = 0;
    private Weapon _parentWeapon;

    private Weapon ParentWeapon => _parentWeapon
        ?? throw new InvalidOperationException($"{Name}: parent Weapon not set. Call SetParentWeapon() when slotting the Arc.");

    public float Damage => ParentWeapon.Damage * DamageMultiplier;
    public float Knockback { get => _knockback; set => _knockback = value; }
    public float StaminaCost => ParentWeapon.StaminaCost * StaminaCostMultiplier;
    public float TenacityDamage => ParentWeapon.TenacityDamage * TenacityMultiplier;
    public float StaminaRestore => ParentWeapon.StaminaRestore * StaminaRestoreMultiplier;
    public float Penetration => ParentWeapon.Penetration * PenetrationMultiplier;
    public float HeavyAttackDuration { get => _heavyAttackDuration; set => _heavyAttackDuration = Mathf.Clamp(value, 0.1f, 5f); }
    public float SpecialCooldownDuration { get => _specialCooldownDuration; set => _specialCooldownDuration = Mathf.Clamp(value, 0f, 10f); }
    public int SpecialHitInterval { get => ParentWeapon.SpecialHitInterval; set => ParentWeapon.SpecialHitInterval = value; }
    public int HitCount { get => ParentWeapon.HitCount; set => ParentWeapon.HitCount = value; }

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
        _parentWeapon = weapon;
    }

    public override void _Ready()
    {
        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        AnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        Hitbox = GetNodeOrNull<Area2D>("Hitbox Area")
              ?? GetNodeOrNull<Area2D>("Hitbox")
              ?? GetNodeOrNull<Area2D>("Area2D");

        if (Hitbox == null)
            GD.PushError($"{Name}: no hitbox node found (expected a child Area2D named 'Hitbox Area'). Arc hits will not register.");
    }

    public void PlayAttackAnimation(string direction = "Down", bool isHeavy = false)
    {
        PrepareAttack(direction, isHeavy);
    }

    public void PrepareAttack(string direction = "Down", bool isHeavy = false, int sequenceIndex = 0)
    {
        _preparedDirection = direction;
        _preparedHeavyAttack = isHeavy;
        _preparedSequenceIndex = Mathf.Clamp(sequenceIndex, 0, 3);

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

        int sequenceNumber = Mathf.Clamp(_preparedSequenceIndex + 1, 1, 4);

        string animationToPlay = WeaponAnimations.GetAttackAnimationName(AnimationPlayer, _preparedDirection, _preparedHeavyAttack, _preparedSequenceIndex);

        if (string.IsNullOrEmpty(animationToPlay))
        {
            string[] animationNames = new[] {
                $"Attack_{_preparedDirection}_{sequenceNumber}",
                $"attack_{_preparedDirection}_{sequenceNumber}",
                $"Attack_{_preparedDirection}",
                $"Sword_Attack_{_preparedDirection}",
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

        float desiredDuration = GetAttackAnimationDuration(_preparedDirection, _preparedHeavyAttack);

        float nativeLength = GetAnimationDuration(animationToPlay);
        float playbackSpeed = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        AnimationPlayer.SpeedScale = playbackSpeed;

        AnimationPlayer.Seek(0);
        AnimationPlayer.Play(animationToPlay);

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
        return isHeavy && HeavyAttackDuration > 0f ? HeavyAttackDuration : GetAttackSequenceDuration(_preparedSequenceIndex);
    }

    public float GetSpecialCooldownDuration()
    {
        return SpecialCooldownDuration > 0f ? SpecialCooldownDuration : HeavyAttackDuration;
    }

    public float GetAttackSequenceDuration(int sequenceIndex)
    {
        if (AttackDurations == null || AttackDurations.Length == 0)
            return 0.37f;

        int clampedIndex = Mathf.Clamp(sequenceIndex, 0, AttackDurations.Length - 1);
        return AttackDurations[clampedIndex];
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

    public float ApplyDamage(Enemy enemy)
    {
        return _parentWeapon != null ? _parentWeapon.ApplyDamage(enemy) : 0f;
    }

    public float CalculateTenacityDamage(float baseTenacityDamage)
    {
        return _parentWeapon != null ? _parentWeapon.CalculateTenacityDamage(baseTenacityDamage) : 0f;
    }

    public void ResetTenacityDamage()
    {
        _parentWeapon?.ResetTenacityDamage();
    }
}
