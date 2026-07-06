using Godot;

public partial class ScytheArc : WeaponArc
{
    public override void SetParentWeapon(Weapon weapon)
    {
        base.SetParentWeapon(weapon);
        AttackType = WeaponArc.WeaponAttackType.Slashing;
        if (weapon != null)
        {
            weapon.Damage = 5f;
            weapon.TenacityDamage = 100f;
            weapon.StaminaCost = 51f;
            Knockback = 93f;
            HeavyAttackDuration = 0.5f;
            SpecialCooldownDuration = 0.5f;
            PlayerPushForce = 150f;
            AttackDurations = new float[4] { 0.2f, 0.2f, 0.2f, 0.5f };
            SpecialHitInterval = 4;
            
            DamageMultiplier = 2f;
            TenacityMultiplier = 1f;
            PenetrationMultiplier = 1f;
        }
    }
}
