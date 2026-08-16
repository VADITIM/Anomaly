using Godot;

public partial class EnemyStateMachine : StateMachine
{
    // Strike lands at mid-swing; the tail is recovery so dodges have a readable window.
    private const float ActivePhaseStart = 0.5f;
    private const float RecoveryPhaseStart = 0.75f;
    private const float StrikeRangeGrace = 1.25f;

    public Node2D Target { get; set; }
    public EnemyAttackPhase CurrentAttackPhase { get; private set; } = EnemyAttackPhase.None;

    private Enemy Enemy => OwnerEntity as Enemy;

    private float _totalAttackDuration = 0f;
    private bool _hasStruckThisAttack = false;

    public override void _Ready()
    {
        base._Ready();
        Target = GetTree().Root.FindChild("Player", true, false) as Node2D;
    }

    // Most enemies only perceive their own plane. SightElevationSpan widens that for the ones that
    // are meant to notice the player above or below them (design.md §3.2).
    private bool CanSeeTarget()
    {
        if (Target is not Entity target || Enemy == null)
            return true;

        return ElevationMath.SharesPlane(Enemy.Elevation, target.Elevation, 0.5f + Enemy.SightElevationSpan);
    }

    protected override void OnAttackRequested(bool isHeavy)
    {
        CurrentAttackPhase = EnemyAttackPhase.WindUp;
        _totalAttackDuration = _attackDuration;
        _hasStruckThisAttack = false;
    }

    protected override void ProcessPhysicsState(float delta)
    {
        if (Enemy == null)
            return;

        switch (CurrentState)
        {
            case State.Attacking:
                _attackDuration -= delta;
                UpdateAttackPhase();
                if (_attackDuration <= 0)
                {
                    CurrentAttackPhase = EnemyAttackPhase.None;
                    RaiseAttackEnded();
                    TransitionTo(State.Idle);
                }
                break;
        }

        var movementBehavior = GetMovementBehavior();
        if (Target == null || !CanSeeTarget())
        {
            if (CurrentState == State.Chasing)
                TransitionTo(State.Idle);

            movementBehavior?.SetDirectionFromVector(Vector2.Zero);
            return;
        }

        float distanceToTarget = Enemy.GlobalPosition.DistanceTo(Target.GlobalPosition);

        switch (CurrentState)
        {
            case State.Idle:
                if (distanceToTarget <= Enemy.ChaseRange)
                    TransitionTo(State.Chasing);
                movementBehavior?.SetDirectionFromVector(Vector2.Zero);
                break;

            case State.Chasing:
                if (distanceToTarget > Enemy.ChaseRange)
                {
                    TransitionTo(State.Idle);
                    movementBehavior?.SetDirectionFromVector(Vector2.Zero);
                }
                else if (distanceToTarget <= Enemy.AttackRange)
                {
                    RequestAttack(OwnerEntity.GetAttackDuration(), false);
                    movementBehavior?.SetDirectionFromVector(Vector2.Zero);
                }
                else
                {
                    if (!Enemy.TenacitySystem.IsInLockedState())
                    {
                        Vector2 direction = Enemy.GlobalPosition.DirectionTo(Target.GlobalPosition);
                        movementBehavior?.SetDirectionFromVector(direction);
                    }
                    else
                    {
                        movementBehavior?.SetDirectionFromVector(Vector2.Zero);
                    }
                }
                break;
        }
    }

    private void UpdateAttackPhase()
    {
        if (_totalAttackDuration <= 0f)
            return;

        float progress = 1f - (_attackDuration / _totalAttackDuration);

        if (progress >= RecoveryPhaseStart)
        {
            CurrentAttackPhase = EnemyAttackPhase.Recovery;
        }
        else if (progress >= ActivePhaseStart)
        {
            CurrentAttackPhase = EnemyAttackPhase.Active;
            TryStrikeTarget();
        }
    }

    private void TryStrikeTarget()
    {
        if (_hasStruckThisAttack)
            return;

        _hasStruckThisAttack = true;

        if (Target is not Player player)
            return;

        float distanceToTarget = Enemy.GlobalPosition.DistanceTo(player.GlobalPosition);
        if (distanceToTarget > Enemy.AttackRange * StrikeRangeGrace)
            return;

        if (!ElevationMath.SharesPlane(Enemy.Elevation, player.Elevation))
            return;

        player.TakeDamage(Enemy.Damage, Enemy.GlobalPosition);
    }
}
