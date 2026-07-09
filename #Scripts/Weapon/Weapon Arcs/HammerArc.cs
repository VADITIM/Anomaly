using Godot;

public partial class HammerArc : WeaponArc
{
    // NOTE: multipliers are placeholder balance until the Arc data moves into
    // SoulWeaponArc Resources (pitfall P6) and gets a real tuning pass.
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        AttackType = WeaponArc.WeaponAttackType.Smashing;
        if (weapon != null)
        {
            Knockback = 15f;
            SpecialHitInterval = 5;
            HeavyAttackDuration = 0.8f;
            SpecialCooldownDuration = 0.8f;

            DamageMultiplier = 2f;
            TenacityMultiplier = 1.5f;
            PenetrationMultiplier = 1f;
            StaminaCostMultiplier = 1.5f;
        }
    }
}
