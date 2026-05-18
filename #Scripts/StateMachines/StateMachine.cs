using Godot;
using System;

public partial class StateMachine : Node
{
    public Entity OwnerEntity { get; private set; }
    public Player Player => OwnerEntity as Player;
    public Enemy Enemy => OwnerEntity as Enemy;

    public State CurrentState { get; protected set; }
    public State PreviousState { get; protected set; }
    public float StateTime { get; protected set; } = 0f;

    public bool CanAct { get; set; } = true;
    public bool CanMove { get; set; } = true;
    public bool CanAttack { get; set; } = true;
    public bool IsPaused { get; set; } = false;

    public Node2D Target { get; set; }
    public EnemyAttackPhase CurrentAttackPhase { get; private set; } = EnemyAttackPhase.None;
    public int MaxStaggers { get; set; } = 3;

    private float _staggerDuration = 0f;
    private float _knockbackDuration = 0f;
    private float _attackDuration = 0f;
    private float _heavyChargeDuration = 1f;
    private float _healDuration = 0f;
    private const float HEAL_DURATION = 1.5f;
    private Vector2 _knockbackVelocity = Vector2.Zero;

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

    public float HealProgress => CurrentState == State.Healing ? 1f - (_healDuration / HEAL_DURATION) : 0f;
    public float HeavyChargeProgress { get; private set; } = 0f;

    public Vector2 GetKnockbackVelocity() => _knockbackVelocity;
    public Vector2 GetDodgeDirection() => Dodge.GetDodgeDirection();
    public float GetRemainingStateTime() => CurrentState == State.Attacking || CurrentState == State.HeavyAttacking || CurrentState == State.AirAttacking ? _attackDuration : 0f;

    public event Action<State, State> OnStateChanged;
    public event Action<Player.MovementDirection> OnMovementDirectionChanged;
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

    public override void _Ready()
    {
        OwnerEntity = GetParent() as Entity;
        SetInitialState(State.Idle);

        if (Enemy != null)
            Target = GetTree().Root.FindChild("Player", true, false) as Node2D;
    }

    public override void _Process(double delta)
    {
        if (IsPaused)
            return;

        AdvanceStateTime((float)delta);

        if (Player != null)
            ProcessPlayer((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsPaused)
            return;

        if (Enemy != null)
            ProcessEnemy((float)delta);
    }

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
        if (CurrentState == State.Dead)
            return newState == State.Idle;

        return !IsState(newState);
    }

    protected virtual bool IsLockedState(State state)
    {
        return state == State.Staggered ||
               state == State.Knockback ||
               state == State.Dodging ||
               state == State.Dead;
    }

    protected virtual void OnTransitioned(State previousState, State newState, bool wasInLockedState) { }

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

    public float GetMaxStaggers() => MaxStaggers;

    public void SetMaxStaggers(int max)
    {
        MaxStaggers = max;
    }

    public void NotifyDamageTaken(float damage)
    {
        OnDamageTaken?.Invoke(damage);
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
        TransitionTo(isHeavy ? State.HeavyAttacking : State.Attacking);
        OnAttackStarted?.Invoke(isHeavy);
    }

    public void RequestHeal(float duration)
    {
        if (CurrentState == State.Dead) return;
        _healDuration = duration;
        TransitionTo(State.Healing);
        OnHealStarted?.Invoke(duration);
    }

    public void RequestDodge(Vector2 direction, float duration)
    {
        if (CurrentState == State.Dead) return;
        _knockbackVelocity = Vector2.Zero;
        TransitionTo(State.Dodging);
        OnDodgeStarted?.Invoke(direction);
    }

