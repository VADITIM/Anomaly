using Godot;

namespace Anomaly;

// The V/AS curve set, named after the GSAP eases it was authored in, so a clock reads the same as the motion spec.
// Each phase picks its curve: arrivals land on back.out, departures leave on power2.in, sweeps run power3.inOut.
public static class Motion
{
    private const float BACK = 1.70158f;

    public static float Power2Out(float t) => 1f - Cube(1f - Clamp(t));
    public static float Power2In(float t) => Cube(Clamp(t));
    public static float Power4Out(float t) => 1f - Mathf.Pow(1f - Clamp(t), 5f);

    public static float Power3InOut(float t)
    {
        t = Clamp(t);
        return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;
    }

    public static float BackOut(float t)
    {
        float u = Clamp(t) - 1f;
        return 1f + (BACK + 1f) * u * u * u + BACK * u * u;
    }

    public static float BackIn(float t)
    {
        t = Clamp(t);
        return (BACK + 1f) * t * t * t - BACK * t * t;
    }

    // Hitstop drives Engine.TimeScale; UI feedback runs on real time so it answers on the frame of the hit.
    public static float Unscaled(double delta) => Mathf.Min((float)(delta / Mathf.Max(Engine.TimeScale, 0.01)), 0.1f);

    private static float Clamp(float t) => Mathf.Clamp(t, 0f, 1f);
    private static float Cube(float t) => t * t * t;
}
