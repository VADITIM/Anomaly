using Godot;

// Enemy stat blueprint. Extends EntityStats with combat/AI/reward stats specific to
// enemies. Author one .tres per enemy type and assign it to the enemy node's
// EntityStats slot; Enemy.ApplyEnemyStats reads these fields when the assigned
// blueprint is an EnemyStats. Never mutated at runtime.
[GlobalClass]
public partial class EnemyStats : EntityStats
{
    [Export(PropertyHint.Range, "1,10")] public int CorruptionReward { get; set; } = 2;
    [Export] public float SoulReward { get; set; } = 50f;
    [Export] public EnemyDamageType DamageType { get; set; }
    [Export] public EnemyWeaknessType WeaknessType { get; set; }
    [Export] public float ChaseRange { get; set; } = 200f;
    [Export] public float AttackRange { get; set; } = 50f;
}
