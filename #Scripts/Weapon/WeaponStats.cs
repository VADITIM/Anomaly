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

    // Legacy save files used display-style keys before WeaponStatType existed.
    private static readonly Dictionary<string, WeaponStatType> LegacyKeyMap = new()
    {
        ["Stamina Cost"] = WeaponStatType.StaminaCost,
        ["Stamina Restore"] = WeaponStatType.StaminaRestore,
        ["Special Hit Interval"] = WeaponStatType.SpecialHitInterval
    };

    // Defaults are the Scythe baseline — Arc flavor applies through WeaponArc
    // multipliers at point of use and is never written back here (design.md §3.10).
    public WeaponStats()
    {
        _stats[WeaponStatType.Damage] = new Stat { Current = 5f, Max = 150f };
        _stats[WeaponStatType.StaminaCost] = new Stat { Current = 51f, Max = float.MaxValue };
        _stats[WeaponStatType.TenacityDamage] = new Stat { Current = 10f, Max = 10f };
        _stats[WeaponStatType.StaminaRestore] = new Stat { Current = 10f, Max = 10f };
        _stats[WeaponStatType.Penetration] = new Stat { Current = 50f, Max = 200f };
        _stats[WeaponStatType.SpecialHitInterval] = new Stat { Current = 4f, Max = float.MaxValue };
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

    public Godot.Collections.Dictionary ToDictionary()
    {
        var outDict = new Godot.Collections.Dictionary();
        foreach (var kv in _stats)
        {
            var s = kv.Value;
            var statDict = new Godot.Collections.Dictionary();
            statDict["Current"] = s.Current;
            statDict["Max"] = s.Max;
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
            if (!Enum.TryParse(key, out WeaponStatType type) && !LegacyKeyMap.TryGetValue(key, out type)) continue;
            if (!_stats.TryGetValue(type, out var s)) continue;
            if (!data.TryGetValue(key, out var statDictVar)) continue;
            var statDict = statDictVar.AsGodotDictionary();
            if (statDict == null) continue;
            if (statDict.TryGetValue("Current", out var curr)) s.Current = (float)curr;
            if (statDict.TryGetValue("Max", out var max)) s.Max = (float)max;
            if (statDict.TryGetValue("UpgradeLevels", out var upLvl)) s.UpgradeLevels = (int)upLvl;
        }
    }
}
