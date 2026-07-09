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

    private readonly Dictionary<StatType, Stat> _stats = new();

    // Legacy save files used display-style keys before StatType existed.
    private static readonly Dictionary<string, StatType> LegacyKeyMap = new()
    {
        ["Stamina Regen"] = StatType.StaminaRegen,
        ["Health S"] = StatType.HealthS,
        ["Stamina S"] = StatType.StaminaS
    };

    public PlayerStats()
    {
        _stats[StatType.Speed] = new Stat { Current = 130f, CurrentMax = 130f, TotalMax = 150f };
        _stats[StatType.Armor] = new Stat { Current = 20f, CurrentMax = 20f, TotalMax = 100f };
        _stats[StatType.Tenacity] = new Stat { Current = 5f, CurrentMax = 5f, TotalMax = 20f };

        _stats[StatType.Health] = new Stat { Current = 100f, CurrentMax = 100f, TotalMax = 200f };

        _stats[StatType.Stamina] = new Stat { Current = 300f, CurrentMax = 300f, TotalMax = 300f };
        _stats[StatType.StaminaRegen] = new Stat { Current = 50f, CurrentMax = 50f, TotalMax = 50f };

        _stats[StatType.Corruption] = new Stat { Current = 0f, CurrentMax = 100f, TotalMax = 100f };
        _stats[StatType.Vessel] = new Stat { Current = 0f, CurrentMax = 100f, TotalMax = 100f };
        _stats[StatType.HealthS] = new Stat { Current = 0f, CurrentMax = 100f, TotalMax = 100f };
        _stats[StatType.StaminaS] = new Stat { Current = 100f, CurrentMax = 100f, TotalMax = 100f };

        _stats[StatType.Soul] = new Stat { Current = 0f, CurrentMax = 0f, TotalMax = 100f };
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var outDict = new Godot.Collections.Dictionary();
        foreach (var kv in _stats)
        {
            var s = kv.Value;
            var statDict = new Godot.Collections.Dictionary();
            statDict["Current"] = s.Current;
            statDict["CurrentMax"] = s.CurrentMax;
            statDict["TotalMax"] = s.TotalMax;
            statDict["UpgradeLevels"] = s.UpgradeLevels;
            outDict[kv.Key.ToString()] = statDict;
        }
        return outDict;
    }

    public void LoadFromDictionary(Godot.Collections.Dictionary data)
    {
        if (data == null) return;
        foreach (var keyObj in data.Keys)
        {
            var key = (string)keyObj;
            if (string.IsNullOrEmpty(key)) continue;
            if (!Enum.TryParse(key, out StatType type) && !LegacyKeyMap.TryGetValue(key, out type)) continue;
            if (!_stats.TryGetValue(type, out var s)) continue;
            if (!data.TryGetValue(key, out var statDictVar)) continue;
            var statDict = statDictVar.AsGodotDictionary();
            if (statDict == null) continue;
            if (statDict.TryGetValue("Current", out var curr)) s.Current = (float)curr;
            if (statDict.TryGetValue("CurrentMax", out var currMax)) s.CurrentMax = (float)currMax;
            if (statDict.TryGetValue("TotalMax", out var totMax)) s.TotalMax = (float)totMax;
            if (statDict.TryGetValue("UpgradeLevels", out var upLvl)) s.UpgradeLevels = (int)upLvl;
        }
    }

    public Stat GetStat(StatType type)
    {
        return _stats.TryGetValue(type, out var stat) ? stat : null;
    }

    public float GetCurrent(StatType type)
    {
        var stat = GetStat(type);
        return stat?.Current ?? 0f;
    }

    public void SetCurrent(StatType type, float value)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.Current = Mathf.Clamp(value, 0, stat.CurrentMax);
        }
    }

    public float GetCurrentMax(StatType type)
    {
        var stat = GetStat(type);
        return stat?.CurrentMax ?? 0f;
    }

    public void SetCurrentMax(StatType type, float value)
    {
        var stat = GetStat(type);
        if (stat != null)
        {
            stat.CurrentMax = Mathf.Clamp(value, 0, stat.TotalMax);
            if (stat.Current > stat.CurrentMax)
                stat.Current = stat.CurrentMax;
        }
    }

    public float GetTotalMax(StatType type)
    {
        var stat = GetStat(type);
        return stat?.TotalMax ?? 0f;
    }

    public int GetUpgradeLevels(StatType type)
    {
        var stat = GetStat(type);
        return stat?.UpgradeLevels ?? 0;
    }

    public bool CanIncrease(StatType type)
    {
        var stat = GetStat(type);
        return stat != null && stat.CurrentMax < stat.TotalMax;
    }

    public bool CanDecrease(StatType type)
    {
        var stat = GetStat(type);
        return stat != null && stat.UpgradeLevels > 0;
    }

    public void IncreaseStat(StatType type, float amount = 1f)
    {
        var stat = GetStat(type);
        if (stat != null && stat.CurrentMax < stat.TotalMax)
        {
            stat.CurrentMax += amount;
            if (stat.CurrentMax > stat.TotalMax)
                stat.CurrentMax = stat.TotalMax;
            stat.UpgradeLevels++;
            stat.Current = stat.CurrentMax;
        }
    }

    public void DecreaseStat(StatType type, float amount = 1f)
    {
        var stat = GetStat(type);
        if (stat != null && stat.UpgradeLevels > 0)
        {
            stat.CurrentMax -= amount;
            stat.UpgradeLevels--;
            if (stat.Current > stat.CurrentMax)
                stat.Current = stat.CurrentMax;
        }
    }
}
