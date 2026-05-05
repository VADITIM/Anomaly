using Godot;
using System;

public partial class Camera : CameraFocus
{
    private float shakeDecay = .01f;
    private float shakeIntensity = 0f;
    private float shakeTimer = 0f;
    private Vector2 shakeOffset = Vector2.Zero;

    public override void _Process(double delta)
    {
        UpdateFocusOffset((float)delta);
        shakeOffset = CameraFeedback.TenacityBreakShake(delta, ref shakeDecay, ref shakeIntensity, ref shakeTimer);
        Offset = focusOffset + shakeOffset;
        
        // Update debug sphere position
        if (debugSphere != null && Player != null)
        {
            Vector2 focusPoint = Player.GlobalPosition + focusOffset;
            debugSphere.GlobalPosition = focusPoint - new Vector2(4, 4); // Center the 8x8 sphere
            debugSphere.Visible = showDebugSphere;
        }
    }

    public void ShakeCamera(float intensity = 0f)
    {
        CameraFeedback.ShakeCamera(ref shakeIntensity, ref shakeTimer, intensity);
    }
}
