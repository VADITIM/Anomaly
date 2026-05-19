using Godot;
using System;

public partial class Camera : Camera2D
{
    protected Player Player;
    protected Enemy Enemy;

    public virtual bool IsLocked => false;

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
    }

    public virtual void ShakeCamera(float intensity = 0f)
    {
    }
}
