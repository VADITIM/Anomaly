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
        float rawDamage = Damage * (_currentArc?.Data?.DamageMultiplier ?? 1f);

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
        float tenacityDamageValue = baseTenacityDamage * _currentTenacityDamageMultiplier / 10f;

        if (_isSpecialHitSwing)
            tenacityDamageValue *= _currentArc?.Data?.SpecialHitTenacityMultiplier ?? 1.2f;

        _currentTenacityDamageMultiplier -= 0.003f;
        _currentTenacityDamageMultiplier = Mathf.Max(_currentTenacityDamageMultiplier, 0.1f);

        return tenacityDamageValue;
    }

    // NOTE: _hitCount is not reset here — the special-hit interval runs
    // continuously across combos and Tenacity resets by design.
    public void ResetTenacityDamage()
    {
        _currentTenacityDamageMultiplier = 1f;
    }

    public float GetLightAttackDuration(int sequenceIndex)
    {
        if (_attackDurations == null || _attackDurations.Length == 0)
            return 0.37f;

        int clampedIndex = Mathf.Clamp(sequenceIndex, 0, _attackDurations.Length - 1);
        return _attackDurations[clampedIndex];
    }

    public float GetAttackAnimationDuration(string direction, bool isHeavy)
    {
        if (isHeavy && _currentArc != null)
            return _currentArc.HeavyAttackDuration;

        return GetLightAttackDuration(_attackSequenceIndex);
    }


    private float GetCurrentAttackDamageMultiplier()
    {
        if (_attackDamageMultipliers == null || _attackDamageMultipliers.Length == 0)
            return 1f;

        int clampedIndex = Mathf.Clamp(_attackSequenceIndex, 0, _attackDamageMultipliers.Length - 1);
        return _attackDamageMultipliers[clampedIndex];
    }
}
