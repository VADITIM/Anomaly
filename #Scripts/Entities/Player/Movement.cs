using Godot;
using System;

public static class Movement
{
    public static Player.MovementDirection CurrentMovementDirection { get;  set; } = Player.MovementDirection.None;
    private const float HEAL_SPEED_MODIFIER = 0.2f;

    public static Vector2 GetMovementVector()
    {
        Vector2 movement = Vector2.Zero;
        
        if ((CurrentMovementDirection & Player.MovementDirection.Up) != 0) movement.Y -= 1;
        if ((CurrentMovementDirection & Player.MovementDirection.Down) != 0) movement.Y += 1;
        if ((CurrentMovementDirection & Player.MovementDirection.Left) != 0) movement.X -= 1;
        if ((CurrentMovementDirection & Player.MovementDirection.Right) != 0) movement.X += 1;
        
        return movement.Normalized();
    }
    
    public static float GetSpeedModifier()
    {
        var sm = PlayerStateMachine.Instance;
        if (sm != null && sm.CurrentState == PlayerState.Healing)
            return HEAL_SPEED_MODIFIER;
        return 1f;
    }

    public static Vector2 GetInputVector() { return GetMovementVector(); }
    
    public static bool IsMoving() { return PlayerStateMachine.Instance?.IsMoving ?? false; }

    public static void ProcessMovement(this CharacterBody2D body, float delta)
    {
        var sm = PlayerStateMachine.Instance;
        if (sm == null || Player.Instance == null)
            return;

        switch (sm.CurrentState)
        {
            case PlayerState.Moving:
                Vector2 movement = GetMovementVector();
                body.Velocity = movement * Player.Instance.Stats.GetCurrentMax("Speed");
                body.MoveAndSlide();
                break;

            case PlayerState.Healing:
                Vector2 healMovement = GetMovementVector();
                float healSpeed = Player.Instance.Stats.GetCurrentMax("Speed") * GetSpeedModifier();
                body.Velocity = healMovement * healSpeed;
                body.MoveAndSlide();
                break;

            case PlayerState.Dodging:
                body.Velocity = Dodge.GetDodgeVelocity();
                body.MoveAndSlide();
                break;

            case PlayerState.Knockback:
                body.Velocity = sm.GetKnockbackVelocity();
                body.MoveAndSlide();
                break;

            default:
                body.Velocity = Vector2.Zero;
                break;
        }
    }
}
