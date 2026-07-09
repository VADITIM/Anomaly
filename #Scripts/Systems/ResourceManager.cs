using Godot;
using System;

public class ResourceManager : IEntityBehavior
{
    private readonly Player _player;
    private float _specialCooldownTimer = 0f;
    private float _specialCooldownDuration = 0f;
    private float _healthSFillTimer = 0f;
    private const float HealthSFillDuration = 1.2f;
    private const float HealConsumptionDuration = HealthSFillDuration * 0.5f;
    private int _consecutiveHitCount = 0;
    private float _healConsumptionTimer = 0f;
    private bool _isHealing = false;
    private float _healStartHealthS = 0f;
    private float _healStartVessel = 0f;
    private float _healLastProgress = 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnCorruptionChanged;
    public event Action<float, float> OnVesselChanged;
    public event Action<float, float> OnHealthSChanged;
    public event Action<float, float> OnStaminaSChanged;

    public ResourceManager(Player player)
    {
        _player = player;
    }

    public float Health
    {
        get => _player.Stats.GetCurrent(StatType.Health);
        set => SetHealth(value);
    }

    public float MaxHealth
    {
        get => _player.Stats.GetCurrentMax(StatType.Health);
        set => SetMaxHealth(value);
    }

    public float Stamina
    {
        get => _player.Stats.GetCurrent(StatType.Stamina);
        set => SetStamina(value);
    }

    public float MaxStamina
    {
        get => _player.Stats.GetCurrentMax(StatType.Stamina);
        set => SetMaxStamina(value);
    }

    public float StaminaRegenRate
    {
        get => _player.Stats.GetCurrentMax(StatType.StaminaRegen);
        set => _player.Stats.SetCurrentMax(StatType.StaminaRegen, value);
    }

    public float Corruption
    {
        get => _player.Stats.GetCurrent(StatType.Corruption);
        set => SetResourceValue(StatType.Corruption, value, OnCorruptionChanged);
    }

    public float Vessel
    {
        get => _player.Stats.GetCurrent(StatType.Vessel);
        set => SetResourceValue(StatType.Vessel, value, OnVesselChanged);
    }

    public float HealthS
    {
        get => _player.Stats.GetCurrent(StatType.HealthS);
        set => SetResourceValue(StatType.HealthS, value, OnHealthSChanged);
    }

    public float StaminaS
    {
        get => _player.Stats.GetCurrent(StatType.StaminaS);
        set => SetResourceValue(StatType.StaminaS, value, OnStaminaSChanged);
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
        float oldHealth = _player.Stats.GetCurrent(StatType.Health);
        _player.Stats.SetCurrent(StatType.Health, Mathf.Clamp(value, 0, _player.Stats.GetCurrentMax(StatType.Health)));

        if (_player.Stats.GetCurrent(StatType.Health) != oldHealth)
            OnHealthChanged?.Invoke(_player.Stats.GetCurrent(StatType.Health), _player.Stats.GetCurrentMax(StatType.Health));
    }

    public void TakeDamage(float damage) { SetHealth(Health - damage); }
    public void Heal(float amount) { SetHealth(Health + amount); }

    public void SetMaxHealth(float value)
    {
        _player.Stats.SetCurrentMax(StatType.Health, Mathf.Clamp(value, 1, _player.Stats.GetTotalMax(StatType.Health)));
        OnHealthChanged?.Invoke(_player.Stats.GetCurrent(StatType.Health), _player.Stats.GetCurrentMax(StatType.Health));
    }

    public bool IsDead() => _player.Stats.GetCurrent(StatType.Health) <= 0;

    public void SetStamina(float value)
    {
        float oldStamina = _player.Stats.GetCurrent(StatType.Stamina);
        _player.Stats.SetCurrent(StatType.Stamina, Mathf.Clamp(value, 0, _player.Stats.GetCurrentMax(StatType.Stamina)));
        if (_player.Stats.GetCurrent(StatType.Stamina) != oldStamina)
            OnStaminaChanged?.Invoke(_player.Stats.GetCurrent(StatType.Stamina), _player.Stats.GetCurrentMax(StatType.Stamina));
    }

    public bool TryUseStamina(float cost)
    {
        if (_player.Stats.GetCurrent(StatType.Stamina) >= cost)
        {
            SetStamina(_player.Stats.GetCurrent(StatType.Stamina) - cost);
            _player.OnActionPerformed();
            return true;
        }
        return false;
    }

    public bool HasStamina(float cost) { return _player.Stats.GetCurrent(StatType.Stamina) >= cost; }

    public void SetMaxStamina(float value)
    {
        _player.Stats.SetCurrentMax(StatType.Stamina, Mathf.Clamp(value, 1, _player.Stats.GetTotalMax(StatType.Stamina)));
        OnStaminaChanged?.Invoke(_player.Stats.GetCurrent(StatType.Stamina), _player.Stats.GetCurrentMax(StatType.Stamina));
    }

    public void AddCorruption(float amount)
    {
        AddResourceProgress(StatType.Corruption, amount, OnCorruptionChanged);
    }

