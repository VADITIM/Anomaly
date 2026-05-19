using Godot;
using System;

public partial class Bee : Enemy
{
    public Bee()
    {
        armor = 30;
        health = 150f;
        speed = 150f;
        damage = 15f;
        attackRange = 10f;
        cameraPriority = 1.25f;
        tenacity = 3f; maxTenacity = 3f;
        maxStaggers = 5;
        attackDuration = 1.2f;
        damageType = DamageType.Corrupted;
        weaknessType = WeaknessType.Slashing;
    }
}
