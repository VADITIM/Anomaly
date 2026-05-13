using Godot;
using System;

public partial class PlayerStateMachine : StateMachineBase
{
    Player Player;
    public static PlayerStateMachine Instance { get; private set; }
    
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

    private float staggerDuration = 0f;
    private float knockbackDuration = 0f;
    private float attackDuration = 0f;       // duration of the current swing only
    private float heavyChargeDuration = 1f;
    private float healDuration = 0f;
    private const float HEAL_DURATION = 1.5f;
    private Vector2 knockbackVelocity = Vector2.Zero;
    
    public float HealProgress => CurrentState == PlayerState.Healing ? 1f - (healDuration / HEAL_DURATION) : 0f;
    public float HeavyChargeProgress { get; private set; } = 0f;

    public Vector2 GetKnockbackVelocity() => knockbackVelocity;
    public Vector2 GetDodgeDirection() => Dodge.GetDodgeDirection();

    public override void _Ready()
    {
        Instance = this;
        SetInitialState(PlayerState.Idle);
        Player = GetParent<Player>();
    }

    public override void _Process(double delta)
    {
        if (IsPaused) return;
        
        AdvanceStateTime((float)delta);
        ProcessCurrentState((float)delta);
        ProcessInput();
    }

    private void ProcessCurrentState(float delta)
    {
        if (Player != null && Player.IsJumping)
        {
            if (!IsLockedState(CurrentState) && !IsAirborne)
                TransitionTo(PlayerState.Airborne);
        }
        else if (IsAirborne)
        {
            if (Movement.CurrentMovementDirection != Player.MovementDirection.None && CanMove)
                TransitionTo(PlayerState.Moving);
            else
                TransitionTo(PlayerState.Idle);
        }

        switch (CurrentState)
        {
            case PlayerState.Staggered:
                staggerDuration -= delta;
                if (staggerDuration <= 0)
                {
                    knockbackVelocity = Vector2.Zero;
                    TransitionTo(PlayerState.Idle);
                }
                break;
                
            case PlayerState.Knockback:
                knockbackDuration -= delta;
                knockbackVelocity = knockbackVelocity.MoveToward(Vector2.Zero, 800f * delta);
                if (knockbackDuration <= 0)
                {
                    knockbackVelocity = Vector2.Zero;
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
                attackDuration -= delta;
                if (attackDuration <= 0)
                {
                    Player?.Weapon?.OnAttackAnimationFinished();

                    if (Player?.Weapon != null &&
                        Player.Weapon.TryConsumeQueuedAttack(
                            CurrentState == PlayerState.HeavyAttacking,
                            out float nextDuration))
                    {
                        attackDuration = nextDuration;
                        ResetStateTime();
                        OnAttackStarted?.Invoke(CurrentState == PlayerState.HeavyAttacking);
                        return;
                    }

                    OnAttackEnded?.Invoke();
                    TransitionTo(PlayerState.Idle);
                }
                break;
                
            case PlayerState.HeavyCharging:
                HeavyChargeProgress += delta / heavyChargeDuration;
                HeavyChargeProgress = Mathf.Clamp(HeavyChargeProgress, 0f, 1f);
                if (HeavyChargeProgress >= 1f)
                    ExecuteHeavyAttack();
                break;
                
            case PlayerState.Healing:
                healDuration -= delta;
                if (healDuration <= 0)
                {
                    OnHealEnded?.Invoke();
                    TransitionTo(PlayerState.Idle);
                }
                break;
        }
    }

    private void ProcessInput()
    {
        if (!CanAct) return;

        UpdateMovementDirection();
        
        if (Input.IsActionJustPressed(Keybinds.Dodge) && CanMove)
        {
            if (CurrentState == PlayerState.Healing)
                OnHealEnded?.Invoke();
            TryDodge();
            return;
        }
        
        if (IsInLockedState()) return;
        
        if (CurrentState == PlayerState.Healing)
        {
            UpdateMovementDirection();
            return;
        }

        if (CurrentState == PlayerState.Attacking)
        {
            if (Input.IsActionJustPressed(Keybinds.Attack))
                Player?.Weapon?.QueueAttackFollowUp();
            return;
        }

        if (CurrentState == PlayerState.HeavyAttacking)
            return;

        if (IsAirborne)
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
                if (CurrentState != PlayerState.HeavyCharging)
                    StartHeavyCharge();
            }
            return;
        }
        
        if (Input.IsActionJustReleased(Keybinds.Heavy) && CurrentState == PlayerState.HeavyCharging)
        {
            ExecuteHeavyAttack();
            return;
        }

