using Godot;
using System;

public partial class CameraFocus : Camera2D
{
    [Export] protected float focusSpeed = 4f;
    [Export] protected float offsetRadius = 100;
    [Export] protected bool showDebugSphere = false;

    protected Vector2 focusOffset = Vector2.Zero;
    protected Player Player;
    protected Enemy Enemy;
    protected ColorRect debugSphere;

    public bool IsLocked => Enemy != null;

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
        
        debugSphere = new ColorRect();
        debugSphere.CustomMinimumSize = new Vector2(8, 8);
        debugSphere.Color = new Color(1, 1, 0, 0.7f); 
        debugSphere.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(debugSphere);
        debugSphere.Visible = showDebugSphere;
    }

    public override void _Process(double delta)
    {
        UpdateFocusOffset((float)delta);
        Offset = focusOffset;
        
        if (debugSphere != null && Player != null)
        {
            Vector2 focusPoint = Player.GlobalPosition + focusOffset;
            debugSphere.GlobalPosition = focusPoint - new Vector2(4, 4);
            debugSphere.Visible = showDebugSphere;
        }
    }

    protected virtual void UpdateFocusOffset(float delta)
    {
        Vector2 cursorPosition = GetGlobalMousePosition();
        Enemy targetEnemy = Enemy.GetBestCameraTarget(cursorPosition);
        Enemy = targetEnemy;

        Vector2 desiredOffset = Vector2.Zero;
        if (targetEnemy != null)
        {
            Vector2 toEnemy = targetEnemy.GlobalPosition - Player.GlobalPosition;
            desiredOffset = toEnemy * 0.5f;

            if (desiredOffset.Length() > offsetRadius)
                desiredOffset = desiredOffset.Normalized() * offsetRadius;
        }

        float lerpWeight = 1f - Mathf.Exp(-focusSpeed * delta);
        focusOffset = focusOffset.Lerp(desiredOffset, lerpWeight);
    }
}
