using Godot;

[GlobalClass]
public partial class DifficultyData : Resource
{
    [Export] public int Version { get; set; } = 1;
    [Export] public int DifficultyLevel { get; set; } = 1;
}
