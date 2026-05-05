using Godot;
using System;

public partial class PlayerStateMachine : Node
{
    public static PlayerStateMachine Instance { get; private set; }
    private Player Player;

    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    public PlayerState PreviousState { get; private set; } = PlayerState.Idle;

    public event Action<PlayerState, PlayerState> OnStateChanged;
    public event Action<MovementDirection> OnMovementDirectionChanged;
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

    public float StateTime { get; private set; } = 0f;
    public float HeavyChargeProgress { get; private set; } = 0f;

    private float _staggerDuration = 0f;
    private float _knockbackDuration = 0f;
    private float _attackDuration = 0f;
    private float _attackCooldown = 0f;
    private float _heavyChargeDuration = 1f;
    private float _healDuration = 0f;
    private const float HEAL_DURATION = 1.5f;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    
    public float HealProgress => CurrentState == PlayerState.Healing  ? 1f - (_healDuration / HEAL_DURATION)  : 0f;

    public bool CanAct { get; set; } = true;
    public bool CanMove { get; set; } = true;
    public bool CanAttack { get; set; } = true;
    public bool IsPaused { get; set; } = false;
    public bool IsIdle => CurrentState == PlayerState.Idle;
    public bool IsMoving => CurrentState == PlayerState.Moving;
    public bool IsAttacking => CurrentState == PlayerState.Attacking || CurrentState == PlayerState.HeavyAttacking;
    public bool IsHeavyAttacking => CurrentState == PlayerState.HeavyAttacking;
    public bool IsChargingHeavy => CurrentState == PlayerState.HeavyCharging;
    public bool IsDodging => CurrentState == PlayerState.Dodging;
    public bool HasDodged => PreviousState == PlayerState.Dodging && CurrentState != PlayerState.Dodging;
    public bool IsHealing => CurrentState == PlayerState.Healing;
    public bool IsStaggered => CurrentState == PlayerState.Staggered;
    public bool IsKnockedBack => CurrentState == PlayerState.Knockback;
    public bool IsDead => CurrentState == PlayerState.Dead;
    public bool CanPerformAction => !IsInLockedState() && CanAct;

    public Vector2 GetKnockbackVelocity() => _knockbackVelocity;
    public Vector2 GetDodgeDirection() => Dodge.GetDodgeDirection();

    public override void _Ready()
    {
        Instance = this;
        Player = GetParent<Player>();
    }

    public override void _Process(double delta)
    {
        if (IsPaused) return;
        
        StateTime += (float)delta;
        ProcessCurrentState((float)delta);
        ProcessInput();
    }

    private void ProcessCurrentState(float delta)
    {
        switch (CurrentState)
        {
            case PlayerState.Staggered:
                _staggerDuration -= delta;
                if (_staggerDuration <= 0)
                {
                    _knockbackVelocity = Vector2.Zero;
                    TransitionTo(PlayerState.Idle);
                }
                break;
                
            case PlayerState.Knockback:
                _knockbackDuration -= delta;
                _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, 800f * delta);
                if (_knockbackDuration <= 0)
                {
                    _knockbackVelocity = Vector2.Zero;
                    TransitionTo(PlayerState.Idle);
                }
                break;
                
            case PlayerState.Dodging:
                Dodge.ProcessDodge(delta);
                if (!Dodge.IsDodging())
                {
                    OnDodgeEnded?.Invoke();
                    TransitionTo(PlayerState.Idle);
                }
                break;
                
            case PlayerState.Attacking:
            case PlayerState.HeavyAttacking:
                _attackDuration -= delta;
                if (_attackDuration <= 0)
                {
                    OnAttackEnded?.Invoke();
                    TransitionTo(PlayerState.Idle);
                }
                break;
                
            case PlayerState.HeavyCharging:
                HeavyChargeProgress += delta / _heavyChargeDuration;
                HeavyChargeProgress = Mathf.Clamp(HeavyChargeProgress, 0f, 1f);
                if (HeavyChargeProgress >= 1f)
                {
                    ExecuteHeavyAttack();
                }
                break;
                
            case PlayerState.Healing:
                _healDuration -= delta;
                if (_healDuration <= 0)
                {
                    OnHealEnded?.Invoke();
                    TransitionTo(PlayerState.Idle);
                }
                break;
        }
        
