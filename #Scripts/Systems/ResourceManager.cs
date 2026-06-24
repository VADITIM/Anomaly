using Godot;
using System;

public partial class ResourceManager : IEntityBehavior
{
    public static ResourceManager Instance { get; private set; }

    private Player _player;
    private float _specialCooldownTimer = 0f;
    private float _specialCooldownDuration = 0f;
    private float _healthSFillTimer = 0f;
    private const float HealthSFillDuration = 1.2f;
    private const float HealConsumptionDuration = HealthSFillDuration * 0.5f;
    private int _consecutiveHitCount = 0;
    private float _healConsumptionTimer = 0f;
    private bool _isHealing = false;
    private float _healStartHealth = 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnXpChanged;
    public event Action<int> OnLevelUp;
    public event Action<float, float> OnCorruptionChanged;
    public event Action<float, float> OnVesselChanged;
    public event Action<float, float> OnHealthSChanged;
    public event Action<float, float> OnStaminaSChanged;

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

    public float Corruption
    {
        get => _player?.Stats.GetCurrent("Corruption") ?? 0f;
        set => SetResourceValue("Corruption", value, OnCorruptionChanged);
    }

    public float Vessel
    {
        get => _player?.Stats.GetCurrent("Vessel") ?? 0f;
        set => SetResourceValue("Vessel", value, OnVesselChanged);
    }

    public float HealthS
    {
        get => _player?.Stats.GetCurrent("Health S") ?? 0f;
        set => SetResourceValue("Health S", value, OnHealthSChanged);
    }

