using Godot;
using System;
using System.Collections.Generic;

public class PlayerStats
{
    public class Stat
    {
        public float Current { get; set; }
        public float CurrentMax { get; set; }
        public float TotalMax { get; set; }
        public int UpgradeLevels { get; set; } = 0;
    }

    private Dictionary<string, Stat> Stats = new Dictionary<string, Stat>();
    private float staminaRegenTimer = 0f;
    private const float STAMINA_REGEN_COOLDOWN = .9f;

    public PlayerStats()
    {
        Stats["Speed"] = new Stat { Current = 130f, CurrentMax = 130f, TotalMax = 150f };
        Stats["Armor"] = new Stat { Current = 20f, CurrentMax = 20f, TotalMax = 100f };
        Stats["Tenacity"] = new Stat { Current = 5f, CurrentMax = 5f, TotalMax = 20f };
        
        Stats["Health"] = new Stat { Current = 100f, CurrentMax = 200f, TotalMax = 200f };
        
        Stats["Stamina"] = new Stat { Current = 100f, CurrentMax = 100f, TotalMax = 300f };
        Stats["Stamina Regen"] = new Stat { Current = 10f, CurrentMax = 10f, TotalMax = 50f };
        
        Stats["Vessel"] = new Stat { Current = 50f, CurrentMax = 50f, TotalMax = 100f };
        
        Stats["Corruption"] = new Stat { Current = 100f, CurrentMax = 100f, TotalMax = 200f };
        
        Stats["Lacerate Multiplier"] = new Stat { Current = 1f, CurrentMax = 1f, TotalMax = 3f };
        Stats["Puncture Multiplier"] = new Stat { Current = 1f, CurrentMax = 1f, TotalMax = 3f };
        Stats["Crush Multiplier"] = new Stat { Current = 1f, CurrentMax = 1f, TotalMax = 3f };
        
        Stats["Soul"] = new Stat { Current = 0f, CurrentMax = 0f, TotalMax = 100f };
    }

    public void NotifyActionPerformed()
    {
        staminaRegenTimer = STAMINA_REGEN_COOLDOWN;
    }

    public void ProcessStaminaRegeneration(float delta)
    {
        staminaRegenTimer -= delta;

        if (staminaRegenTimer > 0f)
            return;

        float currentStamina = GetCurrent("Stamina");
        float maxStamina = GetCurrentMax("Stamina");
        float regenRate = GetCurrentMax("Stamina Regen");

        if (currentStamina < maxStamina)
        {
            float newStamina = Mathf.Min(currentStamina + regenRate * delta, maxStamina);
            SetCurrent("Stamina", newStamina);
        }
    }

    public Stat GetStat(string statName)
    {
        if (Stats.TryGetValue(statName, out var stat))
        {
            return stat;
        }
        return null;
    }

    // Current value getters/setters (for active gameplay)
    public float GetCurrent(string statName)
    {
        var stat = GetStat(statName);
        return stat?.Current ?? 0f;
    }

    public void SetCurrent(string statName, float value)
    {
        var stat = GetStat(statName);
        if (stat != null)
        {
            stat.Current = Mathf.Clamp(value, 0, stat.CurrentMax);
        }
    }

    public float GetCurrentMax(string statName)
    {
        var stat = GetStat(statName);
        return stat?.CurrentMax ?? 0f;
    }

    public void SetCurrentMax(string statName, float value)
    {
        var stat = GetStat(statName);
        if (stat != null)
        {
            stat.CurrentMax = Mathf.Clamp(value, 0, stat.TotalMax);
            // Clamp current to new max
            if (stat.Current > stat.CurrentMax)
                stat.Current = stat.CurrentMax;
        }
    }

    public float GetTotalMax(string statName)
    {
        var stat = GetStat(statName);
        return stat?.TotalMax ?? 0f;
    }

    public int GetUpgradeLevels(string statName)
    {
        var stat = GetStat(statName);
        return stat?.UpgradeLevels ?? 0;
    }

    public bool CanIncrease(string statName)
    {
        var stat = GetStat(statName);
        return stat != null && stat.CurrentMax < stat.TotalMax;
    }

    public bool CanDecrease(string statName)
    {
        var stat = GetStat(statName);
        return stat != null && stat.UpgradeLevels > 0;
    }

    public void IncreaseStat(string statName, float amount = 1f)
    {
        var stat = GetStat(statName);
        if (stat != null && stat.CurrentMax < stat.TotalMax)
        {
            stat.CurrentMax += amount;
            if (stat.CurrentMax > stat.TotalMax)
                stat.CurrentMax = stat.TotalMax;
            stat.UpgradeLevels++;
            // Increase current value too
            stat.Current = stat.CurrentMax;
        }
    }

    public void DecreaseStat(string statName, float amount = 1f)
    {
        var stat = GetStat(statName);
        if (stat != null && stat.UpgradeLevels > 0)
        {
            stat.CurrentMax -= amount;
            stat.UpgradeLevels--;
            // Clamp current to new max
            if (stat.Current > stat.CurrentMax)
                stat.Current = stat.CurrentMax;
        }
    }
}
