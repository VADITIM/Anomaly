using Godot;

public partial class Player
{
    private float _staminaRegenerationTimer = 0f;
    private const float StaminaRegenCooldown = 1.2f;

    // ResourceManager is created in _Ready; base-class calls before that fall back to Stats.
    protected override float GetHealth() => ResourceManager?.Health ?? Stats?.GetCurrent(StatType.Health) ?? base.GetHealth();
    protected override float GetMaxHealth() => ResourceManager?.MaxHealth ?? Stats?.GetCurrentMax(StatType.Health) ?? base.GetMaxHealth();

    protected override void SetHealth(float value)
    {
        if (ResourceManager != null)
            ResourceManager.SetHealth(value);
        else if (Stats != null)
            Stats.SetCurrent(StatType.Health, value);
        else
            base.SetHealth(value);
    }

    protected override void SetMaxHealth(float value)
    {
        if (ResourceManager != null)
            ResourceManager.SetMaxHealth(value);
        else if (Stats != null)
            Stats.SetCurrentMax(StatType.Health, value);
        else
            base.SetMaxHealth(value);
    }

    private void PassiveStaminaRegeneration(float delta)
    {
        _staminaRegenerationTimer -= delta;

        if (_staminaRegenerationTimer > 0) return;

        float currentStamina = ResourceManager.Stamina;
        float maxStamina = ResourceManager.MaxStamina;
        float regenRate = ResourceManager.StaminaRegenRate;

        if (currentStamina < maxStamina)
            ResourceManager.SetStamina(Mathf.Min(currentStamina + regenRate * delta, maxStamina));
    }
}
