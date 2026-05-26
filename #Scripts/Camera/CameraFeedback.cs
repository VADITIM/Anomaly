using Godot;
using System;

public static class CameraFeedback
{
    public struct ShakeParams
    {
        public float intensity;
        public float duration;
    }

    // Base shake function - call this from UpdateShakeOffset
    public static Vector2 ApplyShake(double delta, ref float shakeIntensity, ref float shakeTimer)
    {
        if (shakeTimer <= 0)
        {
            shakeIntensity = 0f;
            return Vector2.Zero;
        }

        shakeTimer -= (float)delta;

        Vector2 offset = new Vector2(
            (float)GD.Randf() * shakeIntensity - shakeIntensity / 2,
            (float)GD.Randf() * shakeIntensity - shakeIntensity / 2
        );

        if (shakeTimer <= 0)
        {
            shakeIntensity = 0f;
            return Vector2.Zero;
        }

        return offset;
    }

    // Initialize a shake with custom parameters
    public static void InitiateShake(ref float shakeIntensity, ref float shakeTimer, ShakeParams param)
    {
        shakeIntensity = param.intensity;
        shakeTimer = param.duration;
    }

    // Specific shake types
    public static ShakeParams NormalShake() => new ShakeParams { intensity = 2.2f, duration = 0.15f };
    public static ShakeParams WeaknessShake() => new ShakeParams { intensity = 2.4f, duration = 0.15f };
    public static ShakeParams StaggerShake() => new ShakeParams { intensity = 2.6f, duration = 0.15f };
    public static ShakeParams TenacityBreakShake() => new ShakeParams { intensity = 3.3f, duration = 0.4f };

    // Helper to trigger shakes directly from camera context
    public static void TriggerNormalShake(Camera camera) => camera?.ShakeCamera(0.5f);
    public static void TriggerWeaknessShake(Camera camera) => camera?.ShakeCamera(0.75f);
    public static void TriggerStaggerShake(Camera camera) => camera?.ShakeCamera(1f);
    public static void TriggerTenacityBreakShake(Camera camera) => camera?.ShakeCamera(5f);
}
