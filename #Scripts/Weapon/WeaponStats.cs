using Godot;

namespace Anomaly;

// The Scythe's own level, raised by Maldor. Each level adds a little flat damage; the real ceiling on how hard Kio hits
// lives in DamageScaling, which folds every source of growth together.
[GlobalClass]
public partial class WeaponStats : Resource
{
    public const float BASE_DAMAGE = 8f;
    public const int MAX_LEVEL = 25;
    public const float DAMAGE_PER_LEVEL = 0.3f;
    public const float BASE_CRITICAL_CHANCE = 0.05f;
    public const float CRITICAL_CHANCE_PER_UPGRADE = 0.02f;
    public const float BASE_CRITICAL_MULTIPLIER = 1.5f;
    public const float CRITICAL_MULTIPLIER_PER_UPGRADE = 0.1f;

    [Export] public int Level = 1;
    // Hone count from before weapon levels. Folded into Level on load and then left at 0.
    [Export] public int DamageUpgrades = 0;
    [Export] public int CriticalChanceUpgrades = 0;
    [Export] public int CriticalMultiplierUpgrades = 0;

    public bool IsMaxLevel => Level >= MAX_LEVEL;
    public float WeaponDamage => DamageAt(Level);
    public int NextLevelCost => LevelCost(Level);
    public float CriticalChance => BASE_CRITICAL_CHANCE + CriticalChanceUpgrades * CRITICAL_CHANCE_PER_UPGRADE;
    public float CriticalMultiplier => BASE_CRITICAL_MULTIPLIER + CriticalMultiplierUpgrades * CRITICAL_MULTIPLIER_PER_UPGRADE;

    public static float DamageAt(int level) => BASE_DAMAGE + (Mathf.Clamp(level, 1, MAX_LEVEL) - 1) * DAMAGE_PER_LEVEL;

    // Souls to go from this level to the next.
    public static int LevelCost(int level) => Mathf.RoundToInt(60f + 24f * Mathf.Pow(level - 1, 1.35f));

    public void MigrateLegacy()
    {
        if (DamageUpgrades <= 0)
            return;
        Level = Mathf.Min(MAX_LEVEL, Level + DamageUpgrades);
        DamageUpgrades = 0;
    }
}
