using Godot;

public partial class SpearArc : WeaponArc
{
    // NOTE: multipliers are placeholder balance until the Arc data moves into
    // SoulWeaponArc Resources (pitfall P6) and gets a real tuning pass.
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        AttackType = WeaponArc.WeaponAttackType.Piercing;
        if (weapon != null)
        {
            Knockback = 6f;
            SpecialHitInterval = 4;
            HeavyAttackDuration = 0.5f;
            SpecialCooldownDuration = 0.5f;
            AttackDurations = new float[4] { 1.2f, 0.2f, 0.2f, 0.5f };

            DamageMultiplier = 1.2f;
            TenacityMultiplier = 0.2f;
            PenetrationMultiplier = 1f;
            StaminaCostMultiplier = 1f;
        }
    }
}
