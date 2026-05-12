using Godot;

public partial class SpearArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 20f;
            weapon.AttackSpeed = 1.0f;
            weapon.TenacityDamage = 10f;
            weapon.StaminaCost = 2f;
            weapon.SpecialHitInterval = 4;
            weapon.AttackDuration = 0.5f;
            weapon.HeavyAttackDuration = 0.5f;
        }
    }
}
