using Xunit;

public class WeaponStatsTests
{
    [Fact]
    public void SetCurrent_ClampsToMax()
    {
        var stats = new WeaponStats();

        stats.SetCurrent(WeaponStatType.Damage, 9999f);
        Assert.Equal(stats.GetMax(WeaponStatType.Damage), stats.GetCurrent(WeaponStatType.Damage));

        stats.SetCurrent(WeaponStatType.Damage, -5f);
        Assert.Equal(0f, stats.GetCurrent(WeaponStatType.Damage));
    }

    [Fact]
    public void SetMax_DragsCurrentDown()
    {
        var stats = new WeaponStats();

        stats.SetCurrent(WeaponStatType.Damage, 12f);
        stats.SetMax(WeaponStatType.Damage, 10f);

        Assert.Equal(10f, stats.GetCurrent(WeaponStatType.Damage));
    }

    [Fact]
    public void IncreaseStat_RaisesMaxRefillsCurrent_AndCountsUpgrade()
    {
        var stats = new WeaponStats();
        float maxBefore = stats.GetMax(WeaponStatType.Damage);

        stats.IncreaseStat(WeaponStatType.Damage, 5f);

        Assert.Equal(maxBefore + 5f, stats.GetMax(WeaponStatType.Damage));
        Assert.Equal(stats.GetMax(WeaponStatType.Damage), stats.GetCurrent(WeaponStatType.Damage));
        Assert.Equal(1, stats.GetUpgradeLevels(WeaponStatType.Damage));
    }

    [Fact]
    public void DecreaseStat_OnlyReversesRecordedUpgrades()
    {
        var stats = new WeaponStats();
        float maxBefore = stats.GetMax(WeaponStatType.TenacityDamage);

        stats.DecreaseStat(WeaponStatType.TenacityDamage, 5f);
        Assert.Equal(maxBefore, stats.GetMax(WeaponStatType.TenacityDamage));

        stats.IncreaseStat(WeaponStatType.TenacityDamage, 5f);
        stats.DecreaseStat(WeaponStatType.TenacityDamage, 5f);
        Assert.Equal(maxBefore, stats.GetMax(WeaponStatType.TenacityDamage));
        Assert.Equal(0, stats.GetUpgradeLevels(WeaponStatType.TenacityDamage));
    }

    [Fact]
    public void UnknownStat_ReadsAsZero_AndIgnoresWrites()
    {
        var stats = new WeaponStats();

        Assert.Null(stats.GetStat((WeaponStatType)999));
        Assert.Equal(0f, stats.GetCurrent((WeaponStatType)999));
        stats.SetCurrent((WeaponStatType)999, 5f);
    }
}
