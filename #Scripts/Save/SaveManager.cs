using Godot;
using System;

// Owns the single mutable instance of each save Resource for the session (design.md §3.11).
// All systems read save data through these properties — never by loading Resources directly.
public static class SaveManager
{
    private const string PlayerSavePath = "user://player_save.res";
    private const string WeaponSavePath = "user://weapon_save.res";
    private const string ProgressionPath = "user://progression.res";
    private const string WorldDataPath = "user://world_data.res";
    private const string DifficultyPath = "user://difficulty_data.res";
    private const string LegacyJsonPath = "user://savegame.json";

    public static PlayerSaveData PlayerSave { get; private set; }
    public static WeaponSaveData WeaponSave { get; private set; }
    public static ProgressionData Progression { get; private set; }
    public static WorldData World { get; private set; }
    public static DifficultyData Difficulty { get; private set; }

    public static bool IsLoaded => PlayerSave != null;
    public static bool HasExistingSave { get; private set; }

    public static void EnsureLoaded()
    {
        if (!IsLoaded)
            Load();
    }

    public static void Load()
    {
        bool anyDomainOnDisk = ResourceLoader.Exists(PlayerSavePath)
                            || ResourceLoader.Exists(WeaponSavePath)
                            || ResourceLoader.Exists(ProgressionPath)
                            || ResourceLoader.Exists(WorldDataPath)
                            || ResourceLoader.Exists(DifficultyPath);

        PlayerSave = LoadOrCreate<PlayerSaveData>(PlayerSavePath);
        WeaponSave = LoadOrCreate<WeaponSaveData>(WeaponSavePath);
        Progression = LoadOrCreate<ProgressionData>(ProgressionPath);
        World = LoadOrCreate<WorldData>(WorldDataPath);
        Difficulty = LoadOrCreate<DifficultyData>(DifficultyPath);

        HasExistingSave = anyDomainOnDisk;

        if (!anyDomainOnDisk && FileAccess.FileExists(LegacyJsonPath))
            HasExistingSave = TryImportLegacyJson();
    }

    // All domains are written together on every trigger — there is no partial save.
    // SavePoint.None (canonical events, NPC interactions) never moves the respawn point.
    public static void Save(Player player, SavePoint savePoint)
    {
        EnsureLoaded();

        if (savePoint != SavePoint.None)
            PlayerSave.LastSavePoint = savePoint;

        PlayerSave.Position = player.GlobalPosition;
        PlayerSave.Stats = player.Stats.ToDictionary();
        WeaponSave.Stats = player.Weapon.Stats.ToDictionary();

        bool allWritten = SaveResource(PlayerSave, PlayerSavePath);
        allWritten &= SaveResource(WeaponSave, WeaponSavePath);
        allWritten &= SaveResource(Progression, ProgressionPath);
        allWritten &= SaveResource(World, WorldDataPath);
        allWritten &= SaveResource(Difficulty, DifficultyPath);

        if (allWritten)
        {
            HasExistingSave = true;
            GD.Print("Game saved.");
        }
    }

    public static void ApplyTo(Player player)
    {
        EnsureLoaded();

        if (!HasExistingSave)
            return;

        player.GlobalPosition = PlayerSave.Position;
        player.Stats.LoadFromDictionary(PlayerSave.Stats);
        player.Weapon.Stats.LoadFromDictionary(WeaponSave.Stats);
    }

    public static void RecordVoidHeartDestroyed(Player player, string voidHeartId)
    {
        EnsureLoaded();

        if (!World.DestroyedVoidHearts.Contains(voidHeartId))
            World.DestroyedVoidHearts.Add(voidHeartId);

        Difficulty.DifficultyLevel = DifficultyScalingSystem.NextDifficultyLevel(Difficulty.DifficultyLevel);
        Save(player, SavePoint.None);
    }

    // Drops the session instances and reloads the scene; Player._Ready re-applies from disk.
    public static void ReloadFromDisk()
    {
        PlayerSave = null;
        WeaponSave = null;
        Progression = null;
        World = null;
        Difficulty = null;
        HasExistingSave = false;

        ((SceneTree)Engine.GetMainLoop()).ReloadCurrentScene();
    }

    private static T LoadOrCreate<T>(string path) where T : Resource, new()
    {
        if (!ResourceLoader.Exists(path))
            return new T();

        var resource = ResourceLoader.Load<T>(path, null, ResourceLoader.CacheMode.Ignore);
        if (resource == null)
        {
            GD.PushError($"SaveManager: '{path}' exists but failed to load as {typeof(T).Name} — starting that domain fresh.");
            return new T();
        }

        return resource;
    }

    private static bool SaveResource(Resource resource, string path)
    {
        Error result = ResourceSaver.Save(resource, path);
        if (result != Error.Ok)
            GD.PushError($"SaveManager: failed to write '{path}' ({result}).");

        return result == Error.Ok;
    }

    // Pre-.res saves: versioned JSON envelope (v2) or flat keys (v1). Imported once,
    // then persisted as .res domains on the next save trigger.
    private static bool TryImportLegacyJson()
    {
        try
        {
            using var file = FileAccess.Open(LegacyJsonPath, FileAccess.ModeFlags.Read);
            var json = new Json();
            if (json.Parse(file.GetAsText()) != Error.Ok)
            {
                GD.PushError("SaveManager: legacy JSON save is unreadable — ignoring it.");
                return false;
            }

            var root = json.Data.AsGodotDictionary();

            if (root.TryGetValue("domains", out var domainsVariant))
                ImportLegacyEnvelope(domainsVariant.AsGodotDictionary());
            else
                ImportLegacyFlatKeys(root);

            GD.Print("SaveManager: imported legacy JSON save.");
            return true;
        }
        catch (Exception e)
        {
            GD.PushError("SaveManager: failed to import legacy JSON save: ", e.Message);
            return false;
        }
    }

    private static void ImportLegacyEnvelope(Godot.Collections.Dictionary domains)
    {
        if (domains == null)
            return;

        if (domains.TryGetValue("Player", out var playerVariant))
        {
            var playerDomain = playerVariant.AsGodotDictionary();
            if (playerDomain.TryGetValue("PositionX", out var x) && playerDomain.TryGetValue("PositionY", out var y))
                PlayerSave.Position = new Vector2((float)x, (float)y);
            if (playerDomain.TryGetValue("Stats", out var stats))
                PlayerSave.Stats = stats.AsGodotDictionary();
        }

        if (domains.TryGetValue("Weapon", out var weaponVariant))
        {
            var weaponDomain = weaponVariant.AsGodotDictionary();
            if (weaponDomain.TryGetValue("Stats", out var stats))
                WeaponSave.Stats = stats.AsGodotDictionary();
        }
    }

    private static void ImportLegacyFlatKeys(Godot.Collections.Dictionary root)
    {
        if (root.TryGetValue("player_position", out var positionVariant))
        {
            var position = positionVariant.AsGodotDictionary();
            if (position.TryGetValue("x", out var x) && position.TryGetValue("y", out var y))
                PlayerSave.Position = new Vector2((float)x, (float)y);
        }

        if (root.TryGetValue("player_stats", out var playerStats))
            PlayerSave.Stats = playerStats.AsGodotDictionary();

        if (root.TryGetValue("weapon_stats", out var weaponStats))
            WeaponSave.Stats = weaponStats.AsGodotDictionary();
    }
}
