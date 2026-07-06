using Godot;
using System;
using System.Runtime.CompilerServices;

public class TenacitySystem
{
    public TenacitySystem(Enemy enemy, Node parent, StateMachine stateMachine)
    {
        Enemy = enemy;
        Parent = parent;
        StateMachine = stateMachine;
        MaxStaggers = enemy.MaxStaggers;
    }

    private Enemy Enemy;
    private Node Parent;
    private StateMachine StateMachine;

    public event Action<Vector2, float> OnKnockbackStarted;
    public event Action OnKnockbackEnded;
    public event Action<float> OnStaggerStarted;
    public event Action OnStaggerEnded;
    public event Action<float> OnRecoveryStarted;

    private float knockbackDuration = 0f;
    private bool isKnockbackActive = false;

    private float staggerDuration = 0f;
    private bool isStaggered = false;
    public int CurrentStaggerCount { get; private set; } = 0;
    public int MaxStaggers { get; private set; } = 3;

    private float recoveryDuration = 0f;
    private bool isRecovering = false;
    public bool IsRecovering => isRecovering;

    private int tenacityStackCount = 0;
    private const int MAX_TENACITY_STACKS = 10;
    private Timer stackResetTimer = null;

    private bool isInStaggerWindow = false;
    private Timer staggerWindowTimer = null;

    private bool cuePlayed = false;
    private bool hasPlayedTenacityBreakShake = false;
    private WeaponArc pendingWeaponReset = null;
    public bool IsInRecoveryCooldown => isRecovering;
    public bool IsInStaggerWindow => isInStaggerWindow;
    public bool IsStaggered => isStaggered;
    public bool IsKnockbackActive => isKnockbackActive;

    private const float STACK_RESET_TIME = 1.5f;
    private const float STAGGER_IMMOBILITY_DURATION = 1.2f;
    private const float STAGGER_WINDOW_DURATION = 2.0f;
    private const float RECOVERY_DURATION = 2.5f;

    public float GetRemainingStaggerTime()  => isStaggered ? staggerDuration : 0f;
    public float GetRemainingKnockbackTime() => isKnockbackActive ? knockbackDuration : 0f;
    public float GetRemainingRecoveryTime() => isRecovering ? recoveryDuration : 0f;
    public bool IsInLockedState() { return isStaggered || isKnockbackActive || StateMachine.IsDead; }

    public void Process(float delta)
    {
        ProcessStagger(delta);
        ProcessKnockback(delta);
        ProcessRecovery(delta);
    }

    private void ProcessStagger(float delta)
    {
        if (!isStaggered) return;

        staggerDuration -= delta;

        if (staggerDuration <= 0)
        {
            isStaggered = false;
            StateMachine.TransitionTo(State.Idle);
            OnStaggerEnded?.Invoke();
        }
    }

    private void ProcessKnockback(float delta)
    {
        if (!isKnockbackActive) return;

        knockbackDuration -= delta;

        if (knockbackDuration <= 0)
        {
            isKnockbackActive = false;
            Enemy.GetBehavior<KnockbackBehavior>()?.StopKnockback();
            StateMachine.TransitionTo(State.Idle);
            OnKnockbackEnded?.Invoke();
        }
    }

    private void ProcessRecovery(float delta)
    {
        if (!isRecovering) return;

        recoveryDuration -= delta;
        if (recoveryDuration <= 0)
        {
            isRecovering = false;
            CurrentStaggerCount = 0;
            OnRecoveryComplete();
        }
    }

    public bool ProcessTenacitySystem(Vector2 playerPosition, WeaponArc weapon)
    {
        if (isRecovering) return false;

        pendingWeaponReset = weapon;
        float tenacityDamage;
        if (Combat.IsHeavyAttacking())
        {
            tenacityDamage = weapon.CalculateTenacityDamage(weapon.TenacityDamage) * 1.30f;
        }
        else
        {
            tenacityDamage = weapon.CalculateTenacityDamage(weapon.TenacityDamage);
        }

        Enemy.Tenacity -= tenacityDamage;
        Enemy.Tenacity = Mathf.Max(Enemy.Tenacity, 0f);

        if (tenacityStackCount < MAX_TENACITY_STACKS)
        {
            tenacityStackCount++;
            RestartStackResetTimer();
        }
        else
        {
            TriggerRecovery();
            return false;
        }

        bool shouldStagger = Enemy.Tenacity <= 0f && CanBeStaggered();

        if (shouldStagger)
        {
            TriggerStagger(playerPosition, weapon);
            bool isFirstBreak = !hasPlayedTenacityBreakShake;
            hasPlayedTenacityBreakShake = true;
            return isFirstBreak;
        }

        return false;
    }

    public bool CanBeStaggered()
    {
        if (isRecovering || StateMachine.IsDead)
            return false;
        return CurrentStaggerCount < MaxStaggers;
    }

    public bool RequestStagger(float duration = -1f)
    {
        if (!CanBeStaggered()) return false;

        staggerDuration = duration > 0 ? duration : Enemy.DefaultStaggerDuration;
        CurrentStaggerCount++;
        isStaggered = true;

        StateMachine.TransitionTo(State.Staggered);
        OnStaggerStarted?.Invoke(staggerDuration);

        return true;
    }

