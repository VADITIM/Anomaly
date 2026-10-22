using Godot;

namespace Anomaly;

public static class DifficultyScalingSystem
{
    public const int MAX_DIFFICULTY = 5;
    public const int MAX_ENEMY_LEVEL = 6;

    private static readonly float[][] LEVEL_WEIGHTS =
    {
        new[] { 0.90f, 0.10f },
        new[] { 0.30f, 0.60f, 0.10f },
        new[] { 0.10f, 0.25f, 0.55f, 0.10f },
        new[] { 0.00f, 0.05f, 0.20f, 0.65f, 0.10f },
        new[] { 0.00f, 0.00f, 0.05f, 0.10f, 0.75f, 0.10f },
    };

    private static readonly float[] STAT_MULTIPLIERS = { 1.00f, 1.20f, 1.45f, 1.75f, 2.10f, 2.50f };

    public static int SampleEnemyLevel(int difficultyLevel)
    {
        float[] weights = LEVEL_WEIGHTS[Mathf.Clamp(difficultyLevel, 1, MAX_DIFFICULTY) - 1];
        float roll = GD.Randf();
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return i + 1;
        }
        return weights.Length;
    }

    public static float ScaleStat(float baseStat, int enemyLevel) => baseStat * STAT_MULTIPLIERS[Mathf.Clamp(enemyLevel, 1, MAX_ENEMY_LEVEL) - 1];

    public static bool IsRegionFiveUnlocked(int difficultyLevel) => difficultyLevel >= MAX_DIFFICULTY;

    public static void RegisterVoidHeartDestroyed()
    {
        SaveManager.Difficulty.DifficultyLevel = Mathf.Min(SaveManager.Difficulty.DifficultyLevel + 1, MAX_DIFFICULTY);
    }
}
