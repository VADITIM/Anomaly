using Godot;

public partial class Player
{
    private float _staminaRegenerationCooldown = 0f;
    public float STAMINA_REGEN_COOLDOWN = 1.5f;

    protected override float GetHealth() => Stats?.GetCurrent("Health") ?? base.GetHealth();
    protected override float GetMaxHealth() => Stats?.GetCurrentMax("Health") ?? base.GetMaxHealth();
    protected override void SetHealth(float value)
    {
        if (Stats != null)
            Stats.SetCurrent("Health", value);
        else
            base.SetHealth(value);
    }
    protected override void SetMaxHealth(float value)
    {
        if (Stats != null)
            Stats.SetCurrentMax("Health", value);
        else
            base.SetMaxHealth(value);
    }

    private void PassiveStaminaRegeneration(float delta)
    {
        _staminaRegenerationCooldown -= delta;

        if (_staminaRegenerationCooldown > 0) return;

        float currentStamina = Stats.GetCurrent("Stamina");
        float maxStamina = Stats.GetCurrentMax("Stamina");
        float regenRate = Stats.GetCurrentMax("Stamina Regen");
        
        if (currentStamina < maxStamina)
        {
            float newStamina = Mathf.Min(currentStamina + regenRate * delta, maxStamina);
            Stats.SetCurrent("Stamina", newStamina);
        }
    }

}