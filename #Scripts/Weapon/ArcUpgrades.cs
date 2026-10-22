using Godot;

namespace Anomaly;

// Soul Arc levels, bought from Maldor for each arc on its own. For now every arc grows the same way (damage, special
// cooldown, Tenacity); each arc is meant to get its own mechanical upgrades later. Levels start at 1.
public static class ArcUpgrades
{
    public const int MAX_LEVEL = 5;
    public const float DAMAGE_PER_LEVEL = 0.04f;
    public const float COOLDOWN_CUT_PER_LEVEL = 0.06f;
    public const float TENACITY_PER_LEVEL = 0.05f;

    public static int LevelOf(string arcId) =>
        SaveManager.Progression.ArcLevels.TryGetValue(arcId, out int level) ? Mathf.Clamp(level, 1, MAX_LEVEL) : 1;

    public static void Raise(string arcId) => SaveManager.Progression.ArcLevels[arcId] = Mathf.Min(MAX_LEVEL, LevelOf(arcId) + 1);

    // Souls to go from this level to the next.
    public static int Cost(int level) => 150 + 120 * (level - 1);

    public static float DamageMultiplier(int level) => 1f + (level - 1) * DAMAGE_PER_LEVEL;

    public static float Cooldown(SoulWeaponArc arc, int level) => arc.SpecialCooldown * (1f - (level - 1) * COOLDOWN_CUT_PER_LEVEL);

    public static float TenacityMultiplier(int level) => 1f + (level - 1) * TENACITY_PER_LEVEL;
}
