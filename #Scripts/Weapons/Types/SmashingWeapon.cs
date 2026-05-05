public partial class SmashingWeapon : Weapon
{
    public SmashingWeapon()
    {
        damage = 440f;
        range = 60f;
        attackSpeed = 0.8f;
        weaponType = WeaponType.Melee;
        attackType = AttackType.Smashing;
        specialHitInterval = 5; // Every 5th hit
    }
}
