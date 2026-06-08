using Godot;
using System;

public class TenacityBehavior : IEntityBehavior
{
    public float DefaultStaggerDuration { get; set; } = TenacityDefaults.DefaultStaggerDuration;
    public float DefaultRecoveryDuration { get; set; } = TenacityDefaults.DefaultRecoveryDuration;
    public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;

    public Func<float> GetCurrentTenacity { get; set; }
    public Action<float> SetCurrentTenacity { get; set; }
    public Func<float> GetMaxTenacity { get; set; }
    public Action<float> SetMaxTenacity { get; set; }

    private Entity owner;

    public void OnReady(Entity owner)
    {
        this.owner = owner;
    }

    public void OnProcess(double delta)
    {
    }

    public void OnPhysicsProcess(double delta)
    {
    }

    public void OnExitTree()
    {
    }

    public float Current => GetCurrentTenacity?.Invoke() ?? 0f;
    public float Max => GetMaxTenacity?.Invoke() ?? 0f;

    public void InitializeToMax()
    {
        if (SetCurrentTenacity == null)
            return;

        float max = GetMaxTenacity?.Invoke() ?? 0f;
        SetCurrentTenacity(Mathf.Clamp(max, 0f, max));
    }

    public float ApplyDamage(float amount)
    {
        if (amount <= 0f || SetCurrentTenacity == null)
            return Current;

        float newValue = Mathf.Max(0f, Current - amount);
        SetCurrentTenacity(newValue);
        return newValue;
    }

    public void ResetToMax()
    {
        if (SetCurrentTenacity == null)
            return;

        float max = Mathf.Max(0f, GetMaxTenacity?.Invoke() ?? 0f);
        SetCurrentTenacity(max);
    }
}