    private void ProcessPlayer(float delta)
    {
        if (Player == null)
            return;

        if (Player.IsJumping)
        {
            if (!IsLockedState(CurrentState) && !IsAirborne)
                TransitionTo(State.Airborne);
        }
        else if (IsAirborne)
        {
            if (Movement.CurrentMovementDirection != Player.MovementDirection.None && CanMove)
                TransitionTo(State.Moving);
            else
                TransitionTo(State.Idle);
        }

        switch (CurrentState)
        {
            case State.Staggered:
                _staggerDuration -= delta;
                if (_staggerDuration <= 0)
                {
                    _knockbackVelocity = Vector2.Zero;
                    TransitionTo(State.Idle);
                }
                break;

            case State.Knockback:
                _knockbackDuration -= delta;
                _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, 800f * delta);
                if (_knockbackDuration <= 0)
                {
                    _knockbackVelocity = Vector2.Zero;
                    TransitionTo(State.Idle);
                }
                break;

            case State.Dodging:
                Dodge.ProcessDodge(delta);
                if (!Dodge.IsDodging())
                {
                    OnDodgeEnded?.Invoke();
                    TransitionTo(State.Idle);
                }
                break;

            case State.Attacking:
            case State.HeavyAttacking:
            case State.AirAttacking:
                _attackDuration -= delta;
                if (_attackDuration <= 0)
                {
                    Player?.Weapon?.OnAttackAnimationFinished();
                    OnAttackEnded?.Invoke();
                    if (CurrentState == State.AirAttacking)
                        TransitionTo(State.Airborne);
                    else
                        TransitionTo(State.Idle);
                }
                break;

            case State.HeavyCharging:
                HeavyChargeProgress += delta / _heavyChargeDuration;
                HeavyChargeProgress = Mathf.Clamp(HeavyChargeProgress, 0f, 1f);
                if (HeavyChargeProgress >= 1f)
                    ExecuteHeavyAttack();
                break;

            case State.Healing:
                _healDuration -= delta;
                if (_healDuration <= 0)
                {
                    OnHealEnded?.Invoke();
                    TransitionTo(State.Idle);
                }
                break;
        }

