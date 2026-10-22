using Godot;

namespace Anomaly;

public static class SaveManager
{
    private const string PLAYER_STATS_PATH = "user://player_stats.res";
    private const string WEAPON_STATS_PATH = "user://weapon_stats.res";
    private const string PROGRESSION_PATH = "user://progression.res";
    private const string WORLD_DATA_PATH = "user://world_data.res";
    private const string DIFFICULTY_PATH = "user://difficulty_data.res";

    public static PlayerStats PlayerStats { get; private set; }
    public static WeaponStats WeaponStats { get; private set; }
    public static ProgressionData Progression { get; private set; }
    public static WorldData World { get; private set; }
    public static DifficultyData Difficulty { get; private set; }

    public static void Load()
    {
        PlayerStats = LoadOrCreate<PlayerStats>(PLAYER_STATS_PATH);
        WeaponStats = LoadOrCreate<WeaponStats>(WEAPON_STATS_PATH);
        WeaponStats.MigrateLegacy();
        Progression = LoadOrCreate<ProgressionData>(PROGRESSION_PATH);
        World = LoadOrCreate<WorldData>(WORLD_DATA_PATH);
        Difficulty = LoadOrCreate<DifficultyData>(DIFFICULTY_PATH);
    }

    public static void Save(SavePoint savePoint)
    {
        if (savePoint != SavePoint.None)
            PlayerStats.LastSavePoint = savePoint;
        ResourceSaver.Save(PlayerStats, PLAYER_STATS_PATH);
        ResourceSaver.Save(WeaponStats, WEAPON_STATS_PATH);
        ResourceSaver.Save(Progression, PROGRESSION_PATH);
        ResourceSaver.Save(World, WORLD_DATA_PATH);
        ResourceSaver.Save(Difficulty, DIFFICULTY_PATH);
    }

    public static void NewGame()
    {
        PlayerStats = new PlayerStats();
        WeaponStats = new WeaponStats();
        Progression = new ProgressionData();
        World = new WorldData();
        Difficulty = new DifficultyData();
    }

    private static T LoadOrCreate<T>(string path) where T : Resource, new()
    {
        if (ResourceLoader.Exists(path))
        {
            T loaded = ResourceLoader.Load<T>(path, null, ResourceLoader.CacheMode.Ignore);
            if (loaded != null)
                return loaded;
        }
        return new T();
    }
}