    private void TriggerStagger(Vector2 playerPosition, WeaponArc weapon)
    {
        bool isFirstStagger = !isInStaggerWindow;

        if (!cuePlayed && Enemy.TenacityCooldownCue != null)
        {
            Enemy.TenacityCooldownCue.Play("tenacity broken");
            Enemy.TenacityCooldownCue.Visible = true;
            cuePlayed = true;
        }

        bool staggerApplied = RequestStagger(STAGGER_IMMOBILITY_DURATION);

        if (staggerApplied)
        {
            bool isSpecialHit = (weapon.HitCount % weapon.SpecialHitInterval) == 0;
            ApplyStaggerKnockback(playerPosition, weapon, isSpecialHit);

            if (isFirstStagger)
            {
                StartStaggerWindow();
            }

            if (CurrentStaggerCount >= MaxStaggers)
            {
                ClearStaggerWindowTimer();
                TriggerRecovery();
            }
        }
    }

    private void StartStaggerWindow()
    {
        isInStaggerWindow = true;
        ClearStaggerWindowTimer();

        staggerWindowTimer = new Timer();
        staggerWindowTimer.WaitTime = STAGGER_WINDOW_DURATION;
        staggerWindowTimer.OneShot = true;
        Parent.AddChild(staggerWindowTimer);
        staggerWindowTimer.Start();

        staggerWindowTimer.Timeout += OnStaggerWindowExpired;
    }

    private void OnStaggerWindowExpired()
    {
        ClearStaggerWindowTimer();
        TriggerRecovery();
    }

    public void RequestRecovery(float duration = -1f)
    {
        if (StateMachine.IsDead) return;
        if (isRecovering) return;

        recoveryDuration = duration > 0 ? duration : Enemy.DefaultRecoveryDuration;
        isRecovering = true;

        if (isStaggered)
        {
            isStaggered = false;
            StateMachine.TransitionTo(State.Idle);
        }

        OnRecoveryStarted?.Invoke(recoveryDuration);
    }

    private void TriggerRecovery()
    {
        if (isRecovering) return;

        isInStaggerWindow = false;
        ClearStackResetTimer();
        ClearStaggerWindowTimer();

        RequestRecovery(RECOVERY_DURATION);
    }

    private void OnRecoveryComplete()
    {
        Enemy.Tenacity = Enemy.MaxTenacity;
        tenacityStackCount = 0;
        isInStaggerWindow = false;
        hasPlayedTenacityBreakShake = false;

        pendingWeaponReset?.ResetTenacityDamage();
        pendingWeaponReset = null;

        if (cuePlayed && Enemy.TenacityCooldownCue != null)
        {
            Enemy.TenacityCooldownCue.Play("tenacity recovered");
            cuePlayed = false;
        }
    }

    public void RequestKnockback(Vector2 direction, float force, float duration = -1f)
    {
        if (StateMachine.IsDead) return;

        float appliedDuration = duration > 0 ? duration : Enemy.DefaultKnockbackDuration;
        Enemy.GetBehavior<KnockbackBehavior>()?.ApplyKnockbackFromDirection(direction, force, appliedDuration);

        if (!isStaggered)
        {
            knockbackDuration = appliedDuration;
            isKnockbackActive = true;
            StateMachine.TransitionTo(State.Knockback);
        }
        OnKnockbackStarted?.Invoke(direction, force);
    }

    private void ApplyStaggerKnockback(Vector2 playerPosition, WeaponArc weaponArc, bool isSpecialHit)
    {
        float baseForce = weaponArc.Knockback / 4;

        if (isSpecialHit)
        {
            baseForce *= 1.3f;
        }

        float tenacityDifference = Enemy.MaxTenacity - 5f;
        float tenacityDistanceMultiplier = 1f + (tenacityDifference * -0.1f);
        tenacityDistanceMultiplier = Mathf.Clamp(tenacityDistanceMultiplier, 0.25f, 1.25f);

        float maxTenacityReduction = 1f - (Enemy.MaxTenacity / 100f);
        maxTenacityReduction = Mathf.Clamp(maxTenacityReduction, 0.3f, 1f);

        float weaknessMultiplier = 1f;

        float effectiveKnockback = baseForce * tenacityDistanceMultiplier * maxTenacityReduction * weaknessMultiplier * 10f;
        Vector2 knockbackDirection = (Enemy.GlobalPosition - playerPosition).Normalized();

        RequestKnockback(knockbackDirection, effectiveKnockback);
    }

    private void RestartStackResetTimer()
    {
        if (isInStaggerWindow) return;

        ClearStackResetTimer();

        stackResetTimer = new Timer();
        stackResetTimer.WaitTime = STACK_RESET_TIME;
        stackResetTimer.OneShot = true;
        Parent.AddChild(stackResetTimer);
        stackResetTimer.Start();

        stackResetTimer.Timeout += OnStackResetTimeout;
    }

    private void OnStackResetTimeout()
    {
        if (tenacityStackCount < MAX_TENACITY_STACKS && !isRecovering && !isInStaggerWindow)
        {
            Enemy.Tenacity = Enemy.MaxTenacity;
            tenacityStackCount = 0;
        }

        ClearStackResetTimer();
    }

    private void ClearStackResetTimer()
    {
        if (stackResetTimer != null && IsInstanceValid(stackResetTimer))
        {
            stackResetTimer.Stop();
            stackResetTimer.QueueFree();
            stackResetTimer = null;
        }
    }

    private void ClearStaggerWindowTimer()
    {
        if (staggerWindowTimer != null && IsInstanceValid(staggerWindowTimer))
        {
            staggerWindowTimer.Stop();
            staggerWindowTimer.QueueFree();
            staggerWindowTimer = null;
        }
    }

    private bool IsInstanceValid(GodotObject obj)
    {
        return GodotObject.IsInstanceValid(obj);
    }

    public void Cleanup()
    {
        ClearStackResetTimer();
        ClearStaggerWindowTimer();
    }
}
