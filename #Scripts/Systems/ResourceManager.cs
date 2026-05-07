using Godot;
using System;

public partial class ResourceManager
{
    public static ResourceManager Instance { get; private set; }

    private Player _player;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnXpChanged;
    public event Action<int> OnLevelUp;

    public float Health
    {
        get => _player?.Stats.GetCurrent("Health") ?? 0;
        set
        {
            float oldValue = _player.Stats.GetCurrent("Health");
            _player.Stats.SetCurrent("Health", value);
            if (oldValue != value)
                OnHealthChanged?.Invoke(_player.Stats.GetCurrent("Health"), _player.Stats.GetCurrentMax("Health"));
        }
    }

    public float MaxHealth
    {
        get => _player?.Stats.GetCurrentMax("Health") ?? 0;
        set
        {
            _player.Stats.SetCurrentMax("Health", value);
            OnHealthChanged?.Invoke(_player.Stats.GetCurrent("Health"), _player.Stats.GetCurrentMax("Health"));
        }
    }

    public float Stamina
    {
        get => _player?.Stats.GetCurrent("Stamina") ?? 0;
        set
        {
            float oldValue = _player.Stats.GetCurrent("Stamina");
            _player.Stats.SetCurrent("Stamina", value);
            if (oldValue != value)
                OnStaminaChanged?.Invoke(_player.Stats.GetCurrent("Stamina"), _player.Stats.GetCurrentMax("Stamina"));
        }
    }

    public float MaxStamina
    {
        get => _player?.Stats.GetCurrentMax("Stamina") ?? 0;
        set
        {
            _player.Stats.SetCurrentMax("Stamina", value);
            OnStaminaChanged?.Invoke(_player.Stats.GetCurrent("Stamina"), _player.Stats.GetCurrentMax("Stamina"));
        }
    }

    public float StaminaRegenRate
    {
        get => _player?.Stats.GetCurrentMax("Stamina Regen") ?? 0;
        set
        {
            _player.Stats.SetCurrentMax("Stamina Regen", value);
        }
    }

    public float Xp
    {
        get => _player?.Stats.GetCurrent("Vessel") ?? 0;
        set
        {
            float oldValue = _player.Stats.GetCurrent("Vessel");
            _player.Stats.SetCurrent("Vessel", value);
            if (oldValue != value)
                OnXpChanged?.Invoke(_player.Stats.GetCurrent("Vessel"), _player.Stats.GetCurrentMax("Vessel"));
        }
    }

    public float MaxXp
    {
        get => _player?.Stats.GetCurrentMax("Vessel") ?? 0;
        set
        {
            _player.Stats.SetCurrentMax("Vessel", value);
            OnXpChanged?.Invoke(_player.Stats.GetCurrent("Vessel"), _player.Stats.GetCurrentMax("Vessel"));
        }
    }

    public ResourceManager(Player player)
    {
        _player = player;
        Instance = this;
    }

    public void SetHealth(float value)
    {
        float oldHealth = _player.Stats.GetCurrent("Health");
        _player.Stats.SetCurrent("Health", Mathf.Clamp(value, 0, _player.Stats.GetCurrentMax("Health")));

        if (_player.Stats.GetCurrent("Health") != oldHealth)
            OnHealthChanged?.Invoke(_player.Stats.GetCurrent("Health"), _player.Stats.GetCurrentMax("Health"));
    }

    public void TakeDamage(float damage) { SetHealth(Health - damage); }
    public void Heal(float amount) { SetHealth(Health + amount); }

    public void SetMaxHealth(float value)
    {
        _player.Stats.SetCurrentMax("Health", Mathf.Clamp(value, 1, _player.Stats.GetTotalMax("Health")));
        OnHealthChanged?.Invoke(_player.Stats.GetCurrent("Health"), _player.Stats.GetCurrentMax("Health"));
    }

    public bool IsDead() => _player != null && _player.Stats.GetCurrent("Health") <= 0;

    public void SetStamina(float value)
    {
        float oldStamina = _player.Stats.GetCurrent("Stamina");
        _player.Stats.SetCurrent("Stamina", Mathf.Clamp(value, 0, _player.Stats.GetCurrentMax("Stamina")));
        if (_player.Stats.GetCurrent("Stamina") != oldStamina)
            OnStaminaChanged?.Invoke(_player.Stats.GetCurrent("Stamina"), _player.Stats.GetCurrentMax("Stamina"));
    }

    public bool TryUseStamina(float cost)
    {
        if (_player.Stats.GetCurrent("Stamina") >= cost)
        {
            SetStamina(_player.Stats.GetCurrent("Stamina") - cost);
            _player.OnActionPerformed();
            return true;
        }
        return false;
    }

    public bool HasStamina(float cost) { return _player != null && _player.Stats.GetCurrent("Stamina") >= cost; }

    public void SetMaxStamina(float value)
    {
        _player.Stats.SetCurrentMax("Stamina", Mathf.Clamp(value, 1, _player.Stats.GetTotalMax("Stamina")));
        OnStaminaChanged?.Invoke(_player.Stats.GetCurrent("Stamina"), _player.Stats.GetCurrentMax("Stamina"));
    }

    public void AddXp(float amount)
    {
        float currentXp = _player.Stats.GetCurrent("Vessel");
        _player.Stats.SetCurrent("Vessel", currentXp + amount);

        OnXpChanged?.Invoke(_player.Stats.GetCurrent("Vessel"), _player.Stats.GetCurrentMax("Vessel"));
    }

    public void SetMaxXp(float value)
    {
        _player.Stats.SetCurrentMax("Vessel", Mathf.Clamp(value, 1, _player.Stats.GetTotalMax("Vessel")));
    }

    public void SetXp(float value)
    {
        _player.Stats.SetCurrent("Vessel", Mathf.Clamp(value, 0, _player.Stats.GetCurrentMax("Vessel")));
        OnXpChanged?.Invoke(_player.Stats.GetCurrent("Vessel"), _player.Stats.GetCurrentMax("Vessel"));
    }
}