    public float StaminaS
    {
        get => _player?.Stats.GetCurrent("Stamina S") ?? 0f;
        set => SetResourceValue("Stamina S", value, OnStaminaSChanged);
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

    public void OnReady(Entity owner)
    {
    }

    public void OnProcess(double delta)
    {
        UpdateSpecialCooldown((float)delta);
        ProcessHealing((float)delta);
    }

    public void OnPhysicsProcess(double delta)
    {
    }

    public void OnExitTree()
    {
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

    public void AddCorruption(float amount)
    {
        AddResourceProgress("Corruption", amount, OnCorruptionChanged);
    }

    public void AddVesselCharge(float damageDealt, float playerMaxHealth)
    {
        if (damageDealt <= 0f)
            return;

        // Don't add vessel while Health S is filling
        if (_healthSFillTimer > 0f)
            return;

        _consecutiveHitCount++;
        float consecutiveMultiplier = _consecutiveHitCount * 1.3f;
        float vesselGain = 1f * consecutiveMultiplier;
        float currentVessel = _player.Stats.GetCurrent("Vessel");

        float newVessel = Mathf.Min(100f, currentVessel + vesselGain);
        _player.Stats.SetCurrent("Vessel", newVessel);

        if (!Mathf.IsEqualApprox(currentVessel, newVessel))
            OnVesselChanged?.Invoke(newVessel, _player.Stats.GetCurrentMax("Vessel"));

        if (newVessel >= 100f)
        {
            _player.Stats.SetCurrent("Vessel", 0f);
            OnVesselChanged?.Invoke(0f, _player.Stats.GetCurrentMax("Vessel"));
            StartHealthSFill();
        }
    }

    public void ResetConsecutiveHits()
    {
        _consecutiveHitCount = 0;
    }

    public void StartHealing(float healDuration)
    {
        _isHealing = true;
        _healConsumptionTimer = HealConsumptionDuration;
        _healStartHealth = _player.Stats.GetCurrent("Health");
    }

    public void EndHealing()
    {
        _isHealing = false;
        _healConsumptionTimer = 0f;
    }

    private void ProcessHealing(float delta)
    {
        if (!_isHealing || _healConsumptionTimer <= 0f)
            return;

        _healConsumptionTimer -= delta;

        float healthSCurrent = _player.Stats.GetCurrent("Health S");
        float vesselCurrent = _player.Stats.GetCurrent("Vessel");

        if (healthSCurrent <= 0f || vesselCurrent <= 0f)
        {
            _healConsumptionTimer = 0f;
            return;
        }

        float consumptionProgress = 1f - (_healConsumptionTimer / HealConsumptionDuration);
        consumptionProgress = Mathf.Clamp(consumptionProgress, 0f, 1f);

        float newHealthS = Mathf.Lerp(_player.Stats.GetCurrent("Health S"), 0f, consumptionProgress);
        float newVessel = Mathf.Lerp(_player.Stats.GetCurrent("Vessel"), 0f, consumptionProgress);

        _player.Stats.SetCurrent("Health S", newHealthS);
        _player.Stats.SetCurrent("Vessel", newVessel);

        OnHealthSChanged?.Invoke(newHealthS, _player.Stats.GetCurrentMax("Health S"));
        OnVesselChanged?.Invoke(newVessel, _player.Stats.GetCurrentMax("Vessel"));

        float healAmount = healthSCurrent * (consumptionProgress / HealConsumptionDuration) * delta;
        float currentHealth = _player.Stats.GetCurrent("Health");
        float newHealth = Mathf.Min(currentHealth + healAmount, _player.Stats.GetCurrentMax("Health"));
        _player.Stats.SetCurrent("Health", newHealth);

        if (!Mathf.IsEqualApprox(currentHealth, newHealth))
            OnHealthChanged?.Invoke(newHealth, _player.Stats.GetCurrentMax("Health"));
    }

    public bool HasSpecialAttackReady()
    {
        return _specialCooldownTimer <= 0f;
    }

    public float GetSpecialAttackProgress()
    {
        if (_specialCooldownDuration <= 0f)
            return 100f;

        float readyProgress = 1f - (_specialCooldownTimer / _specialCooldownDuration);
        return Mathf.Clamp(readyProgress * 100f, 0f, 100f);
    }

    public void StartSpecialCooldown(float duration)
    {
        _specialCooldownDuration = Mathf.Max(0.1f, duration);
        _specialCooldownTimer = _specialCooldownDuration;
        SyncSpecialCooldownBar();
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

    private void SetResourceValue(string statName, float value, Action<float, float> changedEvent)
    {
        if (_player?.Stats == null)
            return;

        float oldValue = _player.Stats.GetCurrent(statName);
        _player.Stats.SetCurrent(statName, value);
        if (!Mathf.IsEqualApprox(oldValue, _player.Stats.GetCurrent(statName)))
            changedEvent?.Invoke(_player.Stats.GetCurrent(statName), _player.Stats.GetCurrentMax(statName));
    }

    private float AddResourceProgress(string statName, float amount, Action<float, float> changedEvent)
    {
        if (_player?.Stats == null || amount <= 0f)
            return 0f;

        float current = _player.Stats.GetCurrent(statName);
        float max = Mathf.Max(0f, _player.Stats.GetCurrentMax(statName));
        float newValue = current + amount;
        float overflow = Mathf.Max(0f, newValue - max);
        _player.Stats.SetCurrent(statName, newValue);

        float appliedValue = _player.Stats.GetCurrent(statName);
        if (!Mathf.IsEqualApprox(current, appliedValue))
            changedEvent?.Invoke(appliedValue, max);

        return overflow;
    }

    private void StartHealthSFill()
    {
        if (_healthSFillTimer > 0f)
            return;

        _healthSFillTimer = HealthSFillDuration;
        _player.Stats.SetCurrent("Health S", 0f);
        OnHealthSChanged?.Invoke(0f, _player.Stats.GetCurrentMax("Health S"));
    }

    private void UpdateSpecialCooldown(float delta)
    {
        if (_player?.Stats == null)
            return;

        if (_healthSFillTimer > 0f)
        {
            _healthSFillTimer = Mathf.Max(0f, _healthSFillTimer - delta);

            float healthSProgress = 1f - (_healthSFillTimer / HealthSFillDuration);
            float healthSValue = Mathf.Clamp(healthSProgress * 100f, 0f, 100f);
            _player.Stats.SetCurrent("Health S", healthSValue);
            OnHealthSChanged?.Invoke(healthSValue, _player.Stats.GetCurrentMax("Health S"));

            if (_healthSFillTimer <= 0f)
            {
                _healthSFillTimer = 0f;
                _player.Stats.SetCurrent("Health S", 100f);
                OnHealthSChanged?.Invoke(100f, _player.Stats.GetCurrentMax("Health S"));
            }
        }

        if (_specialCooldownTimer > 0f)
        {
            _specialCooldownTimer = Mathf.Max(0f, _specialCooldownTimer - delta);
            SyncSpecialCooldownBar();
            return;
        }

        if (!Mathf.IsEqualApprox(_player.Stats.GetCurrent("Stamina S"), 100f))
            SyncSpecialCooldownBar();
    }

    private void SyncSpecialCooldownBar()
    {
        float progress = GetSpecialAttackProgress();
        _player.Stats.SetCurrent("Stamina S", progress);
        OnStaminaSChanged?.Invoke(_player.Stats.GetCurrent("Stamina S"), _player.Stats.GetCurrentMax("Stamina S"));
    }
}
