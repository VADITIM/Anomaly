using Godot;
using System;

public partial class CameraFocus : Camera2D
{
    [Export] protected float focusSpeed = 2f;
    [Export] protected float offsetRadius = 30;
    [Export] protected bool showDebugSphere = true;

    protected Vector2 focusOffset = Vector2.Zero;
    protected Player Player;
    protected Enemy Enemy;
    protected ColorRect debugSphere;

    public bool IsLocked => Enemy != null;

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
        
        // Create debug sphere
        debugSphere = new ColorRect();
        debugSphere.CustomMinimumSize = new Vector2(8, 8);
        debugSphere.Color = new Color(1, 1, 0, 0.7f); // Yellow with slight transparency
        debugSphere.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(debugSphere);
        debugSphere.Visible = showDebugSphere;
    }

    public override void _Process(double delta)
    {
        UpdateFocusOffset((float)delta);
        Offset = focusOffset;
        
        // Update debug sphere position
        if (debugSphere != null && Player != null)
        {
            Vector2 focusPoint = Player.GlobalPosition + focusOffset;
            debugSphere.GlobalPosition = focusPoint - new Vector2(4, 4); // Center the 8x8 sphere
            debugSphere.Visible = showDebugSphere;
        }
    }

    protected virtual void UpdateFocusOffset(float delta)
    {
        focusOffset = focusOffset.Lerp(Vector2.Zero, 1f - Mathf.Exp(-focusSpeed * delta));
        Vector2 cursorPosition = GetGlobalMousePosition();
        Enemy targetEnemy = Enemy.GetBestCameraTarget(cursorPosition);
        Enemy = targetEnemy;

        Vector2 desiredOffset = Vector2.Zero;
        if (targetEnemy != null)
        {
            Vector2 toEnemy = targetEnemy.GlobalPosition - Player.GlobalPosition;
            float distance = toEnemy.Length();

            if (distance > 0.001f)
            {
                Vector2 direction = toEnemy / distance;
                desiredOffset = direction * offsetRadius;
            }
        }

        float lerpWeight = 1f - Mathf.Exp(-focusSpeed * delta);
        focusOffset = focusOffset.Lerp(desiredOffset, lerpWeight);
    }
}
