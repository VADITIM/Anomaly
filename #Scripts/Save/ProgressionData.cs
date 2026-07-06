using Godot;

[GlobalClass]
public partial class ProgressionData : Resource
{
    [Export] public int Version { get; set; } = 1;
    [Export] public Godot.Collections.Array<string> UnlockedArcs { get; set; } = new();
    [Export] public string EquippedArc { get; set; } = string.Empty;
    [Export] public Godot.Collections.Array<string> AmuletInventory { get; set; } = new();
    [Export] public Godot.Collections.Array<string> EquippedAmulets { get; set; } = new();
    [Export] public int CurrentAct { get; set; } = 1;
    [Export] public Godot.Collections.Array<string> NarrativeFlags { get; set; } = new();
}
