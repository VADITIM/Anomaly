using Godot;

// Designer-authored stat blueprint shared by every entity type (Player fallback,
// Enemy, Prop). Author one .tres per entity and assign it to the node's EntityStats
// slot; an assigned blueprint is the stat authority (see Entity.ApplyEntityStats).
// Never mutated at runtime. Enemy-specific stats live on EnemyStats, which extends this.
[GlobalClass]
public partial class EntityStats : Resource
{
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float Speed { get; set; } = 100f;
    [Export] public float Damage { get; set; } = 10f;
    [Export] public float Armor { get; set; } = 0f;
    [Export] public float Weight { get; set; } = 1f;
    [Export] public bool UseKnockback { get; set; } = true;
    [Export] public bool UseTenacity { get; set; } = true;
    // Tenacity is authored on the runtime meter scale (~1-15): weapons land
    // ~0.2-1.2 tenacity damage per hit and TenacitySystem's stagger/knockback
    // curves assume single-digit maxima. Do NOT author design.md's 75-100 values here.
    [Export] public float Tenacity { get; set; } = 5f;
    [Export] public float MaxTenacity { get; set; } = 10f;
    [Export] public int MaxStaggers { get; set; } = 3;
    [Export] public float CameraPriority { get; set; } = 0f;
    [Export] public float AttackDuration { get; set; } = 1f;
}