    public void AddVesselCharge(float damageDealt, float playerMaxHealth)
    {
        if (damageDealt <= 0f)
            return;

        // Vessel pauses while Health S is filling so the two meters read as one exchange.
        if (_healthSFillTimer > 0f)
            return;

        _consecutiveHitCount++;
        float consecutiveMultiplier = _consecutiveHitCount * 1.3f;
        float vesselGain = 1f * consecutiveMultiplier;
        float currentVessel = _player.Stats.GetCurrent(StatType.Vessel);

        float newVessel = Mathf.Min(100f, currentVessel + vesselGain);
        _player.Stats.SetCurrent(StatType.Vessel, newVessel);

        if (!Mathf.IsEqualApprox(currentVessel, newVessel))
            OnVesselChanged?.Invoke(newVessel, _player.Stats.GetCurrentMax(StatType.Vessel));

        if (newVessel >= 100f)
        {
            _player.Stats.SetCurrent(StatType.Vessel, 0f);
            OnVesselChanged?.Invoke(0f, _player.Stats.GetCurrentMax(StatType.Vessel));
            StartHealthSFill();
        }
    }

    public void ResetConsecutiveHits()
    {
        _consecutiveHitCount = 0;
    }

    public void StartHealing()
    {
        _isHealing = true;
        _healConsumptionTimer = HealConsumptionDuration;
        _healStartHealthS = _player.Stats.GetCurrent(StatType.HealthS);
        _healStartVessel = _player.Stats.GetCurrent(StatType.Vessel);
        _healLastProgress = 0f;
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

        if (_healStartHealthS <= 0f || _healStartVessel <= 0f)
        {
            _healConsumptionTimer = 0f;
            return;
        }

        _healConsumptionTimer -= delta;

        float progress = Mathf.Clamp(1f - (_healConsumptionTimer / HealConsumptionDuration), 0f, 1f);

        float newHealthS = Mathf.Lerp(_healStartHealthS, 0f, progress);
        float newVessel = Mathf.Lerp(_healStartVessel, 0f, progress);

        _player.Stats.SetCurrent(StatType.HealthS, newHealthS);
        _player.Stats.SetCurrent(StatType.Vessel, newVessel);

        OnHealthSChanged?.Invoke(newHealthS, _player.Stats.GetCurrentMax(StatType.HealthS));
        OnVesselChanged?.Invoke(newVessel, _player.Stats.GetCurrentMax(StatType.Vessel));

        // Total heal over the full consumption equals the Health S that was drained, frame-rate independent.
        float healAmount = _healStartHealthS * (progress - _healLastProgress);
        _healLastProgress = progress;

        float currentHealth = _player.Stats.GetCurrent(StatType.Health);
        float newHealth = Mathf.Min(currentHealth + healAmount, _player.Stats.GetCurrentMax(StatType.Health));
        _player.Stats.SetCurrent(StatType.Health, newHealth);

        if (!Mathf.IsEqualApprox(currentHealth, newHealth))
            OnHealthChanged?.Invoke(newHealth, _player.Stats.GetCurrentMax(StatType.Health));
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

    private void SetResourceValue(StatType type, float value, Action<float, float> changedEvent)
    {
        float oldValue = _player.Stats.GetCurrent(type);
        _player.Stats.SetCurrent(type, value);
        if (!Mathf.IsEqualApprox(oldValue, _player.Stats.GetCurrent(type)))
            changedEvent?.Invoke(_player.Stats.GetCurrent(type), _player.Stats.GetCurrentMax(type));
    }

    private float AddResourceProgress(StatType type, float amount, Action<float, float> changedEvent)
    {
        if (amount <= 0f)
            return 0f;

        float current = _player.Stats.GetCurrent(type);
        float max = Mathf.Max(0f, _player.Stats.GetCurrentMax(type));
        float newValue = current + amount;
        float overflow = Mathf.Max(0f, newValue - max);
        _player.Stats.SetCurrent(type, newValue);

        float appliedValue = _player.Stats.GetCurrent(type);
        if (!Mathf.IsEqualApprox(current, appliedValue))
            changedEvent?.Invoke(appliedValue, max);

        return overflow;
    }

    private void StartHealthSFill()
    {
        if (_healthSFillTimer > 0f)
            return;

        _healthSFillTimer = HealthSFillDuration;
        _player.Stats.SetCurrent(StatType.HealthS, 0f);
        OnHealthSChanged?.Invoke(0f, _player.Stats.GetCurrentMax(StatType.HealthS));
    }

    private void UpdateSpecialCooldown(float delta)
    {
        if (_healthSFillTimer > 0f)
        {
            _healthSFillTimer = Mathf.Max(0f, _healthSFillTimer - delta);

            float healthSProgress = 1f - (_healthSFillTimer / HealthSFillDuration);
            float healthSValue = Mathf.Clamp(healthSProgress * 100f, 0f, 100f);
            _player.Stats.SetCurrent(StatType.HealthS, healthSValue);
            OnHealthSChanged?.Invoke(healthSValue, _player.Stats.GetCurrentMax(StatType.HealthS));

            if (_healthSFillTimer <= 0f)
            {
                _healthSFillTimer = 0f;
                _player.Stats.SetCurrent(StatType.HealthS, 100f);
                OnHealthSChanged?.Invoke(100f, _player.Stats.GetCurrentMax(StatType.HealthS));
            }
        }

        if (_specialCooldownTimer > 0f)
        {
            _specialCooldownTimer = Mathf.Max(0f, _specialCooldownTimer - delta);
            SyncSpecialCooldownBar();
            return;
        }

        if (!Mathf.IsEqualApprox(_player.Stats.GetCurrent(StatType.StaminaS), 100f))
            SyncSpecialCooldownBar();
    }

    private void SyncSpecialCooldownBar()
    {
        float progress = GetSpecialAttackProgress();
        _player.Stats.SetCurrent(StatType.StaminaS, progress);
        OnStaminaSChanged?.Invoke(_player.Stats.GetCurrent(StatType.StaminaS), _player.Stats.GetCurrentMax(StatType.StaminaS));
    }
}
