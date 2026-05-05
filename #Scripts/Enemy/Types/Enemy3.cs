using Godot;
using System;

public partial class Enemy3 : Enemy
{
    public Enemy3()
    {
        armor = 420;
        health = 200f;
        speed = 150f;
        damage = 15f;
        cameraPriority = 0.9f;
        tenacity = 0f; maxTenacity = 0f;
        maxStaggers = 10;
        damageType = DamageType.Corrupted;
        weaknessType = WeaknessType.Piercing;
    }
}
