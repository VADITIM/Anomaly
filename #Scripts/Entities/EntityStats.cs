using Godot;

[GlobalClass]
public partial class EntityStats : Resource
{
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float Weight { get; set; } = 1f;
    [Export] public bool UseKnockback { get; set; } = true;
    [Export] public bool UseTenacity { get; set; } = true;
    [Export] public float Tenacity { get; set; } = 100f;
}
