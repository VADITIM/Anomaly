using Godot;
using System;

public partial class Enemy1 : Enemy
{
    public Enemy1()
    {
        armor = 50;
        health = 100f;
        speed = 150f;
        damage = 15f;
        cameraPriority = 1f;
        tenacity = 10f; maxTenacity = 10f;
        maxStaggers = 1;
        damageType = DamageType.Corrupted;
        weaknessType = WeaknessType.Piercing;
    }
}
