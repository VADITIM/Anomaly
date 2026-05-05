public partial class PiercingWeapon : Weapon
{
    public PiercingWeapon()
    {
        damage = 15f;
        range = 70f;
        attackSpeed = 1.2f;
        weaponType = WeaponType.Melee;
        attackType = AttackType.Piercing;
        specialHitInterval = 3; // Every 3rd hit
    }
}
