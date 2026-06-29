using Godot;
using System;

public partial class Bee : Enemy
{
    public Bee()
    {
        Armor        = 30;
        Health       = 150f;
        Speed        = 150f;
        Damage       = 15f;
        AttackRange  = 10f;
        CameraPriority = 1.25f;
        Tenacity = 3f; MaxTenacity = 3f;
        MaxStaggers  = 5;
        AttackDuration = 1.2f;
        this.DamageType   = EnemyDamageType.Corrupted;
        this.WeaknessType = EnemyWeaknessType.Slashing;
    }
}
