using Godot;
using System;

public class DodgeBehavior : IEntityBehavior
{
    public const float DodgeDistance = 85f;
    public const float DodgeDurationTotalSeconds = 0.36f;
    public const float IFrameDuration = 0.2f;

    public bool HasIFrames { get; set; } = true;
    public float DodgeStaminaCost { get; set; } = 20f;

    public float DodgeDurationTotal => DodgeDurationTotalSeconds;
    public float RemainingDuration => dodgeDuration;
    public bool IsIFrameActive => HasIFrames && iFramesLeft > 0f;

    public bool IsDodging => isDodging;
    public bool HasDodged => hasDodged;

    public Func<bool> HasStamina { get; set; }
    public Func<bool> TryUseStamina { get; set; }

    private Entity owner;
    private StateMachine stateMachine;

    private float dodgeDuration = 0f;
    private Vector2 dodgeDirection = Vector2.Zero;
    private bool staminaUsedForCurrentDodge = false;
    private bool isDodging = false;
    private bool hasDodged = false;
    private float iFramesLeft = 0f;

    public void OnReady(Entity owner)
    {
        this.owner = owner;
        stateMachine = owner?.StateMachine;
    }

    public void OnProcess(double delta)
    {
        if (!isDodging)
        {
            staminaUsedForCurrentDodge = false;
            return;
        }

        if (!staminaUsedForCurrentDodge && TryUseStamina != null)
        {
            if (TryUseStamina())
                staminaUsedForCurrentDodge = true;
        }
    }

    public void OnPhysicsProcess(double delta)
    {
    }

    public void OnExitTree()
    {
    }

    public Vector2 GetDodgeDirection() => dodgeDirection;

    public float GetDodgeDuration() => DodgeDurationTotal;

    public float GetDodgeDistance() => DodgeDistance;

    public Vector2 GetDodgeVelocity()
    {
        if (DodgeDurationTotal <= 0f) return Vector2.Zero;
        float dashSpeed = DodgeDistance / DodgeDurationTotal;
        return dodgeDirection * dashSpeed;
    }

    public bool CanDodge()
    {
        return HasStamina != null && HasStamina();
    }

    public bool TryDodge(Vector2 movementVector)
    {
        if (!CanDodge()) return false;
        if (stateMachine == null) return false;

        var currentState = stateMachine.CurrentState;
        if (currentState != State.Idle &&
            currentState != State.Moving &&
            currentState != State.Airborne &&
            currentState != State.Attacking &&
            currentState != State.Healing)
            return false;

        dodgeDuration = DodgeDurationTotal;
        dodgeDirection = movementVector;
        if (dodgeDirection == Vector2.Zero)
            dodgeDirection = Vector2.Right;

        if (HasIFrames)
            iFramesLeft = IFrameDuration;

        isDodging = true;
        hasDodged = false;

        return true;
    }

    public void ProcessDodge(float delta)
    {
        if (!isDodging) return;

        dodgeDuration -= delta;
        if (iFramesLeft > 0f)
        {
            iFramesLeft -= delta;
            if (iFramesLeft < 0f)
                iFramesLeft = 0f;
        }
        if (dodgeDuration <= 0)
        {
            EndDodge();
        }
    }

    public void EndDodge()
    {
        isDodging = false;
        hasDodged = true;
        dodgeDuration = 0f;
        staminaUsedForCurrentDodge = false;
        if (iFramesLeft < 0f)
            iFramesLeft = 0f;
    }

    public void ResetHasDodged()
    {
        hasDodged = false;
    }
}
