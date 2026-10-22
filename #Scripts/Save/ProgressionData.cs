using Godot;

namespace Anomaly;

[GlobalClass]
public partial class ProgressionData : Resource
{
    [Export] public Godot.Collections.Array<string> UnlockedArcs = new();
    [Export] public string EquippedArc = string.Empty;
    [Export] public Godot.Collections.Dictionary<string, int> ArcLevels = new();
    [Export] public Godot.Collections.Array<string> AmuletInventory = new();
    [Export] public Godot.Collections.Array<string> EquippedAmulets = new();
    [Export] public int CurrentAct = 1;
    [Export] public Godot.Collections.Array<string> NarrativeFlags = new();
    [Export] public int HeldSouls = 0;
}
