using Godot;
using System;

public class TenacitySystem
{
    public TenacitySystem(Enemy enemy, Node parent, StateMachine stateMachine)
    {
        _enemy = enemy;
        _parent = parent;
        _stateMachine = stateMachine;
    }

    private readonly Enemy _enemy;
    private readonly Node _parent;
    private readonly StateMachine _stateMachine;

    public event Action<Vector2, float> OnKnockbackStarted;
    public event Action OnKnockbackEnded;
    public event Action<float> OnStaggerStarted;
    public event Action OnStaggerEnded;
    public event Action<float> OnRecoveryStarted;

    private float _knockbackDuration = 0f;
    private bool _isKnockbackActive = false;

    private float _staggerDuration = 0f;
    private bool _isStaggered = false;
    public int CurrentStaggerCount { get; private set; } = 0;

    private float _recoveryDuration = 0f;
    private bool _isRecovering = false;
    public bool IsRecovering => _isRecovering;

    private int _tenacityStackCount = 0;
    private const int MaxTenacityStacks = 10;
    private Timer _stackResetTimer = null;

    private bool _isInStaggerWindow = false;
    private Timer _staggerWindowTimer = null;

    private bool _cuePlayed = false;
    private bool _hasPlayedTenacityBreakShake = false;
    private WeaponArc _pendingWeaponReset = null;
    public bool IsInRecoveryCooldown => _isRecovering;
    public bool IsInStaggerWindow => _isInStaggerWindow;
    public bool IsStaggered => _isStaggered;
    public bool IsKnockbackActive => _isKnockbackActive;

    private const float StackResetTime = 1.5f;
    private const float StaggerImmobilityDuration = 1.2f;
    private const float StaggerWindowDuration = 2.0f;
    private const float RecoveryDuration = 2.5f;

    public float GetRemainingStaggerTime()  => _isStaggered ? _staggerDuration : 0f;
    public float GetRemainingKnockbackTime() => _isKnockbackActive ? _knockbackDuration : 0f;
    public float GetRemainingRecoveryTime() => _isRecovering ? _recoveryDuration : 0f;
    public bool IsInLockedState() { return _isStaggered || _isKnockbackActive || _stateMachine.IsDead; }

    public void Process(float delta)
    {
        ProcessStagger(delta);
        ProcessKnockback(delta);
        ProcessRecovery(delta);
    }

    private void ProcessStagger(float delta)
    {
        if (!_isStaggered) return;

        _staggerDuration -= delta;

        if (_staggerDuration <= 0)
        {
            _isStaggered = false;
            _stateMachine.TransitionTo(State.Idle);
            OnStaggerEnded?.Invoke();
        }
    }

    private void ProcessKnockback(float delta)
    {
        if (!_isKnockbackActive) return;

        _knockbackDuration -= delta;

        if (_knockbackDuration <= 0)
        {
            _isKnockbackActive = false;
            _enemy.GetBehavior<KnockbackBehavior>()?.StopKnockback();
            _stateMachine.TransitionTo(State.Idle);
            OnKnockbackEnded?.Invoke();
        }
    }

    private void ProcessRecovery(float delta)
    {
        if (!_isRecovering) return;

        _recoveryDuration -= delta;
        if (_recoveryDuration <= 0)
        {
            _isRecovering = false;
            CurrentStaggerCount = 0;
            OnRecoveryComplete();
        }
    }

    public bool ProcessTenacitySystem(Vector2 playerPosition, WeaponArc weapon)
    {
        if (_isRecovering) return false;

        _pendingWeaponReset = weapon;
        float tenacityDamage = weapon.CalculateTenacityDamage(weapon.TenacityDamage);
        if (Combat.IsHeavyAttacking())
            tenacityDamage *= 1.30f;

        _enemy.Tenacity -= tenacityDamage;
        _enemy.Tenacity = Mathf.Max(_enemy.Tenacity, 0f);

        if (_tenacityStackCount < MaxTenacityStacks)
        {
            _tenacityStackCount++;
            RestartStackResetTimer();
        }
        else
        {
            TriggerRecovery();
            return false;
        }

        bool shouldStagger = _enemy.Tenacity <= 0f && CanBeStaggered();

        if (shouldStagger)
        {
            TriggerStagger(playerPosition, weapon);
            bool isFirstBreak = !_hasPlayedTenacityBreakShake;
            _hasPlayedTenacityBreakShake = true;
            return isFirstBreak;
        }

        return false;
    }

    public bool CanBeStaggered()
    {
        if (_isRecovering || _stateMachine.IsDead)
            return false;
        return CurrentStaggerCount < _enemy.MaxStaggers;
    }

    public bool RequestStagger(float duration = -1f)
    {
        if (!CanBeStaggered()) return false;

        _staggerDuration = duration > 0 ? duration : _enemy.DefaultStaggerDuration;
        CurrentStaggerCount++;
        _isStaggered = true;

        _stateMachine.TransitionTo(State.Staggered);
        OnStaggerStarted?.Invoke(_staggerDuration);

        return true;
    }

