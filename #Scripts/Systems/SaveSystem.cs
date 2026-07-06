using Godot;
using System;

public static class SaveSystem
{
    // Versioned envelope: { "version": N, "domains": { "<name>": {...} } }.
    // Bump the version when a domain's shape changes and handle the old shape in ApplyLoadedData.
    public const int SaveVersion = 2;

    public static string SavePath => "user://savegame.json";

    public static void SaveGame()
    {
        var player = Player.Instance;
        if (player == null)
        {
            GD.PushError("SaveSystem: no Player instance — nothing to save.");
            return;
        }

        var domains = new Godot.Collections.Dictionary();

        var playerDomain = new Godot.Collections.Dictionary();
        playerDomain["PositionX"] = player.GlobalPosition.X;
        playerDomain["PositionY"] = player.GlobalPosition.Y;
        playerDomain["Stats"] = player.Stats.ToDictionary();
        domains["Player"] = playerDomain;

        if (player.Weapon != null)
        {
            var weaponDomain = new Godot.Collections.Dictionary();
            weaponDomain["Stats"] = player.Weapon.Stats.ToDictionary();
            domains["Weapon"] = weaponDomain;
        }

        var envelope = new Godot.Collections.Dictionary();
        envelope["version"] = SaveVersion;
        envelope["domains"] = domains;

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            file.StoreString(Json.Stringify(envelope));
            GD.Print("Game saved.");
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to save game: ", e.Message);
        }
    }

    public static void LoadGame()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            GD.Print("No save file found.");
            return;
        }

        // The scene reload re-runs Player._Ready, which calls ApplyLoadedData.
        ((SceneTree)Engine.GetMainLoop()).ReloadCurrentScene();
    }

    public static void ApplyLoadedData()
    {
        if (!FileAccess.FileExists(SavePath))
            return;

        var player = Player.Instance;
        if (player == null)
            return;

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var json = new Json();
            json.Parse(file.GetAsText());
            var root = json.Data.AsGodotDictionary();

            if (root.TryGetValue("domains", out var domainsVar))
                ApplyDomains(player, domainsVar.AsGodotDictionary());
            else
                ApplyLegacyFormat(player, root);
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to load game: ", e.Message);
        }
    }

    private static void ApplyDomains(Player player, Godot.Collections.Dictionary domains)
    {
        if (domains == null)
            return;

        if (domains.TryGetValue("Player", out var playerVar))
        {
            var playerDomain = playerVar.AsGodotDictionary();
            if (playerDomain.TryGetValue("PositionX", out var x) && playerDomain.TryGetValue("PositionY", out var y))
                player.GlobalPosition = new Vector2((float)x, (float)y);
            if (playerDomain.TryGetValue("Stats", out var stats))
                player.Stats.LoadFromDictionary(stats.AsGodotDictionary());
        }

        if (domains.TryGetValue("Weapon", out var weaponVar) && player.Weapon != null)
        {
            var weaponDomain = weaponVar.AsGodotDictionary();
            if (weaponDomain.TryGetValue("Stats", out var stats))
                player.Weapon.Stats.LoadFromDictionary(stats.AsGodotDictionary());
        }
    }

    // Pre-envelope saves: flat player_position/player_stats/weapon_stats keys.
    private static void ApplyLegacyFormat(Player player, Godot.Collections.Dictionary dict)
    {
        if (dict.TryGetValue("player_position", out var posVar))
        {
            var pos = posVar.AsGodotDictionary();
            if (pos.TryGetValue("x", out var xVar) && pos.TryGetValue("y", out var yVar))
                player.GlobalPosition = new Vector2((float)xVar, (float)yVar);
        }

        if (dict.TryGetValue("player_stats", out var psVar))
            player.Stats.LoadFromDictionary(psVar.AsGodotDictionary());

        if (dict.TryGetValue("weapon_stats", out var wsVar) && player.Weapon != null)
            player.Weapon.Stats.LoadFromDictionary(wsVar.AsGodotDictionary());
    }
}
