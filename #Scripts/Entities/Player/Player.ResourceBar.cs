using Godot;

public partial class Player
{
    private float staminaRegenerationTimer = 0f;
    public float staminaRegenCooldown = 1.2f;

    protected override float GetHealth() => ResourceManager.Instance?.Health ?? Stats?.GetCurrent("Health") ?? base.GetHealth();
    protected override float GetMaxHealth() => ResourceManager.Instance?.MaxHealth ?? Stats?.GetCurrentMax("Health") ?? base.GetMaxHealth();
    protected override void SetHealth(float value)
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.SetHealth(value);
        else if (Stats != null)
            Stats.SetCurrent("Health", value);
        else
            base.SetHealth(value);
    }
    protected override void SetMaxHealth(float value)
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.SetMaxHealth(value);
        else if (Stats != null)
            Stats.SetCurrentMax("Health", value);
        else
            base.SetMaxHealth(value);
    }

    private void PassiveStaminaRegeneration(float delta)
    {
        staminaRegenerationTimer -= delta;

        if (staminaRegenerationTimer > 0) return;

        float currentStamina = ResourceManager.Instance?.Stamina ?? Stats.GetCurrent("Stamina");
        float maxStamina = ResourceManager.Instance?.MaxStamina ?? Stats.GetCurrentMax("Stamina");
        float regenRate = ResourceManager.Instance?.StaminaRegenRate ?? Stats.GetCurrentMax("Stamina Regen");

        if (currentStamina < maxStamina)
        {
            float newStamina = Mathf.Min(currentStamina + regenRate * delta, maxStamina);
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.SetStamina(newStamina);
            else
                Stats.SetCurrent("Stamina", newStamina);
        }
    }

}