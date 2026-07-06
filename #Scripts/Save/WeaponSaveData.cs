using Godot;

[GlobalClass]
public partial class WeaponSaveData : Resource
{
    [Export] public int Version { get; set; } = 1;
    [Export] public Godot.Collections.Dictionary Stats { get; set; } = new();
}
