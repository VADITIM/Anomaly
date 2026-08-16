using Godot;
using System;

// Presentation half of a Soul Weapon Arc: sprite, animation and hitbox. All
// tuning lives in the SoulWeaponArc Resource (design.md §3.5) — this class owns
// no balance values of its own.
public partial class WeaponArc : Node2D
{
    [Export] public SoulWeaponArc Data { get; set; }

    public Area2D Hitbox { get; private set; }
    public Sprite2D Sprite { get; private set; }
    public AnimationPlayer AnimationPlayer { get; private set; }

    private Timer _attackAnimationStopTimer;
    private string _preparedDirection = "Down";
    private bool _preparedHeavyAttack = false;
    private int _preparedSequenceIndex = 0;
    private Weapon _parentWeapon;

    private Weapon ParentWeapon => _parentWeapon
        ?? throw new InvalidOperationException($"{Name}: parent Weapon not set. Call SetParentWeapon() when slotting the Arc.");

    private SoulWeaponArc ArcData => Data
        ?? throw new InvalidOperationException($"{Name}: no SoulWeaponArc Resource assigned. Assign Data on the Arc scene root.");

    public WeaponAttackType AttackType => ArcData.AttackType;
    public float Knockback => ArcData.Knockback;
    public float PlayerPushForce => ArcData.PlayerPushForce;
    public float HeavyAttackDuration => ArcData.HeavyAttackDuration;
    public int SpecialHitInterval => ArcData.SpecialHitInterval;
    public bool IsSpecialHitSwing => ParentWeapon.IsSpecialHitSwing;

    public float Damage => ParentWeapon.Damage * ArcData.DamageMultiplier;
    public float StaminaCost => ParentWeapon.StaminaCost * ArcData.StaminaCostMultiplier;
    public float HeavyStaminaCost => ParentWeapon.HeavyStaminaCost * ArcData.HeavyStaminaCostMultiplier;
    public float TenacityDamage => ParentWeapon.TenacityDamage * ArcData.TenacityMultiplier;
    public float StaminaRestore => ParentWeapon.StaminaRestore * ArcData.StaminaRestoreMultiplier;
    public float Penetration => ParentWeapon.Penetration * ArcData.PenetrationMultiplier;

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

    public void SetParentWeapon(Weapon weapon)
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

        if (Data == null)
            GD.PushError($"{Name}: no SoulWeaponArc Resource assigned. Arc will not resolve damage or its special hit.");
    }

    public float GetSpecialCooldownDuration()
    {
        return ArcData.SpecialCooldownDuration > 0f ? ArcData.SpecialCooldownDuration : ArcData.HeavyAttackDuration;
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

    // Swing pacing is the Scythe's alone (single source of truth) — an Arc only
    // overrides the duration of its own heavy attack.
    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        return isHeavy && HeavyAttackDuration > 0f
            ? HeavyAttackDuration
            : ParentWeapon.GetLightAttackDuration(_preparedSequenceIndex);
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
