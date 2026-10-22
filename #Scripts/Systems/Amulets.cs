using System;
using System.Collections.Generic;
using Godot;

namespace Anomaly;

public enum AmuletStat
{
    Healing,
    StaminaRegen,
    Damage,
    DamageTaken,
    MoveSpeed,
}

public sealed record AmuletInfo(string Id, string Name, string Line, Color Color, (AmuletStat Stat, float Amount)[] Stats);

// Amulets sit in the four slots beside the status screen. Each bends one or two of Kio's numbers by a share of their
// base; bonuses of the same kind add before they apply. The five here are debug amulets for trying the slots out, and
// every debug build is handed all of them.
public static class Amulets
{
    public const int SLOTS = 4;
    private const float MIN_MULTIPLIER = 0.1f;

    public static readonly AmuletInfo[] All =
    {
        new("mending", "Mending Charm", "The Vessel pours deeper", new Color(0.55f, 0.82f, 0.52f), new[] { (AmuletStat.Healing, 0.4f) }),
        new("second_wind", "Second Wind", "Breath returns quicker", new Color(1f, 0.62f, 0.1f), new[] { (AmuletStat.StaminaRegen, 0.5f) }),
        new("fury", "Fury Fang", "Every strike bites harder", new Color(0.95f, 0.25f, 0.25f), new[] { (AmuletStat.Damage, 0.2f) }),
        new("stoneheart", "Stoneheart", "Blows land softer, feet grow heavy", new Color(0.62f, 0.66f, 0.78f), new[] { (AmuletStat.DamageTaken, -0.2f), (AmuletStat.MoveSpeed, -0.05f) }),
        new("quickstep", "Quickstep", "Light on the feet", new Color(0.3f, 0.8f, 1f), new[] { (AmuletStat.MoveSpeed, 0.12f), (AmuletStat.StaminaRegen, 0.1f) }),
    };

    private static readonly AmuletStat[] Stats = (AmuletStat[])Enum.GetValues(typeof(AmuletStat));

    // Movement and regen read these every frame, so the worn multipliers are kept until the slots change or a new
    // save is loaded.
    private static ProgressionData cachedFor;
    private static float[] cached;

    public static AmuletInfo Get(string id) => string.IsNullOrEmpty(id) ? null : Array.Find(All, amulet => amulet.Id == id);

    public static IEnumerable<AmuletInfo> Owned
    {
        get
        {
            foreach (AmuletInfo amulet in All)
                if (SaveManager.Progression.AmuletInventory.Contains(amulet.Id))
                    yield return amulet;
        }
    }

    // One id per slot, "" where the slot is empty.
    public static string[] Equipped
    {
        get
        {
            Godot.Collections.Array<string> worn = SaveManager.Progression.EquippedAmulets;
            var slots = new string[SLOTS];
            for (int i = 0; i < SLOTS; i++)
                slots[i] = i < worn.Count ? worn[i] : string.Empty;
            return slots;
        }
    }

    public static AmuletInfo InSlot(int slot) => Get(Equipped[slot]);

    public static void GrantDebug()
    {
        if (!OS.IsDebugBuild())
            return;
        foreach (AmuletInfo amulet in All)
            if (!SaveManager.Progression.AmuletInventory.Contains(amulet.Id))
                SaveManager.Progression.AmuletInventory.Add(amulet.Id);
    }

    // The slots as they would be with this amulet in this slot. One already worn elsewhere moves over, and whatever
    // this slot held takes its old place. An empty id takes the amulet off.
    public static string[] Swapped(int slot, string id)
    {
        string[] slots = Equipped;
        int other = string.IsNullOrEmpty(id) ? -1 : Array.IndexOf(slots, id);
        if (other >= 0 && other != slot)
            slots[other] = slots[slot];
        slots[slot] = id ?? string.Empty;
        return slots;
    }

    public static void Equip(int slot, string id)
    {
        string[] slots = Swapped(slot, id);
        Godot.Collections.Array<string> worn = SaveManager.Progression.EquippedAmulets;
        worn.Clear();
        foreach (string slotId in slots)
            worn.Add(slotId);
        cachedFor = null;
    }

    public static float Bonus(AmuletStat stat, IEnumerable<string> ids)
    {
        float bonus = 0f;
        foreach (string id in ids)
        {
            AmuletInfo amulet = Get(id);
            if (amulet == null)
                continue;
            foreach ((AmuletStat kind, float amount) in amulet.Stats)
                if (kind == stat)
                    bonus += amount;
        }
        return bonus;
    }

    public static float Bonus(AmuletStat stat, AmuletInfo amulet) => amulet == null ? 0f : Bonus(stat, new[] { amulet.Id });

    public static float Multiplier(AmuletStat stat, IEnumerable<string> ids) => Mathf.Max(MIN_MULTIPLIER, 1f + Bonus(stat, ids));

    // What the amulets worn right now make of the stat.
    public static float Multiplier(AmuletStat stat)
    {
        if (cachedFor != SaveManager.Progression || cached == null)
        {
            cachedFor = SaveManager.Progression;
            string[] slots = Equipped;
            cached = new float[Stats.Length];
            foreach (AmuletStat kind in Stats)
                cached[(int)kind] = Multiplier(kind, slots);
        }
        return cached[(int)stat];
    }

    public static string Label(AmuletStat stat) => stat switch
    {
        AmuletStat.Healing => "Healing",
        AmuletStat.StaminaRegen => "Regen",
        AmuletStat.Damage => "Damage",
        AmuletStat.DamageTaken => "Dmg taken",
        _ => "Speed",
    };

    // Less damage taken is the good direction; every other stat wants to grow.
    public static bool IsGood(AmuletStat stat, float change) => stat == AmuletStat.DamageTaken ? change < 0f : change > 0f;

    public static string Percent(float amount) => StatTable.Gain(amount * 100f, "0", "%");

    // The stat before any amulet, read off Kio as he stands.
    public static float Base(Player player, AmuletStat stat) => stat switch
    {
        AmuletStat.Healing => SaveManager.PlayerStats.VesselPotency(player.BasePotency),
        AmuletStat.StaminaRegen => LevelingSystem.StaminaRegen(SaveManager.PlayerStats, player.BaseStaminaRegen),
        AmuletStat.Damage => DamageScaling.StrikeDamage(player.CurrentArc.ArcId) * player.CurrentArc.BaseEfficiency,
        AmuletStat.DamageTaken => 100f,
        _ => player.MoveSpeed,
    };

    public static float Effective(Player player, AmuletStat stat, IEnumerable<string> ids) => Base(player, stat) * Multiplier(stat, ids);

    public static string Format(AmuletStat stat, float value) => stat switch
    {
        AmuletStat.StaminaRegen => $"{value:0}/s",
        AmuletStat.DamageTaken => $"{value:0}%",
        _ => value.ToString("0"),
    };
}