    private void TriggerStagger(Vector2 playerPosition, WeaponArc weapon)
    {
        bool isFirstStagger = !_isInStaggerWindow;

        if (!_cuePlayed && _enemy.TenacityCooldownCue != null)
        {
            _enemy.TenacityCooldownCue.Play("tenacity broken");
            _enemy.TenacityCooldownCue.Visible = true;
            _cuePlayed = true;
        }

        bool staggerApplied = RequestStagger(StaggerImmobilityDuration);

        if (staggerApplied)
        {
            bool isSpecialHit = (weapon.HitCount % weapon.SpecialHitInterval) == 0;
            ApplyStaggerKnockback(playerPosition, weapon, isSpecialHit);

            if (isFirstStagger)
            {
                StartStaggerWindow();
            }

            if (CurrentStaggerCount >= _enemy.MaxStaggers)
            {
                ClearStaggerWindowTimer();
                TriggerRecovery();
            }
        }
    }

    private void StartStaggerWindow()
    {
        _isInStaggerWindow = true;
        ClearStaggerWindowTimer();

        _staggerWindowTimer = new Timer();
        _staggerWindowTimer.WaitTime = StaggerWindowDuration;
        _staggerWindowTimer.OneShot = true;
        _parent.AddChild(_staggerWindowTimer);
        _staggerWindowTimer.Start();

        _staggerWindowTimer.Timeout += OnStaggerWindowExpired;
    }

    private void OnStaggerWindowExpired()
    {
        ClearStaggerWindowTimer();
        TriggerRecovery();
    }

    public void RequestRecovery(float duration = -1f)
    {
        if (_stateMachine.IsDead) return;
        if (_isRecovering) return;

        _recoveryDuration = duration > 0 ? duration : _enemy.DefaultRecoveryDuration;
        _isRecovering = true;

        if (_isStaggered)
        {
            _isStaggered = false;
            _stateMachine.TransitionTo(State.Idle);
        }

        OnRecoveryStarted?.Invoke(_recoveryDuration);
    }

    private void TriggerRecovery()
    {
        if (_isRecovering) return;

        _isInStaggerWindow = false;
        ClearStackResetTimer();
        ClearStaggerWindowTimer();

        RequestRecovery(RecoveryDuration);
    }

    private void OnRecoveryComplete()
    {
        _enemy.Tenacity = _enemy.MaxTenacity;
        _tenacityStackCount = 0;
        _isInStaggerWindow = false;
        _hasPlayedTenacityBreakShake = false;

        _pendingWeaponReset?.ResetTenacityDamage();
        _pendingWeaponReset = null;

        if (_cuePlayed && _enemy.TenacityCooldownCue != null)
        {
            _enemy.TenacityCooldownCue.Play("tenacity recovered");
            _cuePlayed = false;
        }
    }

    public void RequestKnockback(Vector2 direction, float force, float duration = -1f)
    {
        if (_stateMachine.IsDead) return;

        float appliedDuration = duration > 0 ? duration : _enemy.DefaultKnockbackDuration;
        _enemy.GetBehavior<KnockbackBehavior>()?.ApplyKnockbackFromDirection(direction, force, appliedDuration);

        if (!_isStaggered)
        {
            _knockbackDuration = appliedDuration;
            _isKnockbackActive = true;
            _stateMachine.TransitionTo(State.Knockback);
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

        float tenacityDifference = _enemy.MaxTenacity - 5f;
        float tenacityDistanceMultiplier = 1f + (tenacityDifference * -0.1f);
        tenacityDistanceMultiplier = Mathf.Clamp(tenacityDistanceMultiplier, 0.25f, 1.25f);

        float maxTenacityReduction = 1f - (_enemy.MaxTenacity / 100f);
        maxTenacityReduction = Mathf.Clamp(maxTenacityReduction, 0.3f, 1f);

        float effectiveKnockback = baseForce * tenacityDistanceMultiplier * maxTenacityReduction * 10f;
        Vector2 knockbackDirection = (_enemy.GlobalPosition - playerPosition).Normalized();

        RequestKnockback(knockbackDirection, effectiveKnockback);
    }

    private void RestartStackResetTimer()
    {
        if (_isInStaggerWindow) return;

        ClearStackResetTimer();

        _stackResetTimer = new Timer();
        _stackResetTimer.WaitTime = StackResetTime;
        _stackResetTimer.OneShot = true;
        _parent.AddChild(_stackResetTimer);
        _stackResetTimer.Start();

        _stackResetTimer.Timeout += OnStackResetTimeout;
    }

    private void OnStackResetTimeout()
    {
        if (_tenacityStackCount < MaxTenacityStacks && !_isRecovering && !_isInStaggerWindow)
        {
            _enemy.Tenacity = _enemy.MaxTenacity;
            _tenacityStackCount = 0;
        }

        ClearStackResetTimer();
    }

    private void ClearStackResetTimer()
    {
        if (_stackResetTimer != null && GodotObject.IsInstanceValid(_stackResetTimer))
        {
            _stackResetTimer.Stop();
            _stackResetTimer.QueueFree();
            _stackResetTimer = null;
        }
    }

    private void ClearStaggerWindowTimer()
    {
        if (_staggerWindowTimer != null && GodotObject.IsInstanceValid(_staggerWindowTimer))
        {
            _staggerWindowTimer.Stop();
            _staggerWindowTimer.QueueFree();
            _staggerWindowTimer = null;
        }
    }

    public void Cleanup()
    {
        ClearStackResetTimer();
        ClearStaggerWindowTimer();
    }
}
