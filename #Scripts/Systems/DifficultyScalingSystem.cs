using System;

// Pure math on purpose — no Godot dependencies, so the balance tables stay unit-testable.
// World DifficultyLevel persists in DifficultyData; EnemyLevel is sampled per enemy
// at _Ready and never saved (design.md §3.12).
public static class DifficultyScalingSystem
{
    public const int MinDifficultyLevel = 1;
    public const int MaxDifficultyLevel = 5;
    public const int MaxEnemyLevel = 6;

    private static readonly Random SharedRandom = new();

    // Rows: world DifficultyLevel 1-5. Columns: EnemyLevel 1-6, in percent.
    private static readonly int[][] EnemyLevelWeights =
    {
        new[] { 90, 10,  0,  0,  0,  0 },
        new[] { 30, 60, 10,  0,  0,  0 },
        new[] { 10, 25, 55, 10,  0,  0 },
        new[] {  0,  5, 20, 65, 10,  0 },
        new[] {  0,  0,  5, 10, 75, 10 }
    };

    // EnemyLevel 1-6. Placeholders — balance pass required (design.md §3.12).
    private static readonly float[] StatMultipliers = { 1.00f, 1.20f, 1.45f, 1.75f, 2.10f, 2.50f };

    public static int NextDifficultyLevel(int currentLevel)
        => Math.Clamp(currentLevel + 1, MinDifficultyLevel, MaxDifficultyLevel);

    public static int SampleEnemyLevel(int worldDifficultyLevel)
        => SampleEnemyLevel(worldDifficultyLevel, SharedRandom);

    public static int SampleEnemyLevel(int worldDifficultyLevel, Random random)
    {
        int row = Math.Clamp(worldDifficultyLevel, MinDifficultyLevel, MaxDifficultyLevel) - 1;
        int[] weights = EnemyLevelWeights[row];

        int roll = random.Next(0, 100);
        int cumulative = 0;
        for (int level = 0; level < weights.Length; level++)
        {
            cumulative += weights[level];
            if (roll < cumulative)
                return level + 1;
        }

        return weights.Length;
    }

    public static float ScaleStat(float baseValue, int enemyLevel)
    {
        int index = Math.Clamp(enemyLevel, 1, MaxEnemyLevel) - 1;
        return baseValue * StatMultipliers[index];
    }

    public static float GetStatMultiplier(int enemyLevel)
        => ScaleStat(1f, enemyLevel);

    public static int GetEnemyLevelWeight(int worldDifficultyLevel, int enemyLevel)
    {
        int row = Math.Clamp(worldDifficultyLevel, MinDifficultyLevel, MaxDifficultyLevel) - 1;
        int column = Math.Clamp(enemyLevel, 1, MaxEnemyLevel) - 1;
        return EnemyLevelWeights[row][column];
    }
}
