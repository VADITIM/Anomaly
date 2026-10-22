using Godot;

namespace Anomaly;

[GlobalClass]
public partial class DifficultyData : Resource
{
    [Export] public int DifficultyLevel = 1;
}
