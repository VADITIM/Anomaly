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

    private Dictionary<string, Stat> Stats = new Dictionary<string, Stat>();

    public WeaponStats()
    {
        Stats["Damage"] = new Stat { Current = 12f, Max = 150f };
        Stats["Stamina Cost"] = new Stat { Current = 2f, Max = float.MaxValue };
        Stats["TenacityDamage"] = new Stat { Current = 2f, Max = 10f };
        Stats["Stamina Restore"] = new Stat { Current = 10f, Max = 10f };
        Stats["Penetration"] = new Stat { Current = 50f, Max = 200f };
        Stats["Special Hit Interval"] = new Stat { Current = 4f, Max = float.MaxValue };
    }

    public Stat GetStat(string statName)
    {
        if (Stats.TryGetValue(statName, out var stat))
            return stat;
        return null;
    }

    public float GetCurrent(string statName)
    {
        var s = GetStat(statName);
        return s?.Current ?? 0f;
    }

    public void SetCurrent(string statName, float value)
    {
        var s = GetStat(statName);
        if (s != null)
        {
            s.Current = Mathf.Clamp(value, 0f, s.Max);
        }
    }

    public float GetMax(string statName)
    {
        var s = GetStat(statName);
        return s?.Max ?? 0f;
    }

    public void SetMax(string statName, float value)
    {
        var s = GetStat(statName);
        if (s != null)
        {
            s.Max = Mathf.Max(0f, value);
            if (s.Current > s.Max)
                s.Current = s.Max;
        }
    }

    // Backwards-compatible aliases
    public float GetCurrentMax(string statName) => GetMax(statName);
    public void SetCurrentMax(string statName, float value) => SetMax(statName, value);

    public int GetUpgradeLevels(string statName)
    {
        var s = GetStat(statName);
        return s?.UpgradeLevels ?? 0;
    }

    public void IncreaseStat(string statName, float amount = 1f)
    {
        var s = GetStat(statName);
        if (s != null)
        {
            s.Max += amount;
            s.UpgradeLevels++;
            s.Current = s.Max;
        }
    }

    public void DecreaseStat(string statName, float amount = 1f)
    {
        var s = GetStat(statName);
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
        foreach (var kv in Stats)
        {
            var s = kv.Value;
            var statDict = new Godot.Collections.Dictionary();
            statDict["Current"] = s.Current;
            statDict["Max"] = s.Max;
            statDict["UpgradeLevels"] = s.UpgradeLevels;
            outDict[kv.Key] = statDict;
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
            if (!Stats.ContainsKey(key)) continue;
            if (!data.TryGetValue(key, out var statDictVar)) continue;
            var statDict = statDictVar.AsGodotDictionary();
            if (statDict == null) continue;
            var s = Stats[key];
            if (statDict.TryGetValue("Current", out var curr)) s.Current = (float)curr;
            if (statDict.TryGetValue("Max", out var max)) s.Max = (float)max;
            if (statDict.TryGetValue("UpgradeLevels", out var upLvl)) s.UpgradeLevels = (int)upLvl;
        }
    }
}
