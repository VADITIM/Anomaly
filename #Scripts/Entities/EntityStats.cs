using Godot;

namespace Anomaly;

[GlobalClass]
public partial class EntityStats : Resource
{
    [Export] public float MaxHealth = 100f;
    [Export] public float Weight = 1f;
    [Export] public bool UseKnockback = true;
    [Export] public bool UseTenacity = true;
    [Export] public float Tenacity = 100f;
}
