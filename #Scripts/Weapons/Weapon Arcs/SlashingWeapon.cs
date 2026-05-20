using Godot;

public partial class SlashingWeapon : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 20f;
            weapon.TenacityDamage = 10f;
            weapon.StaminaCost = 2f;
            Knockback = 10f;
            SpecialHitInterval = 4;
            HeavyAttackDuration = 0.5f;
            
            DamageMultiplier = 1f;
            TenacityMultiplier = 1f;
            PenetrationMultiplier = 1f;
        }
    }
}
