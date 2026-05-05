using Godot;
using System;

public partial class Enemy4 : Enemy
{
    public Enemy4()
    {
        armor = 120;
        health = 150f;
        speed = 150f;
        damage = 15f;
        cameraPriority = 1.25f;
        tenacity = 3f; maxTenacity = 3f;
        maxStaggers = 5;
        damageType = DamageType.Corrupted;
        weaknessType = WeaknessType.Slashing;
    }
}
