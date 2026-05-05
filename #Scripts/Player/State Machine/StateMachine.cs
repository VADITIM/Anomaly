public static class StateMachine
{
    public static PlayerState CurrentState => PlayerStateMachine.Instance?.CurrentState ?? PlayerState.Idle;
    
    public static bool IsAttacking() => PlayerStateMachine.Instance?.IsAttacking ?? false;
    public static bool IsHeavyPressed() => PlayerStateMachine.Instance?.IsChargingHeavy ?? false;
    public static bool IsMoving() => PlayerStateMachine.Instance?.IsMoving ?? false;
    public static bool IsDodging() => PlayerStateMachine.Instance?.IsDodging ?? false;
    public static bool HasDodged() => PlayerStateMachine.Instance?.HasDodged ?? false;
    public static bool IsHealing() => PlayerStateMachine.Instance?.IsHealing ?? false;
    
    public static bool ExecuteAttack() => PlayerStateMachine.Instance?.CurrentState == PlayerState.Attacking;
    public static bool ExecuteHeavy() => PlayerStateMachine.Instance?.CurrentState == PlayerState.HeavyAttacking;
    public static bool ExecuteDodge() => PlayerStateMachine.Instance?.IsDodging ?? false;
    public static bool ExecuteAnyAttack() => PlayerStateMachine.Instance?.IsAttacking ?? false;
    
    public static bool CanPlayerAct() => PlayerStateMachine.Instance?.CanPerformAction ?? false;
    public static float heavyChargePercent => PlayerStateMachine.Instance?.HeavyChargeProgress ?? 0f;
}