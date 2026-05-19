using Godot;

public partial class SpearArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 20f;
            TenacityDamage = 10f;
            weapon.StaminaCost = 2f;
            SpecialHitInterval = 4;
            AttackDuration = 0.5f;
            HeavyAttackDuration = 0.5f;
        }
    }
}
