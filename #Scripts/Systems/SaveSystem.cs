using Godot;
using System;

public static class SaveSystem
{
    public static string SavePath => "user://savegame.json";

    public static void SaveGame()
    {
        var data = new Godot.Collections.Dictionary();
        var player = Player.Instance;
        if (player != null)
        {
            var pos = new Godot.Collections.Dictionary();
            pos["x"] = player.GlobalPosition.X;
            pos["y"] = player.GlobalPosition.Y;
            data["player_position"] = pos;
            data["player_stats"] = player.Stats.ToDictionary();
            if (player.Weapon != null)
                data["weapon_stats"] = player.Weapon.Stats.ToDictionary();
        }

        string json = Json.Stringify(data);
        try
        {
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            f.StoreString(json);
            GD.Print("Game saved successfully!");
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to save game: ", e.Message);
        }

        ((SceneTree)Engine.GetMainLoop()).ReloadCurrentScene();
    }

    public static void LoadGame()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            GD.Print("No save file found.");
            return;
        }

        GD.Print("Loading game...");
        ((SceneTree)Engine.GetMainLoop()).ReloadCurrentScene();
    }

    public static void ApplyLoadedData()
    {
        if (!FileAccess.FileExists(SavePath))
            return;

        try
        {
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            string json = f.GetAsText();
            var json_obj = new Json();
            json_obj.Parse(json);
            var dict = json_obj.Data.AsGodotDictionary();

            var player = Player.Instance;
            if (player != null)
            {
                if (dict.TryGetValue("player_position", out var posVar))
                {
                    var pos = posVar.AsGodotDictionary();
                    if (pos.TryGetValue("x", out var xVar) && pos.TryGetValue("y", out var yVar))
                    {
                        float x = (float)xVar;
                        float y = (float)yVar;
                        player.GlobalPosition = new Vector2(x, y);
                    }
                }

                if (dict.TryGetValue("player_stats", out var psVar))
                {
                    var ps = psVar.AsGodotDictionary();
                    player.Stats.LoadFromDictionary(ps);
                }

                if (dict.TryGetValue("weapon_stats", out var wsVar) && player.Weapon != null)
                {
                    var ws = wsVar.AsGodotDictionary();
                    player.Weapon.Stats.LoadFromDictionary(ws);
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to load game: ", e.Message);
        }
    }
}
