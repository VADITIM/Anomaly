using System;
using System.Collections.Generic;

namespace Anomaly;

public sealed class StateMachine<TState> where TState : struct, Enum
{
    private sealed class StateDefinition
    {
        public Action Enter;
        public Action<float> Update;
        public Action Exit;
    }

    private readonly Dictionary<TState, StateDefinition> states = new();

    public TState Current { get; private set; }
    public TState Previous { get; private set; }
    public float TimeInState { get; private set; }

    public event Action<TState, TState> Changed;

    public void Add(TState state, Action enter = null, Action<float> update = null, Action exit = null)
    {
        states[state] = new StateDefinition { Enter = enter, Update = update, Exit = exit };
    }

    public void Start(TState state)
    {
        Current = state;
        Previous = state;
        TimeInState = 0f;
        states[state].Enter?.Invoke();
    }

    public void Transition(TState next)
    {
        states[Current].Exit?.Invoke();
        Previous = Current;
        Current = next;
        TimeInState = 0f;
        states[next].Enter?.Invoke();
        Changed?.Invoke(Previous, next);
    }

    public void Update(float delta)
    {
        TimeInState += delta;
        states[Current].Update?.Invoke(delta);
    }

    public bool Is(TState state) => EqualityComparer<TState>.Default.Equals(Current, state);
}
