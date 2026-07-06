using Godot;
using System;

public partial class StateMachine : Node
{
    public Entity OwnerEntity { get; private set; }

    public State CurrentState { get; protected set; }
    public State PreviousState { get; protected set; }
    public float StateTime { get; protected set; } = 0f;

    public bool CanAct { get; set; } = true;
    public bool CanMove { get; set; } = true;
    public bool CanAttack { get; set; } = true;
    public bool IsPaused { get; set; } = false;

    public int MaxStaggers { get; set; } = 3;

    protected float _staggerDuration = 0f;
    protected float _knockbackDuration = 0f;
    protected float _attackDuration = 0f;
    protected Vector2 _knockbackVelocity = Vector2.Zero;

    public bool CanPerformAction => CanAct && !IsInLockedState();

    public bool IsIdle => IsState(State.Idle);
    public bool IsMoving => IsState(State.Moving);
    public bool IsChasing => IsState(State.Chasing);
    public bool IsAttacking => IsState(State.Attacking, State.HeavyAttacking, State.AirAttacking);
    public bool IsHeavyAttacking => IsState(State.HeavyAttacking);
    public bool IsChargingHeavy => IsState(State.HeavyCharging);
    public bool IsDodging => IsState(State.Dodging);
    public bool HasDodged => WasState(State.Dodging) && !IsState(State.Dodging);
    public bool IsHealing => IsState(State.Healing);
    public bool IsAirborne => IsState(State.Airborne);
    public bool IsStaggered => IsState(State.Staggered);
    public bool IsKnockedBack => IsState(State.Knockback);
    public bool IsDead => IsState(State.Dead);

    public event Action<State, State> OnStateChanged;
    public event Action<MovementBehavior.MovementDirection> OnMovementDirectionChanged;
    public event Action<bool> OnAttackStarted;
    public event Action OnAttackEnded;
    public event Action<Vector2> OnDodgeStarted;
    public event Action OnDodgeEnded;
    public event Action<float> OnHealStarted;
    public event Action OnHealEnded;
    public event Action<float> OnStaggered;
    public event Action<Vector2, float> OnKnockback;
    public event Action OnDied;
    public event Action OnRevived;
    public event Action<float> OnDamageTaken;

    private MovementBehavior.MovementDirection _lastMovementDirection = MovementBehavior.MovementDirection.None;

    public Vector2 GetKnockbackVelocity() => _knockbackVelocity;
    public float GetRemainingStateTime() => IsAttacking ? _attackDuration : 0f;

    public float GetMaxStaggers() => MaxStaggers;
    public void SetMaxStaggers(int max) { MaxStaggers = max; }
    public void NotifyDamageTaken(float damage) { OnDamageTaken?.Invoke(damage); }

    public override void _Ready()
    {
        OwnerEntity = GetParent() as Entity;
        SetInitialState(State.Idle);
    }

    public override void _Process(double delta)
    {
        if (IsPaused)
            return;

        AdvanceStateTime((float)delta);
        ProcessState((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsPaused)
            return;

        ProcessPhysicsState((float)delta);
    }

    protected virtual void ProcessState(float delta) { }
    protected virtual void ProcessPhysicsState(float delta) { }

    protected void SetInitialState(State initialState)
    {
        CurrentState = initialState;
        PreviousState = initialState;
        StateTime = 0f;
    }

    protected void AdvanceStateTime(float delta) { StateTime += delta; }
    protected void ResetStateTime() { StateTime = 0f; }

    public bool WasState(State state) { return PreviousState == state; }
    public bool IsState(State state) { return CurrentState == state; }
    public bool IsState(params State[] states)
    {
        foreach (State state in states)
        {
            if (IsState(state))
                return true;
        }

        return false;
    }

    protected virtual bool CanTransitionTo(State newState)
    {
        if (CurrentState == State.Dead)
            return newState == State.Idle;

        return !IsState(newState);
    }

    protected virtual void OnTransitioned(State previousState, State newState, bool wasInLockedState) { }

    public bool TransitionTo(State newState)
    {
        if (IsState(newState))
            return false;

        if (!CanTransitionTo(newState))
            return false;

        bool wasInLockedState = IsInLockedState();

        PreviousState = CurrentState;
        CurrentState = newState;
        ResetStateTime();

        OnStateChanged?.Invoke(PreviousState, CurrentState);
        OnTransitioned(PreviousState, CurrentState, wasInLockedState);

        return true;
    }

    public bool IsInLockedState()
    {
        return IsLockedState(CurrentState);
    }

    protected virtual bool IsLockedState(State state)
    {
        return state == State.Staggered ||
               state == State.Knockback ||
               state == State.Dodging ||
               state == State.Dead;
    }

    public void RequestStagger(float duration)
    {
        if (CurrentState == State.Dead) return;
        _staggerDuration = duration;
        TransitionTo(State.Staggered);
        OnStaggered?.Invoke(duration);
    }

    public void RequestKnockback(Vector2 direction, float force, float duration = 0.3f)
    {
        if (CurrentState == State.Dead) return;
        _knockbackVelocity = direction.Normalized() * force;
        _knockbackDuration = duration;
        TransitionTo(State.Knockback);
        OnKnockback?.Invoke(direction, force);
    }

    public void RequestAttack(float duration, bool isHeavy)
    {
        if (CurrentState == State.Dead) return;
        _attackDuration = duration;
        OnAttackRequested(isHeavy);
        TransitionTo(isHeavy ? State.HeavyAttacking : State.Attacking);
        OnAttackStarted?.Invoke(isHeavy);
    }

    protected virtual void OnAttackRequested(bool isHeavy) { }

    public void RequestDeath()
    {
        TransitionTo(State.Dead);
        OnDied?.Invoke();
    }

    public void RequestRevive()
    {
        if (CurrentState == State.Dead)
        {
            TransitionTo(State.Idle);
            OnRevived?.Invoke();
        }
    }

    protected MovementBehavior GetMovementBehavior()
    {
        return OwnerEntity?.GetBehavior<MovementBehavior>();
    }

    protected void UpdateMovementDirection()
    {
        if (IsAttacking) return;

        var movementBehavior = GetMovementBehavior();
        if (movementBehavior == null)
            return;

        var newDirection = movementBehavior.CurrentDirection;
        if (newDirection != _lastMovementDirection)
        {
            _lastMovementDirection = newDirection;
            OnMovementDirectionChanged?.Invoke(newDirection);
        }
    }

    protected void RaiseAttackStarted(bool isHeavy) => OnAttackStarted?.Invoke(isHeavy);
    protected void RaiseAttackEnded() => OnAttackEnded?.Invoke();
    protected void RaiseDodgeStarted(Vector2 direction) => OnDodgeStarted?.Invoke(direction);
    protected void RaiseDodgeEnded() => OnDodgeEnded?.Invoke();
    protected void RaiseHealStarted(float duration) => OnHealStarted?.Invoke(duration);
    protected void RaiseHealEnded() => OnHealEnded?.Invoke();
}
