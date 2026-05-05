using Godot;

public partial class SlashingWeapon : Weapon
{

    
    public SlashingWeapon()
    {
        damage = 10f;
        range = 60;
        attackSpeed = 5f;
        knockback = 100f;
        penetration = 20f;
        tenacityDamage = 40f;
        weaponType = WeaponType.Melee;
        attackType = AttackType.Slashing;
        staminaCost = 2f;
        specialHitInterval = 4;
        attackDuration = .5f;
        heavyAttackDuration = 0.5f;
        hitboxDelay = 0.1f;
    }
}
