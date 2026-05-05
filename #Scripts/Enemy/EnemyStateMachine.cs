using Godot;
using System;

public partial class EnemyStateMachine : Node
{
    private Enemy Enemy;
    public Node2D Target { get; set; }
    public EnemyState CurrentState { get; private set; } = EnemyState.Idle;
    public EnemyState PreviousState { get; private set; } = EnemyState.Idle;
    public EnemyAttackPhase CurrentAttackPhase { get; private set; } = EnemyAttackPhase.None;

    public float StateTime { get; private set; } = 0f;
    public int MaxStaggers { get; set; } = 3;

    private float _attackDuration = 0f;

    public event Action<EnemyState, EnemyState> OnStateChanged;
    public event Action OnAttackStarted;
    public event Action OnAttackEnded;
    public event Action OnDied;
    public event Action<float> OnDamageTaken; 

    public bool IsIdle => CurrentState == EnemyState.Idle;
    public bool IsChasing => CurrentState == EnemyState.Chasing;
    public bool IsAttacking => CurrentState == EnemyState.Attacking;
    public bool IsStaggered => CurrentState == EnemyState.Staggered;
    public bool IsKnockedBack => CurrentState == EnemyState.Knockback;
    public bool IsDead => CurrentState == EnemyState.Dead;
    public bool CanAct => !IsInLockedState();

    public float GetRemainingStateTime()
    {
        return CurrentState switch
        {
            EnemyState.Attacking => _attackDuration,
            _ => 0f
        };
    }

    public override void _Ready()
    {
        Enemy = GetParent<Enemy>();
        Target = GetTree().Root.FindChild("Player", true, false) as Node2D;
    }

    public override void _PhysicsProcess(double delta)
    {
        StateTime += (float)delta;
        ProcessCurrentState((float)delta);
        ProcessAI((float)delta);
    }

    private void ProcessCurrentState(float delta)
    {
        switch (CurrentState)
        {
            case EnemyState.Attacking:
                _attackDuration -= delta;
                if (_attackDuration <= 0)
                {
                    CurrentAttackPhase = EnemyAttackPhase.None;
                    OnAttackEnded?.Invoke();
                    TransitionTo(EnemyState.Idle);
                }
                break;
        }
    }

    private void ProcessAI(float delta)
    {
        if (IsInLockedState()) return;
        if (Target == null) return;
        
        float distanceToTarget = Enemy.GlobalPosition.DistanceTo(Target.GlobalPosition);
        
        switch (CurrentState)
        {
            case EnemyState.Idle:
                if (distanceToTarget <= Enemy.ChaseRange)
                {
                    TransitionTo(EnemyState.Chasing);
                }
                break;
                
            case EnemyState.Chasing:
                if (distanceToTarget > Enemy.ChaseRange)
                {
                    TransitionTo(EnemyState.Idle);
                }
                else if (distanceToTarget <= Enemy.AttackRange)
                {
                    TryAttack();
                }
                else if (distanceToTarget <= Enemy.StopDistance)
                {
                    Enemy.Velocity = Vector2.Zero;
                }
                else
                {
                    if (!Enemy.TenacitySystem.IsInLockedState())
                    {
                        Vector2 direction = (Enemy.GlobalPosition.DirectionTo(Target.GlobalPosition));
                        Enemy.Velocity = direction * Enemy.speed;
                        Enemy.MoveAndSlide();
                    }
                }
                break;
        }
    }

    public bool TransitionTo(EnemyState newState)
    {
        if (CurrentState == newState) return false;
        if (!CanTransitionTo(newState)) return false;
        
        PreviousState = CurrentState;
        CurrentState = newState;
        StateTime = 0f;
        
        OnStateChanged?.Invoke(PreviousState, CurrentState);
        
        return true;
    }
    
    private bool CanTransitionTo(EnemyState newState)
    {
        if (CurrentState == EnemyState.Dead)
            return false;
        
        if (newState == EnemyState.Dead)
            return true;
            
        return true;
    }
    
    public bool IsInLockedState()
    {
        return CurrentState == EnemyState.Staggered ||
               CurrentState == EnemyState.Knockback ||
               CurrentState == EnemyState.Dead;
    }

    public void RequestDeath()
    {
        TransitionTo(EnemyState.Dead);
        OnDied?.Invoke();
    }
    
    public void NotifyDamageTaken(float damage)
    {
        OnDamageTaken?.Invoke(damage);
    }
    
    public void SetMaxStaggers(int max)
    {
        MaxStaggers = max;
    }

    private void TryAttack()
    {
        if (CurrentState == EnemyState.Chasing || CurrentState == EnemyState.Idle)
        {
            _attackDuration = 1f;
            CurrentAttackPhase = EnemyAttackPhase.WindUp;
            TransitionTo(EnemyState.Attacking);
            OnAttackStarted?.Invoke();
        }
    }
    
    public void PerformAttack(float duration = 1f)
    {
        if (IsInLockedState()) return;
        
        _attackDuration = duration;
        CurrentAttackPhase = EnemyAttackPhase.Active;
        TransitionTo(EnemyState.Attacking);
        OnAttackStarted?.Invoke();
    }

}
