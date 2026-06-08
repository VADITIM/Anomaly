using Godot;

public partial class SpearArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        AttackType = WeaponArc.WeaponAttackType.Piercing;
        if (weapon != null)
        {
            weapon.Damage = 6f;
            weapon.TenacityDamage = 2f;
            weapon.StaminaCost = 2f;
            Knockback = 6f;
            SpecialHitInterval = 4;
            HeavyAttackDuration = 0.5f;
            attackDurations = new float[4] { 1.2f, 0.2f, 0.2f, 0.5f };
            
            DamageMultiplier = 1f;
            TenacityMultiplier = 1f;
            PenetrationMultiplier = 1f;
        }
    }
}
