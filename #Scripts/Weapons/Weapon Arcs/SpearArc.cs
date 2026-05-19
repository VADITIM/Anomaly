using Godot;

public partial class SpearArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 6f;
            TenacityDamage = 2f;
            weapon.StaminaCost = 2f;
            SpecialHitInterval = 4;
            AttackDuration = 0.5f;
            HeavyAttackDuration = 0.5f;
            attackDurations = new float[4] { 1.2f, 0.2f, 0.2f, 0.5f };
        }
    }
}
