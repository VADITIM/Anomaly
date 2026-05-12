using Godot;

public partial class ScytheArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 20f;
            weapon.AttackSpeed = 2.5f;
            weapon.TenacityDamage = 10f;
            weapon.StaminaCost = 2f;
            weapon.SpecialHitInterval = 4;
            weapon.AttackDuration = 0.2f;
            weapon.HeavyAttackDuration = 1.5f;
        }
    }
}
