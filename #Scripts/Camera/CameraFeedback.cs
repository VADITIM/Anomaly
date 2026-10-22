using Godot;

namespace Anomaly;

// Trauma-based shake (offset ~ trauma^2) plus a short zoom punch for heavy moments.
public sealed class CameraFeedback
{
    private const float MAX_OFFSET = 7f;
    private const float MAX_ROLL = 0.018f;
    private const float TRAUMA_DECAY = 1.9f;
    private const float PUNCH_DECAY = 7f;

    private readonly FastNoiseLite noise = new() { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.9f };
    private float trauma;
    private float punch;
    private float time;

    public Vector2 Offset { get; private set; }
    public float Roll { get; private set; }
    public float ZoomBoost => punch;

    public void AddTrauma(float amount) => trauma = Mathf.Min(1f, trauma + amount * GameSettings.ScreenShake);

    public void Punch(float amount) => punch = Mathf.Max(punch, amount * Mathf.Min(1f, GameSettings.ScreenShake));

    public void TenacityShatter()
    {
        AddTrauma(0.5f);
        Punch(0.07f);
    }

    public void Update(float realDelta)
    {
        time += realDelta * 60f;
        trauma = Mathf.Max(0f, trauma - TRAUMA_DECAY * realDelta);
        punch = Mathf.Lerp(punch, 0f, 1f - Mathf.Exp(-PUNCH_DECAY * realDelta));
        float shake = trauma * trauma;
        Offset = new Vector2(noise.GetNoise2D(time, 0f), noise.GetNoise2D(0f, time)) * MAX_OFFSET * shake;
        Roll = noise.GetNoise2D(time, 100f) * MAX_ROLL * shake;
    }
}
