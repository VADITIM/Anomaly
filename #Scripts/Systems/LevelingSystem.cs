using Godot;

namespace Anomaly;

// Resonance is Kio's level. Every corrupted thing he ends leaves Residue, and enough Residue raises Resonance by one,
// granting a point to attune at the Rend Anvil. The Voice calls it strength; each Resonance also quickens Corruption.
public static class LevelingSystem
{
    public const int MAX_RESONANCE = 30;
    public const float HEALTH_PER_RESONANCE = 2f;
    public const float HEALTH_PER_VITALITY = 5f;
    public const float MAX_HEALTH = 300f;
    public const float MAX_STAMINA = 300f;
    public const float STAMINA_PER_ENDURANCE = 5f;
    public const float REGEN_PER_ENDURANCE = 2f;
    public const int MAX_ASPECT = 40;
    // Rage feeds only the Aetheriac State: harder hits and deeper lifesteal while it burns, and a slower drain.
    public const float AETHERIAC_BASE_DAMAGE = 1.25f;
    public const float AETHERIAC_DAMAGE_PER_RAGE = 0.0125f;
    public const float LIFESTEAL_PER_RAGE = 0.005f;
    public const float DRAIN_CUT_PER_RAGE = 0.01f;
    public const float TENACITY_PER_RESOLVE = 0.06f;
    public const float POTENCY_PER_RESOLVE = 2f;
    public const float CORRUPTION_PER_RESONANCE = 0.03f;

    public static int ResidueToNext(int resonance) => Mathf.RoundToInt(35f * Mathf.Pow(resonance, 1.5f) + 15f);

    public static float HealthBonus(PlayerStats stats) =>
        (stats.Resonance - 1) * HEALTH_PER_RESONANCE + stats.Vitality * HEALTH_PER_VITALITY;

    public static float MaxHealth(PlayerStats stats, float baseHealth) => Mathf.Min(MAX_HEALTH, baseHealth + HealthBonus(stats));

    public static float MaxStamina(PlayerStats stats, float baseStamina) =>
        Mathf.Min(MAX_STAMINA, baseStamina + stats.Endurance * STAMINA_PER_ENDURANCE);

    // Every Aspect stops at 40 points. Vitality and Endurance also stop once their total reaches its cap, so no point
    // is spent past it.
    public static bool CanRaise(PlayerStats stats, Aspect aspect, float baseHealth, float baseStamina) => stats.GetAspect(aspect) < MAX_ASPECT && aspect switch
    {
        Aspect.Vitality => MaxHealth(stats, baseHealth) < MAX_HEALTH,
        Aspect.Endurance => MaxStamina(stats, baseStamina) < MAX_STAMINA,
        _ => true,
    };

    public static float StaminaRegen(PlayerStats stats, float baseRegen) => baseRegen + stats.Endurance * REGEN_PER_ENDURANCE;

    public static float AetheriacDamage(PlayerStats stats) => AETHERIAC_BASE_DAMAGE + stats.Might * AETHERIAC_DAMAGE_PER_RAGE;

    public static float AetheriacLifesteal(PlayerStats stats) => stats.Might * LIFESTEAL_PER_RAGE;

    public static float AetheriacDrainScale(PlayerStats stats) => 1f - stats.Might * DRAIN_CUT_PER_RAGE;

    public static float TenacityMultiplier(PlayerStats stats) => 1f + stats.Resolve * TENACITY_PER_RESOLVE;

    public static float CorruptionMultiplier(PlayerStats stats) => 1f + (stats.Resonance - 1) * CORRUPTION_PER_RESONANCE;

    public static int ScaleResidue(int baseResidue, int enemyLevel) => Mathf.RoundToInt(baseResidue * (1f + (enemyLevel - 1) * 0.35f));

    // Returns how many Resonances were gained.
    public static int AddResidue(PlayerStats stats, int amount)
    {
        if (stats.Resonance >= MAX_RESONANCE)
            return 0;
        stats.Residue += amount;
        int gained = 0;
        while (stats.Resonance < MAX_RESONANCE && stats.Residue >= ResidueToNext(stats.Resonance))
        {
            stats.Residue -= ResidueToNext(stats.Resonance);
            stats.Resonance++;
            stats.UnspentPoints++;
            gained++;
        }
        if (stats.Resonance >= MAX_RESONANCE)
            stats.Residue = 0;
        return gained;
    }

    public static string Describe(Aspect aspect) => aspect switch
    {
        Aspect.Vitality => $"+{HEALTH_PER_VITALITY:0} maximum health.",
        Aspect.Endurance => $"+{STAMINA_PER_ENDURANCE:0} maximum stamina and +{REGEN_PER_ENDURANCE:0} stamina per second.",
        Aspect.Might => $"+{AETHERIAC_DAMAGE_PER_RAGE * 100f:0.#}% Aetheriac damage, +{LIFESTEAL_PER_RAGE * 100f:0.#}% lifesteal and {DRAIN_CUT_PER_RAGE * 100f:0}% less drain.",
        _ => $"+{TENACITY_PER_RESOLVE * 100f:0}% Tenacity damage and +{POTENCY_PER_RESOLVE:0} Healing Mark potency.",
    };
}
