using Godot;

namespace Anomaly;

// The look of one strike in a weapon's attack pattern. Reach values are fractions of the weapon's visible reach,
// so the wake always matches the blade that draws it; the equipped Arc only layers its overlay on top.
[GlobalClass]
public partial class StrikeVfx : Resource
{
    [Export] public float SlashArcDegrees = 150f;
    [Export] public float SlashReach = 1f;
    [Export] public float SlashThickness = 5f;
    [Export] public float SlashLinger = 0.08f;
    [Export] public float RingReach;
    [Export] public float RingThickness = 2f;
    [Export] public int SparkCount;
    [Export] public float StarSize;
}
