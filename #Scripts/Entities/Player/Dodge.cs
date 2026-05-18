using Godot;
using System;

public static class Dodge
{
    public const float DashSpeed = DodgeDistance / DodgeDurationTotalSeconds;
    public static bool HasIFrames { get; set; } = true;
    public const float DodgeDistance = 70f;
    public static float DodgeStaminaCost { get; set; } = 20f;

    private static float dodgeDuration = 0f;
    private static Vector2 dodgeDirection = Vector2.Zero;
    private static bool staminaUsedForCurrentDodge = false;
    private static bool isDodging = false;
    private static bool hasDodged = false;
    private static float iFramesLeft = 0f;

    public const float DodgeDurationTotalSeconds = 0.43f;
    public const float IFrameDuration = 0.2f;
    public static float DodgeDurationTotal => DodgeDurationTotalSeconds;
    public static float RemainingDuration => dodgeDuration;
    public static bool IsIFrameActive => HasIFrames && iFramesLeft > 0f;

    public static bool IsDodging() => isDodging;
    public static bool HasDodged() => hasDodged;
    public static Vector2 GetDodgeDirection() => dodgeDirection;
    
    public static float GetDodgeDuration() => DodgeDurationTotal;
    public static float GetDodgeDistance() => DodgeDistance;
    
    public static Vector2 GetDodgeVelocity()
    {
        if (DodgeDurationTotal <= 0f) return Vector2.Zero;
        return dodgeDirection * DashSpeed;
    }
    
    public static bool CanDodge()
    {
        return Player.Instance?.ResourceManager != null && Player.Instance.ResourceManager.HasStamina(DodgeStaminaCost);
    }

    public static bool TryDodge(Vector2 movementVector)
    {
        if (!CanDodge()) return false;
        
        var stateMachine = Player.Instance?.StateMachine;
        if (stateMachine == null) return false;
        
        var currentState = stateMachine.CurrentState;
        if (currentState != PlayerState.Idle && 
            currentState != PlayerState.Moving && 
            currentState != PlayerState.Airborne && 
            currentState != PlayerState.Attacking && 
            currentState != PlayerState.Healing)
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
    
    public static void ProcessDodge(float delta)
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
    
    public static void EndDodge()
    {
        isDodging = false;
        hasDodged = true;
        dodgeDuration = 0f;
        staminaUsedForCurrentDodge = false;
        if (iFramesLeft < 0f)
            iFramesLeft = 0f;
    }
    
    public static void ResetHasDodged()
    {
        hasDodged = false;
    }
    
    public static void UseStamina()
    {
        if (!IsDodging())
        {
            staminaUsedForCurrentDodge = false;
            return;
        }
        
        if (staminaUsedForCurrentDodge) return;
            Player.Instance.ResourceManager.TryUseStamina(DodgeStaminaCost);
            staminaUsedForCurrentDodge = true;
    }
}