        if (_attackCooldown > 0)
            _attackCooldown -= delta;
    }

    private void ProcessInput()
    {
        if (!CanAct) return;
        
        if (Input.IsActionJustPressed("dodge") && CanMove)
        {
            if (CurrentState == PlayerState.Healing)
            {
                OnHealEnded?.Invoke();
            }
            TryDodge();
            return;
        }
        
        if (IsInLockedState()) return;
        
        if (CurrentState == PlayerState.Healing)
        {
            UpdateMovementDirection();
            return;
        }
        
        UpdateMovementDirection();
        
        if (Input.IsActionJustPressed("heal") && CanAct)
        {
            TryHeal();
            return;
        }
        
        if (Input.IsActionPressed("heavy") && CanAttack && _attackCooldown <= 0)
        {
            if (Player.Instance.Stats.GetCurrent("Stamina") >= Player.Instance.Weapon.staminaCost)
            {
                if (CurrentState != PlayerState.HeavyCharging)
                    StartHeavyCharge();
            }
            return;
        }
        
        if (Input.IsActionJustReleased("heavy") && CurrentState == PlayerState.HeavyCharging)
        {
            ExecuteHeavyAttack();
            return;
        }
        
        if (Input.IsActionJustPressed("attack") && CanAttack && _attackCooldown <= 0)
        {
            if (Player.Instance.Stats.GetCurrent("Stamina") >= Player.Instance.Weapon.staminaCost)
            {
                ExecuteNormalAttack();
            }
            return;
        }
        
        if (Movement.CurrentMovementDirection != MovementDirection.None && CanMove)
        {
            if (CurrentState == PlayerState.Idle)
                TransitionTo(PlayerState.Moving);
        }
        else if (CurrentState == PlayerState.Moving)
        {
            TransitionTo(PlayerState.Idle);
        }
    }
    
    private void UpdateMovementDirection()
    {
        MovementDirection newDirection = MovementDirection.None;
        
        if (Input.IsActionPressed("up")) newDirection |= MovementDirection.Up;
        if (Input.IsActionPressed("down")) newDirection |= MovementDirection.Down;
        if (Input.IsActionPressed("left")) newDirection |= MovementDirection.Left;
        if (Input.IsActionPressed("right")) newDirection |= MovementDirection.Right;
        
        if (newDirection != Movement.CurrentMovementDirection)
        {
            Movement.CurrentMovementDirection = newDirection;
            OnMovementDirectionChanged?.Invoke(Movement.CurrentMovementDirection);
        }
    }

    public bool TransitionTo(PlayerState newState)
    {
        if (CurrentState == newState) return false;
        if (!CanTransitionTo(newState)) return false;
        
        bool wasInLockedState = IsInLockedState();
        
        PreviousState = CurrentState;
        CurrentState = newState;
        StateTime = 0f;
        
        if (wasInLockedState && newState == PlayerState.Idle)
        {
            Movement.CurrentMovementDirection = MovementDirection.None;
        }
        
        OnStateChanged?.Invoke(PreviousState, CurrentState);
        
        return true;
    }
    
    private bool CanTransitionTo(PlayerState newState)
    {
        if (CurrentState == PlayerState.Dead && newState != PlayerState.Idle)
            return false;
            
        if (IsInLockedState() && !IsExitingLockedState(newState))
            return false;
            
        return true;
    }
    
    private bool IsInLockedState()
    {
        return CurrentState == PlayerState.Staggered ||
               CurrentState == PlayerState.Knockback ||
               CurrentState == PlayerState.Dodging ||
               CurrentState == PlayerState.Dead;
    }
    
    private bool IsExitingLockedState(PlayerState newState)
    {
        return newState == PlayerState.Idle;
    }

    private void TryHeal()
    {
        if (CurrentState == PlayerState.Idle || CurrentState == PlayerState.Moving)
        {
            _healDuration = HEAL_DURATION;
            TransitionTo(PlayerState.Healing);
            OnHealStarted?.Invoke(HEAL_DURATION);
        }
    }
    

    private void TryDodge()
    {
        if (Dodge.TryDodge(Movement.GetMovementVector()))
        {
            TransitionTo(PlayerState.Dodging);
            OnDodgeStarted?.Invoke(Dodge.GetDodgeDirection());
        }
    }
    
    private void StartHeavyCharge()
    {
        if (CurrentState == PlayerState.Idle || CurrentState == PlayerState.Moving)
        {
            HeavyChargeProgress = 0f;
            TransitionTo(PlayerState.HeavyCharging);
        }
    }
    
    private void ExecuteHeavyAttack()
    {
        float chargeBonus = HeavyChargeProgress;
        HeavyChargeProgress = 0f;
        
        _attackDuration = Player.Weapon.heavyAttackDuration;
        TransitionTo(PlayerState.HeavyAttacking);
        OnAttackStarted?.Invoke(true);
        
        if (Player?.Weapon != null)
            _attackCooldown = 1f / Player.Weapon.attackSpeed;
    }
    
    private void ExecuteNormalAttack()
    {
        if (CurrentState == PlayerState.Idle || CurrentState == PlayerState.Moving)
        {
            Player.Instance.Stats.SetCurrent("Stamina", Mathf.Max(Player.Instance.Stats.GetCurrent("Stamina") - Player.Instance.Weapon.staminaCost, 0f));
            _attackDuration = Player.Weapon.attackDuration;
            TransitionTo(PlayerState.Attacking);
            OnAttackStarted?.Invoke(false);
            
            if (Player?.Weapon != null)
                _attackCooldown = 1f / Player.Weapon.attackSpeed;
        }
    }

    public void RequestStagger(float duration)
    {
        if (CurrentState == PlayerState.Dead) return;
        
        _staggerDuration = duration;
        TransitionTo(PlayerState.Staggered);
        OnStaggered?.Invoke(duration);
    }
    
    public void RequestKnockback(Vector2 direction, float force, float duration = 0.3f)
    {
        if (CurrentState == PlayerState.Dead) return;
        
        _knockbackVelocity = direction.Normalized() * force;
        _knockbackDuration = duration;
        TransitionTo(PlayerState.Knockback);
        OnKnockback?.Invoke(direction, force);
    }
    
    public void RequestDeath()
    {
        TransitionTo(PlayerState.Dead);
        OnDied?.Invoke();
    }
    
    public void RequestRevive()
    {
        if (CurrentState == PlayerState.Dead)
        {
            TransitionTo(PlayerState.Idle);
            OnRevived?.Invoke();
        }
    }


}
