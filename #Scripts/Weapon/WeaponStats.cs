using Godot;
using System;
using System.Collections.Generic;

public class WeaponStats
{
    public class Stat
    {
        public float Current { get; set; }
        public float Max { get; set; }
        public int UpgradeLevels { get; set; } = 0;
    }

    private readonly Dictionary<WeaponStatType, Stat> _stats = new();
    private readonly Dictionary<WeaponStatType, Stat> _baselines = new();

    // Legacy save files used display-style keys before WeaponStatType existed.
    private static readonly Dictionary<string, WeaponStatType> LegacyKeyMap = new()
    {
        ["Stamina Cost"] = WeaponStatType.StaminaCost,
        ["Stamina Restore"] = WeaponStatType.StaminaRestore
    };

    // Defaults are the Scythe baseline — Arc flavor applies through WeaponArc
    // multipliers at point of use and is never written back here (design.md §3.10).
    public WeaponStats()
    {
        Define(WeaponStatType.Damage, current: 5f, max: 150f);
        Define(WeaponStatType.StaminaCost, current: 4f, max: float.MaxValue);
        Define(WeaponStatType.HeavyStaminaCost, current: 10f, max: float.MaxValue);
        Define(WeaponStatType.TenacityDamage, current: 10f, max: 10f);
        Define(WeaponStatType.StaminaRestore, current: 10f, max: 10f);
        Define(WeaponStatType.Penetration, current: 50f, max: 200f);
    }

    // Baselines are the tuning source of truth. Saves persist only UpgradeLevels
    // and replay them onto these, so editing a baseline here retunes existing
    // save files instead of being overwritten by them.
    private void Define(WeaponStatType type, float current, float max)
    {
        _stats[type] = new Stat { Current = current, Max = max };
        _baselines[type] = new Stat { Current = current, Max = max };
    }

    public Stat GetStat(WeaponStatType type)
    {
        return _stats.TryGetValue(type, out var stat) ? stat : null;
    }

    public float GetCurrent(WeaponStatType type)
    {
        var s = GetStat(type);
        return s?.Current ?? 0f;
    }

    public void SetCurrent(WeaponStatType type, float value)
    {
        var s = GetStat(type);
        if (s != null)
        {
            s.Current = Mathf.Clamp(value, 0f, s.Max);
        }
    }

    public float GetMax(WeaponStatType type)
    {
        var s = GetStat(type);
        return s?.Max ?? 0f;
    }

    public void SetMax(WeaponStatType type, float value)
    {
        var s = GetStat(type);
        if (s != null)
        {
            s.Max = Mathf.Max(0f, value);
            if (s.Current > s.Max)
                s.Current = s.Max;
        }
    }

    public int GetUpgradeLevels(WeaponStatType type)
    {
        var s = GetStat(type);
        return s?.UpgradeLevels ?? 0;
    }

    public void IncreaseStat(WeaponStatType type, float amount = 1f)
    {
        var s = GetStat(type);
        if (s != null)
        {
            s.Max += amount;
            s.UpgradeLevels++;
            s.Current = s.Max;
        }
    }

    public void DecreaseStat(WeaponStatType type, float amount = 1f)
    {
        var s = GetStat(type);
        if (s != null && s.UpgradeLevels > 0)
        {
            s.Max = Mathf.Max(0f, s.Max - amount);
            s.UpgradeLevels--;
            if (s.Current > s.Max)
                s.Current = s.Max;
        }
    }

    // Replays IncreaseStat() `levels` times against the baseline, matching it
    // exactly: each level raises Max by 1 and refills Current to the new Max.
    private void ApplyUpgradeLevels(WeaponStatType type, Stat stat, int levels)
    {
        var baseline = _baselines[type];
        levels = Math.Max(0, levels);

        stat.UpgradeLevels = levels;
        stat.Max = baseline.Max + levels;
        stat.Current = levels > 0 ? stat.Max : baseline.Current;
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var outDict = new Godot.Collections.Dictionary();
        foreach (var kv in _stats)
        {
            var statDict = new Godot.Collections.Dictionary();
            statDict["UpgradeLevels"] = kv.Value.UpgradeLevels;
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
            if (!Enum.TryParse(key, out WeaponStatType type) && !LegacyKeyMap.TryGetValue(key, out type)) continue;
            if (!_stats.TryGetValue(type, out var s)) continue;
            if (!data.TryGetValue(key, out var statDictVar)) continue;
            var statDict = statDictVar.AsGodotDictionary();
            if (statDict == null) continue;
            if (!statDict.TryGetValue("UpgradeLevels", out var upLvl)) continue;
            // Current/Max are derived, not restored — older saves still carry them
            // as keys and are intentionally ignored.
            ApplyUpgradeLevels(type, s, (int)upLvl);
        }
    }
}
