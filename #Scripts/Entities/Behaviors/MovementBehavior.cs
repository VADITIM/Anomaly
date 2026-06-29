using Godot;
using System;

public class MovementBehavior : IEntityBehavior
{
    public enum MovementDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8
    }

    public MovementDirection CurrentDirection { get; set; } = MovementDirection.None;
    public float HealSpeedModifier { get; set; } = 0.2f;
    public Func<float> GetBaseSpeed { get; set; }
    public Func<Vector2> GetDodgeVelocity { get; set; }

    private Entity owner;
    private StateMachine stateMachine;

    public void OnReady(Entity owner)
    {
        this.owner = owner;
        stateMachine = owner?.StateMachine;
    }

    public void OnProcess(double delta)
    {
    }

    public void OnPhysicsProcess(double delta)
    {
        if (owner == null)
            return;

        ProcessMovement((float)delta);
    }

    public void OnExitTree()
    {
    }

    public Vector2 GetMovementVector()
    {
        Vector2 movement = Vector2.Zero;

        if ((CurrentDirection & MovementDirection.Up) != 0) movement.Y -= 1f;
        if ((CurrentDirection & MovementDirection.Down) != 0) movement.Y += 1f;
        if ((CurrentDirection & MovementDirection.Left) != 0) movement.X -= 1f;
        if ((CurrentDirection & MovementDirection.Right) != 0) movement.X += 1f;

        return movement.Normalized();
    }

    public void ProcessMovement(float delta)
    {
        if (owner == null)
            return;

        if (stateMachine == null)
        {
            ApplyMove(owner, ResolveBaseSpeed());
            return;
        }

        switch (stateMachine.CurrentState)
        {
            case State.Moving:
            case State.Chasing:
            case State.Airborne:
                ApplyMove(owner, ResolveBaseSpeed());
                break;

            case State.Healing:
                ApplyMove(owner, ResolveBaseSpeed() * HealSpeedModifier);
                break;

            case State.Dodging:
                ApplyDodge(owner);
                break;

            case State.Knockback:
                break;

            default:
                owner.Velocity = Vector2.Zero;
                break;
        }
    }

    private void ApplyMove(CharacterBody2D body, float speed)
    {
        Vector2 movement = GetMovementVector();
        body.Velocity = movement * speed;
        body.MoveAndSlide();
    }

    public void SetDirectionFromVector(Vector2 direction, float deadZone = 0.1f)
    {
        if (direction.Length() <= deadZone)
        {
            CurrentDirection = MovementDirection.None;
            return;
        }

        Vector2 normalized = direction.Normalized();
        MovementDirection newDirection = MovementDirection.None;

        if (normalized.Y < -deadZone) newDirection |= MovementDirection.Up;
        if (normalized.Y > deadZone) newDirection |= MovementDirection.Down;
        if (normalized.X < -deadZone) newDirection |= MovementDirection.Left;
        if (normalized.X > deadZone) newDirection |= MovementDirection.Right;

        CurrentDirection = newDirection;
    }

    private void ApplyDodge(CharacterBody2D body)
    {
        if (GetDodgeVelocity != null)
        {
            body.Velocity = GetDodgeVelocity();
            body.MoveAndSlide();
            return;
        }

        body.Velocity = Vector2.Zero;
    }

    private float ResolveBaseSpeed()
    {
        if (GetBaseSpeed != null)
            return GetBaseSpeed();

        float baseSpeed = owner?.Speed ?? 0f;
        if (owner is Player player && player.Stats != null)
            baseSpeed = player.Stats.GetCurrentMax("Speed");
        return baseSpeed;
    }
}
