using Godot;
using System;

public static class Dodge
{
    public const float DashSpeed = DodgeDistance / DodgeDurationTotalSeconds;
    public static bool HasIFrames { get; set; } = true;
    public const float DodgeDistance = 70f;
    public static float DodgeStaminaCost { get; set; } = 20f;

    private static float _dodgeDuration = 0f;
    private static Vector2 _dodgeDirection = Vector2.Zero;
    private static bool _staminaUsedForCurrentDodge = false;
    private static bool _isDodging = false;
    private static bool _hasDodged = false;
    private static float _iFrameRemaining = 0f;

    public const float DodgeDurationTotalSeconds = 0.43f;
    public const float IFrameDuration = 0.2f;
    public static float DodgeDurationTotal => DodgeDurationTotalSeconds;
    public static float RemainingDuration => _dodgeDuration;
    public static bool IsIFrameActive => HasIFrames && _iFrameRemaining > 0f;

    public static bool IsDodging() => _isDodging;
    public static bool HasDodged() => _hasDodged;
    public static Vector2 GetDodgeDirection() => _dodgeDirection;
    
    public static float GetDodgeDuration() => DodgeDurationTotal;
    public static float GetDodgeDistance() => DodgeDistance;
    
    public static Vector2 GetDodgeVelocity()
    {
        if (DodgeDurationTotal <= 0f) return Vector2.Zero;
        return _dodgeDirection * DashSpeed;
    }
    
    public static bool CanDodge()
    {
        return Player.Instance?.ResourceManager != null && Player.Instance.ResourceManager.HasStamina(DodgeStaminaCost);
    }

    public static bool TryDodge(Vector2 movementVector)
    {
        if (!CanDodge()) return false;
        
        var stateMachine = PlayerStateMachine.Instance;
        if (stateMachine == null) return false;
        
        var currentState = stateMachine.CurrentState;
        if (currentState != PlayerState.Idle && 
            currentState != PlayerState.Moving && 
            currentState != PlayerState.Attacking && 
            currentState != PlayerState.Healing)
            return false;
        
        _dodgeDuration = DodgeDurationTotal;
        _dodgeDirection = movementVector;
        if (_dodgeDirection == Vector2.Zero)
            _dodgeDirection = Vector2.Right;

        if (HasIFrames)
            _iFrameRemaining = IFrameDuration;
        
        _isDodging = true;
        _hasDodged = false;
        
        return true;
    }
    
    public static void ProcessDodge(float delta)
    {
        if (!_isDodging) return;
        
        _dodgeDuration -= delta;
        if (_iFrameRemaining > 0f)
        {
            _iFrameRemaining -= delta;
            if (_iFrameRemaining < 0f)
                _iFrameRemaining = 0f;
        }
        if (_dodgeDuration <= 0)
        {
            EndDodge();
        }
    }
    
    public static void EndDodge()
    {
        _isDodging = false;
        _hasDodged = true;
        _dodgeDuration = 0f;
        _staminaUsedForCurrentDodge = false;
        if (_iFrameRemaining < 0f)
            _iFrameRemaining = 0f;
    }
    
    public static void ResetHasDodged()
    {
        _hasDodged = false;
    }
    
    public static void UseStamina()
    {
        if (!IsDodging())
        {
            _staminaUsedForCurrentDodge = false;
            return;
        }
        
        if (_staminaUsedForCurrentDodge) return;
            Player.Instance.ResourceManager.TryUseStamina(DodgeStaminaCost);
            _staminaUsedForCurrentDodge = true;
    }
}
