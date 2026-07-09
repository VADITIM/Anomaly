using Godot;

[GlobalClass]
public partial class EntityStats : Resource
{
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float Weight { get; set; } = 1f;
    [Export] public bool UseKnockback { get; set; } = true;
    [Export] public bool UseTenacity { get; set; } = true;
    // Tenacity is authored on the runtime meter scale (~1-15): weapons land
    // ~0.2-1.2 tenacity damage per hit and TenacitySystem's stagger/knockback
    // curves assume single-digit maxima. Do NOT author design.md's 75-100 values here.
    [Export] public float Tenacity { get; set; } = 10f;
}
