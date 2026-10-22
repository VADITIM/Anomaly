using Godot;

namespace Anomaly;

// Hitstop and slow motion both drive Engine.TimeScale; they are tracked in real time so a freeze never extends itself.
// Hitstop is a short hard freeze that eases back over its last frames, so the swing resumes instead of snapping.
// Stops that land in quick succession are shortened: a fast string stays crisp rather than stuttering.
public static class TimeControl
{
    private const float SLOW_MOTION_BLEND_OUT = 0.15f;
    private const float HIT_STOP_RELEASE = 0.025f;
    private const float HIT_STOP_MAX = 0.1f;
    private const float CHAIN_WINDOW = 0.22f;
    private const float CHAIN_FALLOFF = 0.55f;

    private static float hitStopRemaining;
    private static float hitStopScale = 0.03f;
    private static float sinceLastHitStop = 99f;
    private static float slowMotionRemaining;
    private static float slowMotionScale = 1f;
    private static float slowMotionBlendOut = SLOW_MOTION_BLEND_OUT;

    public static bool IsFrozen => hitStopRemaining > HIT_STOP_RELEASE;

    public static void HitStop(float duration, float scale = 0.03f)
    {
        if (sinceLastHitStop < CHAIN_WINDOW)
            duration *= CHAIN_FALLOFF;
        duration = Mathf.Min(duration, HIT_STOP_MAX) + HIT_STOP_RELEASE;
        if (duration <= hitStopRemaining)
            return;
        hitStopRemaining = duration;
        hitStopScale = scale;
        sinceLastHitStop = 0f;
        Apply();
    }

    public static void SlowMotion(float duration, float scale, float blendOut = SLOW_MOTION_BLEND_OUT)
    {
        slowMotionRemaining = Mathf.Max(slowMotionRemaining, duration);
        slowMotionScale = scale;
        slowMotionBlendOut = blendOut;
        Apply();
    }

    public static void Update(float realDelta)
    {
        hitStopRemaining = Mathf.Max(0f, hitStopRemaining - realDelta);
        slowMotionRemaining = Mathf.Max(0f, slowMotionRemaining - realDelta);
        sinceLastHitStop += realDelta;
        Apply();
    }

    public static void Reset()
    {
        hitStopRemaining = 0f;
        slowMotionRemaining = 0f;
        sinceLastHitStop = 99f;
        Engine.TimeScale = 1.0;
    }

    private static void Apply()
    {
        float scale = 1f;
        if (slowMotionRemaining > 0f)
        {
            float blend = Mathf.Clamp(slowMotionRemaining / slowMotionBlendOut, 0f, 1f);
            scale = Mathf.Lerp(1f, slowMotionScale, blend);
        }
        if (hitStopRemaining > 0f)
        {
            float release = 1f - Mathf.Clamp(hitStopRemaining / HIT_STOP_RELEASE, 0f, 1f);
            scale = Mathf.Min(scale, Mathf.Lerp(hitStopScale, 1f, release * release));
        }
        Engine.TimeScale = scale;
    }
}
