using Godot;

public partial class ScytheArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            // weapon.Damage = 50f;
            // weapon.Penetration = 0f;
            // weapon.TenacityDamage = 10f;

            // weapon.Knockback = 100f;

            // weapon.OutsideKnockbackForce = 1f;
            // weapon.StaminaCost = 2f;
            // weapon.SpecialHitInterval = 4;
            // weapon.AttackDuration = 0.2f;
            // weapon.HeavyAttackDuration = .5f;
        }
    }
}
