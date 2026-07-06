using Godot;

// Save Resources are single-owner mutable instances written to user:// — the
// read-only rule for shared .tres assets does not apply to them (design.md §3.11).
[GlobalClass]
public partial class PlayerSaveData : Resource
{
    [Export] public int Version { get; set; } = 1;
    [Export] public SavePoint LastSavePoint { get; set; } = SavePoint.None;
    [Export] public Vector2 Position { get; set; } = Vector2.Zero;
    [Export] public Godot.Collections.Dictionary Stats { get; set; } = new();
}
