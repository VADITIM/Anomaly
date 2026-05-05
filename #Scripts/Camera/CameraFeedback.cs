using Godot;
using System;

public static class CameraFeedback
{
    public const float SHAKE_DURATION = 0.2f;

    public static void ShakeCamera(ref float shakeIntensity, ref float shakeTimer, float intensity = 0f)
    {
        shakeIntensity = intensity;
        shakeTimer = SHAKE_DURATION;
    }

    public static Vector2 TenacityBreakShake(double delta, ref float shakeDecay, ref float shakeIntensity, ref float shakeTimer)
    {
        if (shakeTimer <= 0)
        {
            return Vector2.Zero;
        }

        shakeTimer -= (float)delta;

        Vector2 offset = new Vector2(
            (float)GD.Randf() * shakeIntensity - shakeIntensity / 2,
            (float)GD.Randf() * shakeIntensity - shakeIntensity / 2
        );

        shakeIntensity -= shakeDecay;

        if (shakeTimer <= 0)
        {
            shakeIntensity = 0f;
            return Vector2.Zero;
        }

        return offset;
    }
}
