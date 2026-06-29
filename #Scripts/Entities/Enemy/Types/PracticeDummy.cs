using Godot;
using System;

public partial class PracticeDummy : Enemy
{
    public PracticeDummy()
    {
        CanBeKnockedBack = false;
        Armor            = 0f;
        Health           = 1000f;
        MaxHealth        = 1000f;
        Speed            = 0f;
        Damage           = 0f;
        CameraPriority   = 0.85f;
        Tenacity = 6f; MaxTenacity = 6f;
        MaxStaggers      = 5;
        this.DamageType   = EnemyDamageType.Corrupted;
        this.WeaknessType = EnemyWeaknessType.Slashing;
    }
}
