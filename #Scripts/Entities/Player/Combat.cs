using Godot;
using System;

public static partial class Combat
{
    public static bool IsAttacking()
    {
        return PlayerStateMachine.Instance?.IsAttacking ?? false;
    }
    
    public static bool IsChargingHeavy()
    {
        return PlayerStateMachine.Instance?.IsChargingHeavy ?? false;
    }
    
    public static bool IsHeavyAttacking()
    {
        return PlayerStateMachine.Instance?.IsHeavyAttacking ?? false;
    }
    
    public static float HeavyChargeProgress()
    {
        return PlayerStateMachine.Instance?.HeavyChargeProgress ?? 0f;
    }

    public static float GetHeavyDamageMultiplier()
    {
        float charge = HeavyChargeProgress();
        return 1f + (2f * charge); 
    }
}
