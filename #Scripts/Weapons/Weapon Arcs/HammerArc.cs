using Godot;

public partial class HammerArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 440f;
            weapon.StaminaCost = 3f;
            Knockback = 15f;
            SpecialHitInterval = 5;
            HeavyAttackDuration = 0.8f;
            
            DamageMultiplier = 1f;
            TenacityMultiplier = 1f;
            PenetrationMultiplier = 1f;
        }
    }
}
