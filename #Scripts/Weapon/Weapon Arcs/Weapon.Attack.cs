using Godot;

public partial class Weapon
{
    
    public void CheckWeaknessExploited(Enemy enemy)
    {
        enemy.outsideKnockbackForce = 1f;
    }

    private float GetWeaknessMultiplier(Enemy enemy)
    {
        if (currentArc == null || enemy == null)
            return 1f;

        bool isWeaknessExploited = currentArc.AttackType switch
        {
            WeaponArc.WeaponAttackType.Slashing => enemy.weaknessType == Enemy.WeaknessType.Slashing,
            WeaponArc.WeaponAttackType.Piercing => enemy.weaknessType == Enemy.WeaknessType.Piercing,
            WeaponArc.WeaponAttackType.Smashing => enemy.weaknessType == Enemy.WeaknessType.Smashing,
            _ => false
        };

        if (isWeaknessExploited)
            return 1.3f;

        return GD.Randf() * 0.1f + 0.9f; 
    }

    public bool IsEnemyHit() { return weaponHitbox != null && weaponHitbox.GetOverlappingBodies().Count > 0; }

    public float ApplyDamage(Enemy enemy)
    {
        float rawDamage = Damage;

        if (Player.Instance?.StateMachine?.IsHeavyAttacking ?? false)
        {
            float heavyMultiplier = 1f + (2f * (Player.Instance?.StateMachine?.HeavyChargeProgress ?? 0f));
            rawDamage *= heavyMultiplier;
        }
        else
        {
            rawDamage *= GetCurrentAttackDamageMultiplier();
        }

        float weaknessMultiplier = GetWeaknessMultiplier(enemy);
        rawDamage *= weaknessMultiplier;

        float penetrationPercent = Penetration / 100f;
        float effectiveArmor = enemy.armor * (1f - penetrationPercent);
        float damageReductionPercent = effectiveArmor / 200f;
        float damageMultiplier = 1f - damageReductionPercent;
        float calculatedDamage = rawDamage * damageMultiplier;

        return Mathf.Max(calculatedDamage, 0);
    }

    public float CalculateTenacityDamage(float baseTenacityDamage)
    {
        hitCount++;
        bool isSpecialHit = (hitCount % specialHitInterval) == 0;

        float tenacityDamageValue = baseTenacityDamage * currentTenacityDamageMultiplier / 10f;

        if (isSpecialHit)
            tenacityDamageValue *= 1.2f;

        currentTenacityDamageMultiplier -= 0.003f;
        currentTenacityDamageMultiplier = Mathf.Max(currentTenacityDamageMultiplier, 0.1f);

        return tenacityDamageValue;
    }

    public void ResetTenacityDamage()
    {
        currentTenacityDamageMultiplier = 1f;
        hitCount = 0;
    }

    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        if (currentArc != null)
        {
            if (isHeavy)
                return currentArc.HeavyAttackDuration;

            return currentArc.GetAttackSequenceDuration(attackSequenceIndex);
        }

        return GetCurrentAttackSequenceDuration(isHeavy);
    }


    private float GetCurrentAttackDamageMultiplier()
    {
        if (attackDamageMultipliers == null || attackDamageMultipliers.Length == 0)
            return 1f;

        int clampedIndex = Mathf.Clamp(attackSequenceIndex, 0, attackDamageMultipliers.Length - 1);
        return attackDamageMultipliers[clampedIndex];
    }
}