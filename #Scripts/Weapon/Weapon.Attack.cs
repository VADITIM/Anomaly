using Godot;

public partial class Weapon
{
    private float GetWeaknessMultiplier(Enemy enemy)
    {
        if (_currentArc == null || enemy == null)
            return 1f;

        if (enemy.IsWeakTo(_currentArc.AttackType))
            return 1.3f;

        // Baseline efficiency: any Arc stays viable at 90-100% (design.md §1.2).
        return GD.Randf() * 0.1f + 0.9f;
    }

    public float ApplyDamage(Enemy enemy)
    {
        float rawDamage = Damage * (_currentArc?.DamageMultiplier ?? 1f);

        if (OwnerStateMachine?.IsHeavyAttacking ?? false)
        {
            float heavyCharge = (OwnerStateMachine as PlayerStateMachine)?.HeavyChargeProgress ?? 0f;
            rawDamage *= 1f + (2f * heavyCharge);
        }
        else
        {
            rawDamage *= GetCurrentAttackDamageMultiplier();
        }

        float weaknessMultiplier = GetWeaknessMultiplier(enemy);
        rawDamage *= weaknessMultiplier;

        float penetrationPercent = Penetration / 100f;
        float effectiveArmor = enemy.Armor * (1f - penetrationPercent);
        float damageReductionPercent = effectiveArmor / 200f;
        float damageMultiplier = 1f - damageReductionPercent;
        float calculatedDamage = rawDamage * damageMultiplier;

        return Mathf.Max(calculatedDamage, 0);
    }

    public float CalculateTenacityDamage(float baseTenacityDamage)
    {
        _hitCount++;
        bool isSpecialHit = (_hitCount % _specialHitInterval) == 0;

        float tenacityDamageValue = baseTenacityDamage * _currentTenacityDamageMultiplier / 10f;

        if (isSpecialHit)
            tenacityDamageValue *= 1.2f;

        _currentTenacityDamageMultiplier -= 0.003f;
        _currentTenacityDamageMultiplier = Mathf.Max(_currentTenacityDamageMultiplier, 0.1f);

        return tenacityDamageValue;
    }

    public void ResetTenacityDamage()
    {
        _currentTenacityDamageMultiplier = 1f;
        _hitCount = 0;
    }

    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        if (_currentArc != null)
        {
            if (isHeavy)
                return _currentArc.HeavyAttackDuration;

            return _currentArc.GetAttackSequenceDuration(_attackSequenceIndex);
        }

        return GetCurrentAttackSequenceDuration(isHeavy);
    }


    private float GetCurrentAttackDamageMultiplier()
    {
        if (_attackDamageMultipliers == null || _attackDamageMultipliers.Length == 0)
            return 1f;

        int clampedIndex = Mathf.Clamp(_attackSequenceIndex, 0, _attackDamageMultipliers.Length - 1);
        return _attackDamageMultipliers[clampedIndex];
    }
}
