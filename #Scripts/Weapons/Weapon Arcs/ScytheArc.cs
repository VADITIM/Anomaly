using Godot;

public partial class ScytheArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        if (weapon != null)
        {
            weapon.Damage = 5f;
            weapon.TenacityDamage = 100f;
            weapon.StaminaCost = 2f;
            Knockback = 8f;
            SpecialHitInterval = 4;
            HeavyAttackDuration = 0.5f;
            attackDurations = new float[4] { 0.2f, 0.2f, 0.2f, 0.5f };
            
            DamageMultiplier = 2f;
            TenacityMultiplier = 1f;
            PenetrationMultiplier = 144f;
        }
    }
}