        ProcessPlayerInput();
    }

    private void ProcessPlayerInput()
    {
        if (!CanAct || Player == null)
            return;

        UpdateMovementDirection();

        if (Input.IsActionJustPressed(Keybinds.Dodge) && CanMove)
        {
            if (CurrentState == State.Healing)
                OnHealEnded?.Invoke();
            TryDodge();
            return;
        }

        if (IsInLockedState())
            return;

        if (CurrentState == State.Healing)
        {
            UpdateMovementDirection();
            return;
        }

        if (CurrentState == State.Attacking)
        {
            if (Input.IsActionJustPressed(Keybinds.Attack))
                Player?.Weapon?.QueueAttackFollowUp();
            return;
        }

        if (CurrentState == State.HeavyAttacking)
            return;

        if (Input.IsActionJustPressed(Keybinds.Heal) && CanAct)
        {
            TryHeal();
            return;
        }

        if (Input.IsActionPressed(Keybinds.Heavy) && CanAttack && !IsComboCoolingDown())
        {
            if (Player.Instance.Stats.GetCurrent("Stamina") >= Player.Instance.Weapon.StaminaCost)
            {
                if (CurrentState != State.HeavyCharging)
                    StartHeavyCharge();
            }
            return;
        }

        if (Input.IsActionJustReleased(Keybinds.Heavy) && CurrentState == State.HeavyCharging)
        {
            ExecuteHeavyAttack();
            return;
        }

        if (Input.IsActionJustPressed(Keybinds.Attack) && CanAttack && !IsComboCoolingDown())
        {
            if (Player.Instance.Stats.GetCurrent("Stamina") >= Player.Instance.Weapon.StaminaCost)
            {
                if (Player?.Weapon != null && Player.Weapon.CanQueueAttackFollowUp)
                {
                    Player.Weapon.QueueAttackFollowUp();
                    ContinueComboAttack();
                }
                else
                {
                    if (IsAirborne)
                        ExecuteAirAttack();
                    else
                        ExecuteNormalAttack();
                }
            }
            return;
        }

        if (Movement.CurrentMovementDirection != Player.MovementDirection.None && CanMove)
        {
            if (CurrentState == State.Idle)
                TransitionTo(State.Moving);
        }
        else if (CurrentState == State.Moving)
        {
            TransitionTo(State.Idle);
        }
    }

    private bool IsComboCoolingDown()
    {
        return Player?.Weapon?.IsInComboCooldown ?? false;
    }

    private void ContinueComboAttack()
    {
        if (Player?.Weapon == null) return;

        if (!Player.Weapon.TryConsumeQueuedAttack(false, out float duration))
            return;

        Player.Instance.Stats.SetCurrent(
            "Stamina",
            Mathf.Max(Player.Instance.Stats.GetCurrent("Stamina") - Player.Instance.Weapon.StaminaCost, 0f));

        _attackDuration = duration;
        Player.Weapon.StartAttackSequence(false);
        TransitionTo(IsAirborne ? State.AirAttacking : State.Attacking);
        OnAttackStarted?.Invoke(false);
    }

    private void ExecuteAirAttack()
    {
        if (CurrentState != State.Airborne) return;

        Player.Instance.Stats.SetCurrent(
            "Stamina",
            Mathf.Max(Player.Instance.Stats.GetCurrent("Stamina") - Player.Instance.Weapon.StaminaCost, 0f));

        _attackDuration = Player.GetCurrentAttackAnimationDuration(false);
        Player?.Weapon?.StartAttackSequence(false);
        TransitionTo(State.AirAttacking);
        OnAttackStarted?.Invoke(false);
    }

    private void UpdateMovementDirection()
    {
        if (IsAttacking) return;

        Player.MovementDirection newDirection = Player.MovementDirection.None;

        if (Input.IsActionPressed(Keybinds.MoveUp)) newDirection |= Player.MovementDirection.Up;
        if (Input.IsActionPressed(Keybinds.MoveDown)) newDirection |= Player.MovementDirection.Down;
        if (Input.IsActionPressed(Keybinds.MoveLeft)) newDirection |= Player.MovementDirection.Left;
        if (Input.IsActionPressed(Keybinds.MoveRight)) newDirection |= Player.MovementDirection.Right;

        if (newDirection != Movement.CurrentMovementDirection)
        {
            Movement.CurrentMovementDirection = newDirection;
            OnMovementDirectionChanged?.Invoke(Movement.CurrentMovementDirection);
        }
    }

    private void TryHeal()
    {
        if (CurrentState == State.Idle || CurrentState == State.Moving)
        {
            _healDuration = HEAL_DURATION;
            TransitionTo(State.Healing);
            OnHealStarted?.Invoke(HEAL_DURATION);
        }
    }

    private void TryDodge()
    {
        if (Dodge.TryDodge(Movement.GetMovementVector()))
        {
            TransitionTo(State.Dodging);
            OnDodgeStarted?.Invoke(Dodge.GetDodgeDirection());
        }
    }

    private void StartHeavyCharge()
    {
        if (CurrentState == State.Idle || CurrentState == State.Moving)
        {
            HeavyChargeProgress = 0f;
            TransitionTo(State.HeavyCharging);
        }
    }

    private void ExecuteHeavyAttack()
    {
        HeavyChargeProgress = 0f;
        _attackDuration = Player.GetCurrentAttackAnimationDuration(true);
        Player?.Weapon?.StartAttackSequence(true);
        TransitionTo(State.HeavyAttacking);
        OnAttackStarted?.Invoke(true);
    }

    private void ExecuteNormalAttack()
    {
        if (CurrentState != State.Idle && CurrentState != State.Moving) return;

        Player.Instance.Stats.SetCurrent(
            "Stamina",
            Mathf.Max(Player.Instance.Stats.GetCurrent("Stamina") - Player.Instance.Weapon.StaminaCost, 0f));

        _attackDuration = Player.GetCurrentAttackAnimationDuration(false);
        Player?.Weapon?.StartAttackSequence(false);
        TransitionTo(State.Attacking);
        OnAttackStarted?.Invoke(false);
    }

    private void ProcessEnemy(float delta)
    {
        if (Enemy == null)
            return;

        switch (CurrentState)
        {
            case State.Attacking:
                _attackDuration -= delta;
                if (_attackDuration <= 0)
                {
                    CurrentAttackPhase = EnemyAttackPhase.None;
                    OnAttackEnded?.Invoke();
                    TransitionTo(State.Idle);
                }
                break;
        }

        if (Target == null)
            return;

        float distanceToTarget = Enemy.GlobalPosition.DistanceTo(Target.GlobalPosition);

        switch (CurrentState)
        {
            case State.Idle:
                if (distanceToTarget <= Enemy.ChaseRange)
                    TransitionTo(State.Chasing);
                break;

            case State.Chasing:
                if (distanceToTarget > Enemy.ChaseRange)
                {
                    TransitionTo(State.Idle);
                }
                else if (distanceToTarget <= Enemy.AttackRange)
                {
                    TryEnemyAttack();
                }
                else if (distanceToTarget <= Enemy.StopDistance)
                {
                    Enemy.Velocity = Vector2.Zero;
                }
                else
                {
                    if (!Enemy.TenacitySystem.IsInLockedState())
                    {
                        Vector2 direction = Enemy.GlobalPosition.DirectionTo(Target.GlobalPosition);
                        Enemy.Velocity = direction * Enemy.speed;
                        Enemy.MoveAndSlide();
                    }
                }
                break;
        }
    }

    private void TryEnemyAttack()
    {
        if (CurrentState == State.Chasing || CurrentState == State.Idle)
        {
            _attackDuration = OwnerEntity.GetAttackDuration();
            CurrentAttackPhase = EnemyAttackPhase.WindUp;
            TransitionTo(State.Attacking);
            OnAttackStarted?.Invoke(false);
        }
    }
}