        // Normal attack — also handles the combo follow-up when the player is
        // briefly in Idle during the 0.2s window after a swing finishes.
        if (Input.IsActionJustPressed(Keybinds.Attack) && CanAttack && !IsComboCoolingDown())
        {
            if (Player.Instance.Stats.GetCurrent("Stamina") >= Player.Instance.Weapon.StaminaCost)
            {
                // If there's an open combo window, this press continues the chain.
                if (Player?.Weapon != null && Player.Weapon.CanQueueAttackFollowUp)
                {
                    Player.Weapon.QueueAttackFollowUp();
                    ContinueComboAttack();
                }
                else
                {
                    ExecuteNormalAttack();
                }
            }
            return;
        }
        
        if (Movement.CurrentMovementDirection != Player.MovementDirection.None && CanMove)
        {
            if (CurrentState == PlayerState.Idle)
                TransitionTo(PlayerState.Moving);
        }
        else if (CurrentState == PlayerState.Moving)
        {
            TransitionTo(PlayerState.Idle);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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

        attackDuration = duration;
        Player.Weapon.StartAttackSequence(false);
        TransitionTo(PlayerState.Attacking);
        OnAttackStarted?.Invoke(false);
    }

    private void UpdateMovementDirection()
    {
        if (IsAttacking) return;

        Player.MovementDirection newDirection = Player.MovementDirection.None;
        
        if (Input.IsActionPressed(Keybinds.MoveUp))    newDirection |= Player.MovementDirection.Up;
        if (Input.IsActionPressed(Keybinds.MoveDown))  newDirection |= Player.MovementDirection.Down;
        if (Input.IsActionPressed(Keybinds.MoveLeft))  newDirection |= Player.MovementDirection.Left;
        if (Input.IsActionPressed(Keybinds.MoveRight)) newDirection |= Player.MovementDirection.Right;
        
        if (newDirection != Movement.CurrentMovementDirection)
        {
            Movement.CurrentMovementDirection = newDirection;
            OnMovementDirectionChanged?.Invoke(Movement.CurrentMovementDirection);
        }
    }

    protected override bool CanTransitionTo(PlayerState newState)
    {
        if (CurrentState == PlayerState.Dead && newState != PlayerState.Idle)
            return false;
            
        if (IsInLockedState() && !IsExitingLockedState(newState))
            return false;
            
        return true;
    }

    protected override bool IsLockedState(PlayerState state)
    {
        return state == PlayerState.Staggered ||
               state == PlayerState.Knockback ||
               state == PlayerState.Dodging ||
               state == PlayerState.Dead;
    }

    protected override void OnTransitioned(PlayerState previousState, PlayerState newState, bool wasInLockedState)
    {
        if (wasInLockedState && newState == PlayerState.Idle)
            Movement.CurrentMovementDirection = Player.MovementDirection.None;
    }
    
    private bool IsExitingLockedState(PlayerState newState) => newState == PlayerState.Idle;

    private void TryHeal()
    {
        if (CurrentState == PlayerState.Idle || CurrentState == PlayerState.Moving)
        {
            healDuration = HEAL_DURATION;
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
        HeavyChargeProgress = 0f;
        attackDuration = Player.GetCurrentAttackAnimationDuration(true);
        Player?.Weapon?.StartAttackSequence(true);
        TransitionTo(PlayerState.HeavyAttacking);
        OnAttackStarted?.Invoke(true);
    }
    
    private void ExecuteNormalAttack()
    {
        if (CurrentState != PlayerState.Idle && CurrentState != PlayerState.Moving) return;

        Player.Instance.Stats.SetCurrent(
            "Stamina",
            Mathf.Max(Player.Instance.Stats.GetCurrent("Stamina") - Player.Instance.Weapon.StaminaCost, 0f));

        attackDuration = Player.GetCurrentAttackAnimationDuration(false);
        Player?.Weapon?.StartAttackSequence(false);
        TransitionTo(PlayerState.Attacking);
        OnAttackStarted?.Invoke(false);
    }

    // -------------------------------------------------------------------------
    // External requests
    // -------------------------------------------------------------------------

    public void RequestStagger(float duration)
    {
        if (CurrentState == PlayerState.Dead) return;
        staggerDuration = duration;
        TransitionTo(PlayerState.Staggered);
        OnStaggered?.Invoke(duration);
    }
    
    public void RequestKnockback(Vector2 direction, float force, float duration = 0.3f)
    {
        if (CurrentState == PlayerState.Dead) return;
        knockbackVelocity = direction.Normalized() * force;
        knockbackDuration = duration;
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