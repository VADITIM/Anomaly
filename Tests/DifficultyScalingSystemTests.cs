using System;
using Xunit;

public class DifficultyScalingSystemTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EnemyLevelWeights_SumTo100_PerWorldLevel(int worldLevel)
    {
        int sum = 0;
        for (int enemyLevel = 1; enemyLevel <= DifficultyScalingSystem.MaxEnemyLevel; enemyLevel++)
            sum += DifficultyScalingSystem.GetEnemyLevelWeight(worldLevel, enemyLevel);

        Assert.Equal(100, sum);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Level6_IsExclusiveToWorld5(int worldLevel)
    {
        Assert.Equal(0, DifficultyScalingSystem.GetEnemyLevelWeight(worldLevel, 6));
    }

    [Fact]
    public void Level6_AppearsAtWorld5()
    {
        Assert.Equal(10, DifficultyScalingSystem.GetEnemyLevelWeight(5, 6));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SampleEnemyLevel_NeverReturnsZeroWeightLevel(int worldLevel)
    {
        var random = new Random(worldLevel);

        for (int i = 0; i < 2000; i++)
        {
            int sampled = DifficultyScalingSystem.SampleEnemyLevel(worldLevel, random);

            Assert.InRange(sampled, 1, DifficultyScalingSystem.MaxEnemyLevel);
            Assert.True(DifficultyScalingSystem.GetEnemyLevelWeight(worldLevel, sampled) > 0,
                $"World {worldLevel} sampled enemy level {sampled}, which has zero weight.");
        }
    }

    [Fact]
    public void SampleEnemyLevel_MatchesDistribution_AtWorld1()
    {
        var random = new Random(42);
        int level1Count = 0;

        const int samples = 10000;
        for (int i = 0; i < samples; i++)
        {
            if (DifficultyScalingSystem.SampleEnemyLevel(1, random) == 1)
                level1Count++;
        }

        Assert.InRange(level1Count / (float)samples, 0.87f, 0.93f);
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(99, 3, 6)]
    public void SampleEnemyLevel_ClampsWorldLevelOutOfRange(int worldLevel, int minExpected, int maxExpected)
    {
        var random = new Random(7);

        for (int i = 0; i < 500; i++)
            Assert.InRange(DifficultyScalingSystem.SampleEnemyLevel(worldLevel, random), minExpected, maxExpected);
    }

    [Theory]
    [InlineData(1, 1.00f)]
    [InlineData(2, 1.20f)]
    [InlineData(3, 1.45f)]
    [InlineData(4, 1.75f)]
    [InlineData(5, 2.10f)]
    [InlineData(6, 2.50f)]
    public void ScaleStat_AppliesDocumentedMultiplier(int enemyLevel, float expectedMultiplier)
    {
        Assert.Equal(100f * expectedMultiplier, DifficultyScalingSystem.ScaleStat(100f, enemyLevel), 3);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    public void NextDifficultyLevel_IncrementsAndCapsAt5(int current, int expected)
    {
        Assert.Equal(expected, DifficultyScalingSystem.NextDifficultyLevel(current));
    }
}
