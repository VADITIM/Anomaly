using Godot;

namespace Anomaly;

// Keeps Kio from outgrowing the world. Weapon level and the Soul Arc's level multiply into one raw growth factor
// over the bare Scythe. Up to the knee it counts in full; past it, returns fall away toward the ceiling, so a maxed
// Scythe on a maxed Arc hits about two and a half times harder than a fresh one and no more. On top of that, no
// single hit takes more than half of an enemy's health, however the crit, stagger, weakness and Aetheriac bonuses stack.
public static class DamageScaling
{
    public const float GROWTH_KNEE = 1.5f;
    public const float GROWTH_CEILING = 2.5f;
    public const float MAX_HIT_SHARE = 0.5f;

    public static float Growth(float raw)
    {
        if (raw <= GROWTH_KNEE)
            return raw;
        float room = GROWTH_CEILING - GROWTH_KNEE;
        return GROWTH_KNEE + room * (1f - Mathf.Exp(-(raw - GROWTH_KNEE) / room));
    }

    // Damage of a plain 1x strike before efficiency, crits and situational bonuses.
    public static float StrikeDamage(int weaponLevel, int arcLevel)
    {
        float raw = WeaponStats.DamageAt(weaponLevel) / WeaponStats.BASE_DAMAGE * ArcUpgrades.DamageMultiplier(arcLevel);
        return WeaponStats.BASE_DAMAGE * Growth(raw);
    }

    public static float StrikeDamage(string arcId) =>
        StrikeDamage(SaveManager.WeaponStats.Level, ArcUpgrades.LevelOf(arcId));

    public static float CapHit(float damage, IHittable target) =>
        target is Enemy enemy ? Mathf.Min(damage, Mathf.Ceil(enemy.MaxHealth * MAX_HIT_SHARE)) : damage;
}
