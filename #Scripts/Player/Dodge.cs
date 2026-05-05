using Godot;
using System;

public static class Dodge
{
    #region Configuration
    public static float DashSpeed { get; set; } = 600f;
    public static bool HasIFrames { get; set; } = true;
    public static float DodgeDistance { get; set; } = 100f;
    public static float DodgeStaminaCost { get; set; } = 20f;
    #endregion

    #region State
    private static float _dodgeDuration = 0f;
    private static Vector2 _dodgeDirection = Vector2.Zero;
    private static bool _staminaUsedForCurrentDodge = false;
    private static bool _isDodging = false;
    private static bool _hasDodged = false;
    #endregion

    #region Properties
    public static float DefaultDodgeDuration { get; set; } = 0.50f;
    public static float DodgeDurationTotal => Gui.dodgeAnimationSpeed > 0f ? Gui.dodgeAnimationSpeed : DefaultDodgeDuration;
    public static float RemainingDuration => _dodgeDuration;
    #endregion

    #region Query Methods
    public static bool IsDodging() => _isDodging;
    public static bool HasDodged() => _hasDodged;
    public static Vector2 GetDodgeDirection() => _dodgeDirection;
    
    public static float GetDodgeDuration() => DodgeDurationTotal;
    public static float GetDodgeDistance() => DodgeDistance;
    
    public static Vector2 GetDodgeVelocity()
    {
        float total = DodgeDurationTotal;
        if (total <= 0f) return Vector2.Zero;
        float speed = DodgeDistance / total;
        return _dodgeDirection * speed;
    }
    
    public static bool CanDodge()
    {
        return Player.Instance?.ResourceManager != null && Player.Instance.ResourceManager.HasStamina(DodgeStaminaCost);
    }
    #endregion

    #region Actions
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
        
        _isDodging = true;
        _hasDodged = false;
        
        return true;
    }
    
    public static void ProcessDodge(float delta)
    {
        if (!_isDodging) return;
        
        _dodgeDuration -= delta;
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
    #endregion
}
