using Godot;

public partial class HammerArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 440f;
            weapon.AttackSpeed = 0.8f;
            weapon.SpecialHitInterval = 5;
        }
    }
}
