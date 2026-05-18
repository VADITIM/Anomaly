using Godot;
using System;

public static partial class Combat
{
    public static bool IsAttacking()
    {
        return Player.Instance?.StateMachine?.IsAttacking ?? false;
    }
    
    public static bool IsChargingHeavy()
    {
        return Player.Instance?.StateMachine?.IsChargingHeavy ?? false;
    }
    
    public static bool IsHeavyAttacking()
    {
        return Player.Instance?.StateMachine?.IsHeavyAttacking ?? false;
    }
    
    public static float HeavyChargeProgress()
    {
        return Player.Instance?.StateMachine?.HeavyChargeProgress ?? 0f;
    }

    public static float GetHeavyDamageMultiplier()
    {
        float charge = HeavyChargeProgress();
        return 1f + (2f * charge); 
    }
}
