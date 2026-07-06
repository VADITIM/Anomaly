using Godot;

[GlobalClass]
public partial class WorldData : Resource
{
    [Export] public int Version { get; set; } = 1;
    [Export] public Godot.Collections.Array<string> KilledDisciples { get; set; } = new();
    [Export] public Godot.Collections.Array<string> DestroyedVoidHearts { get; set; } = new();
    [Export] public Godot.Collections.Array<string> ExploredRegions { get; set; } = new();
    [Export] public Godot.Collections.Array<string> NpcFlags { get; set; } = new();
}
