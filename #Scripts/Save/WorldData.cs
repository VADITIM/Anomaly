using Godot;

namespace Anomaly;

[GlobalClass]
public partial class WorldData : Resource
{
    [Export] public Godot.Collections.Array<string> KilledDisciples = new();
    [Export] public Godot.Collections.Array<string> DestroyedVoidHearts = new();
    [Export] public Godot.Collections.Array<string> ExploredRegions = new();
    [Export] public Godot.Collections.Array<string> NpcFlags = new();
    [Export] public Godot.Collections.Array<string> OpenedShortcuts = new();
}
