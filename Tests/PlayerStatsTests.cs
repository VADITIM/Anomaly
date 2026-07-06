using Xunit;

public class PlayerStatsTests
{
    [Fact]
    public void SetCurrent_ClampsToCurrentMax()
    {
        var stats = new PlayerStats();

        stats.SetCurrent(StatType.Health, 500f);
        Assert.Equal(stats.GetCurrentMax(StatType.Health), stats.GetCurrent(StatType.Health));

        stats.SetCurrent(StatType.Health, -50f);
        Assert.Equal(0f, stats.GetCurrent(StatType.Health));
    }

    [Fact]
    public void SetCurrentMax_ClampsToTotalMax_AndDragsCurrentDown()
    {
        var stats = new PlayerStats();

        stats.SetCurrentMax(StatType.Speed, 9999f);
        Assert.Equal(stats.GetTotalMax(StatType.Speed), stats.GetCurrentMax(StatType.Speed));

        stats.SetCurrent(StatType.Speed, 150f);
        stats.SetCurrentMax(StatType.Speed, 100f);
        Assert.Equal(100f, stats.GetCurrent(StatType.Speed));
    }

    [Fact]
    public void IncreaseStat_RaisesMaxRefillsCurrent_AndCountsUpgrade()
    {
        var stats = new PlayerStats();
        float maxBefore = stats.GetCurrentMax(StatType.Speed);

        stats.IncreaseStat(StatType.Speed, 10f);

        Assert.Equal(maxBefore + 10f, stats.GetCurrentMax(StatType.Speed));
        Assert.Equal(stats.GetCurrentMax(StatType.Speed), stats.GetCurrent(StatType.Speed));
        Assert.Equal(1, stats.GetUpgradeLevels(StatType.Speed));
    }

    [Fact]
    public void IncreaseStat_DoesNothingAtTotalMax()
    {
        var stats = new PlayerStats();

        Assert.False(stats.CanIncrease(StatType.Stamina));
        stats.IncreaseStat(StatType.Stamina, 10f);

        Assert.Equal(300f, stats.GetCurrentMax(StatType.Stamina));
        Assert.Equal(0, stats.GetUpgradeLevels(StatType.Stamina));
    }

    [Fact]
    public void DecreaseStat_OnlyReversesRecordedUpgrades()
    {
        var stats = new PlayerStats();
        float maxBefore = stats.GetCurrentMax(StatType.Speed);

        stats.DecreaseStat(StatType.Speed, 10f);
        Assert.Equal(maxBefore, stats.GetCurrentMax(StatType.Speed));

        stats.IncreaseStat(StatType.Speed, 10f);
        stats.DecreaseStat(StatType.Speed, 10f);
        Assert.Equal(maxBefore, stats.GetCurrentMax(StatType.Speed));
        Assert.Equal(0, stats.GetUpgradeLevels(StatType.Speed));
    }

    [Fact]
    public void StaminaRegeneration_WaitsForCooldown_ThenRegenerates()
    {
        var stats = new PlayerStats();
        stats.SetCurrent(StatType.Stamina, 100f);
        stats.NotifyActionPerformed();

        stats.ProcessStaminaRegeneration(0.5f);
        Assert.Equal(100f, stats.GetCurrent(StatType.Stamina));

        stats.ProcessStaminaRegeneration(0.5f);
        Assert.Equal(125f, stats.GetCurrent(StatType.Stamina));
    }

    [Fact]
    public void StaminaRegeneration_NeverExceedsCurrentMax()
    {
        var stats = new PlayerStats();
        stats.SetCurrent(StatType.Stamina, 299f);
        stats.NotifyActionPerformed();

        stats.ProcessStaminaRegeneration(10f);
        Assert.Equal(300f, stats.GetCurrent(StatType.Stamina));
    }
}
