using Godot;
using System;

public partial class Enemy5 : Enemy
{
    public Enemy5()
    {
        armor = 10;
        health = 100f;
        speed = 150f;
        damage = 15f;
        cameraPriority = 0.85f;
        tenacity = 6f; maxTenacity = 6f;
        maxStaggers = 0;
        damageType = DamageType.Corrupted;
        weaknessType = WeaknessType.Piercing;
    }
}
