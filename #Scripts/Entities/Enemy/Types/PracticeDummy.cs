using Godot;
using System;

public partial class PracticeDummy : Enemy
{
    public PracticeDummy()
    {
        canBeKnockedBack = false;
        armor = 100f;
        health = 1000f;
        maxHealth = 1000f;
        speed = 0f;
        damage = 0f;
        cameraPriority = 0.85f;
        tenacity = 6f; maxTenacity = 6f;
        maxStaggers = 5;
        damageType = DamageType.Corrupted;
        weaknessType = WeaknessType.Piercing;
    }
}
