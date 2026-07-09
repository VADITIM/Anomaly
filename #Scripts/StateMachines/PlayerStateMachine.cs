using Godot;

public partial class PlayerStateMachine : StateMachine
{
    private const float HealDuration = 1.5f;

    private float _heavyChargeDuration = 1f;
    private float _healDuration = 0f;

    private Player Player => OwnerEntity as Player;

    public float HealProgress => CurrentState == State.Healing ? 1f - (_healDuration / HealDuration) : 0f;
    public float HeavyChargeProgress { get; private set; } = 0f;

    public Vector2 GetDodgeDirection() => GetDodgeBehavior()?.GetDodgeDirection() ?? Vector2.Zero;

    private DodgeBehavior GetDodgeBehavior()
    {
        return OwnerEntity?.GetBehavior<DodgeBehavior>();
    }

    protected override void ProcessState(float delta)
    {
        if (Player == null)
            return;

        var movementBehavior = GetMovementBehavior();
        var currentDirection = movementBehavior?.CurrentDirection ?? MovementBehavior.MovementDirection.None;

        if (Player.IsJumping)
        {
            if (!IsLockedState(CurrentState) && !IsAirborne)
                TransitionTo(State.Airborne);
        }
        else if (IsAirborne)
        {
            if (currentDirection != MovementBehavior.MovementDirection.None && CanMove)
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
                var dodgeBehavior = GetDodgeBehavior();
                if (dodgeBehavior != null)
                    dodgeBehavior.ProcessDodge(delta);
                if (dodgeBehavior == null || !dodgeBehavior.IsDodging)
                {
                    RaiseDodgeEnded();
                    TransitionTo(State.Idle);
                }
                break;

            case State.Attacking:
            case State.HeavyAttacking:
            case State.AirAttacking:
                _attackDuration -= delta;
                if (_attackDuration <= 0)
                {
                    Player.Weapon?.OnAttackAnimationFinished();
                    RaiseAttackEnded();
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
                    RaiseHealEnded();
                    TransitionTo(State.Idle);
                }
                break;
        }

        ProcessInput();
    }

    private void ProcessInput()
    {
        if (!CanAct || Player == null)
            return;

        var movementBehavior = GetMovementBehavior();
        var currentDirection = movementBehavior?.CurrentDirection ?? MovementBehavior.MovementDirection.None;

        UpdateMovementDirection();

        if (IsInLockedState())
            return;

        if (Input.IsActionJustPressed(Keybinds.Dodge) && CanMove)
        {
            if (CurrentState == State.Healing)
                RaiseHealEnded();
            Vector2 dodgeInput = movementBehavior?.GetMovementVector() ?? Vector2.Zero;
            RequestDodge(dodgeInput);
            return;
        }

        if (CurrentState == State.Healing)
            return;

        if (CurrentState == State.Attacking)
        {
            if (Input.IsActionJustPressed(Keybinds.Attack))
                Player.Weapon?.QueueAttackFollowUp();
            return;
        }

        if (CurrentState == State.HeavyAttacking)
            return;

        if (Input.IsActionJustPressed(Keybinds.Heal) && CanAct)
        {
            RequestHeal(HealDuration);
            return;
        }

        if (Input.IsActionPressed(Keybinds.Heavy) && CanAttack && !IsComboCoolingDown())
        {
            if (!Player.ResourceManager.HasSpecialAttackReady())
                return;

            if (HasAttackStamina())
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
            if (HasAttackStamina())
            {
                if (Player.Weapon != null && Player.Weapon.CanQueueAttackFollowUp)
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

        if (currentDirection != MovementBehavior.MovementDirection.None && CanMove)
        {
            if (CurrentState == State.Idle)
                TransitionTo(State.Moving);
        }
        else if (CurrentState == State.Moving)
        {
            TransitionTo(State.Idle);
        }
    }

    public void RequestHeal(float duration)
    {
        if (CurrentState == State.Dead) return;
        if (CurrentState != State.Idle && CurrentState != State.Moving)
            return;
        _healDuration = duration;
        TransitionTo(State.Healing);
        RaiseHealStarted(duration);
    }

    public void RequestDodge(Vector2 direction)
    {
        if (CurrentState == State.Dead) return;
        var dodgeBehavior = GetDodgeBehavior();
        if (dodgeBehavior == null || !dodgeBehavior.TryDodge(direction))
            return;
        _knockbackVelocity = Vector2.Zero;
        TransitionTo(State.Dodging);
        RaiseDodgeStarted(dodgeBehavior.GetDodgeDirection());
    }

    private bool HasAttackStamina()
    {
        return Player.ResourceManager.HasStamina(Player.Weapon.StaminaCost);
    }

    private void SpendAttackStamina()
    {
        Player.ResourceManager.TryUseStamina(Player.Weapon.StaminaCost);
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
        Player.Weapon?.StartAttackSequence(true);
        Player.ResourceManager.StartSpecialCooldown(Player.Weapon?.CurrentArc?.GetSpecialCooldownDuration() ?? _attackDuration);
        TransitionTo(State.HeavyAttacking);
        RaiseAttackStarted(true);
    }

    private void ExecuteNormalAttack()
    {
        if (CurrentState != State.Idle && CurrentState != State.Moving) return;

        SpendAttackStamina();
        _attackDuration = Player.GetCurrentAttackAnimationDuration(false);
        Player.Weapon?.StartAttackSequence(false);
        TransitionTo(State.Attacking);
        RaiseAttackStarted(false);
    }

    private void ContinueComboAttack()
    {
        if (Player.Weapon == null) return;

        if (!Player.Weapon.TryConsumeQueuedAttack(false, out float duration))
            return;

        SpendAttackStamina();
        _attackDuration = duration;
        Player.Weapon.StartAttackSequence(false);
        TransitionTo(IsAirborne ? State.AirAttacking : State.Attacking);
        RaiseAttackStarted(false);
    }

    private void ExecuteAirAttack()
    {
        if (CurrentState != State.Airborne) return;

        SpendAttackStamina();
        _attackDuration = Player.GetCurrentAttackAnimationDuration(false);
        Player.Weapon?.StartAttackSequence(false);
        TransitionTo(State.AirAttacking);
        RaiseAttackStarted(false);
    }

    private bool IsComboCoolingDown() { return Player.Weapon?.IsInComboCooldown ?? false; }
}
