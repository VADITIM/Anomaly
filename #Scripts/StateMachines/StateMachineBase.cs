using Godot;
using System;

public abstract partial class StateMachineBase : Node
{
    public State CurrentState { get; protected set; }
    public State PreviousState { get; protected set; }
    public float StateTime { get; protected set; } = 0f;

    public bool CanAct { get; set; } = true;
    public bool CanMove { get; set; } = true;
    public bool CanAttack { get; set; } = true;
    public bool IsPaused { get; set; } = false;

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

    protected void SetInitialState(State initialState)
    {
        CurrentState = initialState;
        PreviousState = initialState;
        StateTime = 0f;
    }

    protected void AdvanceStateTime(float delta)
    {
        StateTime += delta;
    }

    protected void ResetStateTime()
    {
        StateTime = 0f;
    }

    public bool IsState(State state)
    {
        return CurrentState == state;
    }

    public bool WasState(State state)
    {
        return PreviousState == state;
    }

    public bool IsState(params State[] states)
    {
        foreach (State state in states)
        {
            if (IsState(state))
                return true;
        }

        return false;
    }

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

    protected virtual bool CanTransitionTo(State newState)
    {
        return !IsState(newState);
    }

    protected abstract bool IsLockedState(State state);

    protected virtual void OnTransitioned(State previousState, State newState, bool wasInLockedState) { }
}