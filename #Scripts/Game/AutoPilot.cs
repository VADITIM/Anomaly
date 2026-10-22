using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Anomaly;

// Verification harness: `-- --autopilot --capture=<dir>` drives a scripted tour and saves screenshots.
public partial class AutoPilot : Node
{
    private readonly List<(float time, Action action)> schedule = new();
    private GameWorld world;
    private string captureDirectory;
    private float time;
    private int nextStep;
    private ulong lastTicks;

    private const float MOVIE_FPS = 60f;
    private static readonly bool IsRecordingMovie = OS.GetCmdlineArgs().Contains("--write-movie");

    public static bool IsRequested() => OS.GetCmdlineUserArgs().Any(argument => argument.StartsWith("--autopilot"));

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = -1000;
        // tools/autopilot.ps1 shows the window without activating it; this keeps later clicks from activating it either.
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        world = GetParent<GameWorld>();
        captureDirectory = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--capture="))?.Substring("--capture=".Length) ?? OS.GetUserDataDir();
        DirAccess.MakeDirRecursiveAbsolute(captureDirectory);
        lastTicks = Time.GetTicksUsec();
        BuildSchedule();
    }

    private void At(float seconds, Action action) => schedule.Add((seconds, action));

    // Authored in Hollow Reach's local frame; the Reach now sits inside a larger world.
    private void Teleport(int x, int y) => world.Player.PlaceAt(WorldGrid.TileCenter(x + LevelBlueprint.HollowReachOrigin.X, y + LevelBlueprint.HollowReachOrigin.Y));

    private void TeleportWorld(int x, int y) => world.Player.PlaceAt(WorldGrid.TileCenter(x, y));

    private void Tap(string action)
    {
        Input.ActionPress(action);
        GetTree().CreateTimer(0.06, true, false, true).Timeout += () => Input.ActionRelease(action);
    }

    private void Hold(string action, float seconds)
    {
        Input.ActionPress(action);
        GetTree().CreateTimer(seconds, true, false, true).Timeout += () => Input.ActionRelease(action);
    }

    private void Aim(Vector2 direction) => GameInput.AimOverride = direction;

    private void Capture(string name)
    {
        Image image = GetViewport().GetTexture().GetImage();
        string path = $"{captureDirectory}/{name}.png";
        image.SavePng(path);
        GD.Print($"[AutoPilot] captured {path}");
    }

    // One character per tile: ' ' pit, '.' ground, digits raised ground, '#' wall. Read by the section layout tools.
    private void DumpGrid()
    {
        WorldGrid grid = world.Grid;
        var text = new System.Text.StringBuilder();
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                int height = grid.HeightAt(x, y);
                text.Append(height < 0 ? ' ' : height == 0 ? '.' : height >= WorldGrid.WALL_HEIGHT ? '#' : (char)('0' + height));
            }
            text.Append('\n');
        }
        using var file = FileAccess.Open($"{captureDirectory}/grid.txt", FileAccess.ModeFlags.Write);
        file.StoreString(text.ToString());
    }

    private void BuildSchedule()
    {
        if (OS.GetCmdlineUserArgs().Contains("--regioncheck"))
        {
            BuildRegionCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--voidcheck"))
        {
            BuildVoidCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--griddump"))
        {
            At(0.5f, DumpGrid);
            At(0.6f, () => GetTree().Quit());
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--sectioncheck"))
        {
            BuildSectionCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--seamcheck"))
        {
            BuildSeamCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--chasecheck"))
        {
            BuildChaseCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--perfcheck"))
        {
            BuildPerfCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--enemycheck"))
        {
            BuildEnemyCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--animcheck"))
        {
            BuildAnimationCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--combocheck"))
        {
            BuildComboCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--strikecheck"))
        {
            BuildStrikeCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--ragecheck"))
        {
            // The Aetheriac State has no timer: it must hold until the drain kills Kio.
            At(1.0f, () => { world.Player.AddCorruption(Player.CORRUPTION_MAXIMUM); Tap(GameInput.AETHERIAC); });
            for (int s = 2; s <= 20; s++)
                At(s, () => GD.Print($"[AutoPilot] rage: active={world.Player.AetheriacActive} health={world.Player.Health:0.#} dead={world.Player.IsDead}"));
            At(20.2f, () => GetTree().Quit());
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--fallcheck"))
        {
            At(1.0f, WalkIntoPitUnderCliff);
            for (int f = 0; f < 12; f++)
            {
                int frame = f;
                At(1.1f + 0.05f * f, () => Capture($"fall_{frame:00}"));
            }
            At(2.2f, () => GetTree().Quit());
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--hovercheck"))
        {
            BuildHoverCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--uicheck"))
        {
            BuildUiCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--glowcheck"))
        {
            BuildGlowCheck();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--cuecheck"))
        {
            BuildCueCheck();
            return;
        }
        At(0.5f, () => world.Camera.SetOverview(true));
        At(0.9f, () => Capture("00_overview"));
        At(1.0f, () => { world.Camera.SetOverview(false); world.Camera.SnapToPlayer(); });
        At(1.6f, () => Capture("01_start"));
        At(2.0f, () => { Teleport(8, 27); Aim(Vector2.Up); });
        At(2.3f, () => Hold(GameInput.MOVE_UP, 0.5f));
        At(3.0f, () => Tap(GameInput.ATTACK));
        At(3.25f, () => Tap(GameInput.ATTACK));
        At(3.3f, () => Capture("02_combo"));
        At(3.5f, () => Tap(GameInput.ATTACK));
        At(3.62f, () => Capture("03_finisher"));
        At(4.2f, () => { Hold(GameInput.MOVE_UP, 0.3f); Tap(GameInput.DASH); });
        At(4.27f, () => Capture("04_dash"));
        At(5.0f, () => { Teleport(15, 13); Aim(Vector2.Right); });
        At(5.6f, () => Tap(GameInput.SPECIAL));
        At(5.8f, () => Capture("05_scythe_throw"));
        At(6.4f, () => { Tap(GameInput.ATTACK); });
        At(6.7f, () => { Tap(GameInput.ATTACK); });
        At(7.0f, () => Capture("06_arena"));
        At(7.6f, () => { Teleport(7, 9); Hold(GameInput.MOVE_UP, 0.6f); Tap(GameInput.JUMP); });
        At(7.85f, () => Capture("07_jump"));
        At(8.8f, () => { Teleport(47, 18); Aim(Vector2.Right); });
        At(9.8f, () => Capture("08_brute"));
        At(10.2f, () => { world.Player.AddCorruption(Player.CORRUPTION_MAXIMUM); Tap(GameInput.AETHERIAC); });
        At(10.6f, () => Tap(GameInput.ATTACK));
        At(10.9f, () => Tap(GameInput.ATTACK));
        At(11.0f, () => Capture("09_aetheriac"));
        At(11.6f, () => Tap(GameInput.AETHERIAC));
        At(12.0f, () => { Teleport(52, 8); Aim(Vector2.Up); world.Player.AddVessel(Player.VESSEL_MAXIMUM); });
        At(12.5f, () => Capture("09b_heal_filling"));
        At(13.6f, () => world.Player.AddVessel(Player.VESSEL_MAXIMUM));
        At(14.6f, () => Capture("10_void_heart"));
        At(15.4f, () => { Teleport(31, 12); });
        At(15.9f, () => Capture("11_void_fall"));
        At(17.0f, () => { Teleport(9, 36); world.OpenAnvilMenu(); });
        At(17.6f, () => Capture("12_anvil"));
        At(18.2f, () => GetTree().Quit());
    }

    // Every UI surface in turn: HUD with a region card, prompt and boss bar, then each menu, the map, dialogue and death.
    private void BuildUiCheck()
    {
        At(0.8f, () => { Teleport(47, 18); world.Camera.SnapToPlayer(); });
        At(1.4f, () =>
        {
            WorldSignals.EmitRegionEntered("HOLLOW REACH", "Where the ground forgot its shape");
            WorldSignals.EmitInteractionPromptChanged("Rest at the anvil");
            world.Hud.Toast("The edge remembers more.");
        });
        At(2.6f, () => Capture("ui_01_hud"));
        At(2.8f, () => { WorldSignals.EmitInteractionPromptChanged(null); world.OpenPauseForTest(false); });
        At(3.3f, () => Capture("ui_02_pause"));
        At(3.4f, () => world.OpenPauseForTest(true));
        At(3.9f, () => Capture("ui_03_options"));
        At(4.0f, () => { world.CloseMenusForTest(); Teleport(9, 36); world.OpenAnvilMenu(); });
        At(4.6f, () => Capture("ui_04_anvil"));
        At(4.7f, () => { world.CloseMenusForTest(); world.OpenBonfireForTest(); });
        At(5.2f, () => Capture("ui_05_bonfire"));
        At(5.3f, () => { world.CloseMenusForTest(); world.OpenWorldMap(); });
        At(5.8f, () => Capture("ui_06_map"));
        At(5.9f, () => { world.CloseMenusForTest(); world.ShowDeathForTest(true); });
        At(7.7f, () => Capture("ui_07_death"));
        At(7.8f, () => world.ShowDeathForTest(false));
        // The intro Voice may still be talking; the scene starts on the first check after it closes.
        bool talked = false;
        for (float t = 8.2f; t < 20f; t += 0.2f)
            At(t, () =>
            {
                if (talked || world.Narration.IsOpen)
                    return;
                talked = true;
                world.Narration.Play(new Conversation("a")
                    .Ask("a", Speaker.MALDOR, "The Scythe hungers again. Will you feed it what you carry?", new NarrationChoice("Yes", null), new NarrationChoice("Not yet", null)));
                GetTree().CreateTimer(1.9, true, false, true).Timeout += () => Capture("ui_08_dialogue");
                GetTree().CreateTimer(2.2, true, false, true).Timeout += () => GetTree().Quit();
            });
        At(21f, () => GetTree().Quit());
    }

    // Edge glow for full meters (red top edge, green top-left, both blended) and the Vessel's 99 cap while a heal is held.
    private void BuildGlowCheck()
    {
        void log(string label) => GD.Print($"[AutoPilot] {label}: vessel={world.Player.Vessel:0.#} star={world.Player.HealCharge:0.##}");
        At(1.0f, () => { world.Player.AddCorruption(Player.CORRUPTION_MAXIMUM); });
        At(2.6f, () => Capture("glow_rage"));
        At(2.7f, () => { world.Player.AddVessel(Player.VESSEL_MAXIMUM); log("first pour"); });
        At(4.2f, () => { log("star filled"); world.Player.AddVessel(Player.VESSEL_MAXIMUM); log("held cap"); });
        At(5.0f, () => Capture("glow_both"));
        At(5.1f, () => world.Player.AddCorruption(-Player.CORRUPTION_MAXIMUM));
        At(7.0f, () => Capture("glow_heal"));
        At(7.2f, () => GetTree().Quit());
    }

    // Visits each imported-pack region, then films the world map. Spots are world tiles.
    private void BuildRegionCheck()
    {
        (string name, int x, int y)[] spots =
        {
            ("frost_landing", 65, 41), ("frost_pantheon", 82, 12), ("frost_ravine", 45, 30), ("bones_watch", 141, 74), ("bones_ossuary", 146, 54),
            ("bones_pits", 150, 101), ("grove_landing", 62, 117), ("grove_court", 92, 146), ("grove_altar", 62, 151), ("verge_sealed", 15, 68),
            ("hollow_reach", 61, 94),
        };
        float time = 0.8f;
        foreach ((string name, int x, int y) in spots)
        {
            string label = name;
            int tx = x, ty = y;
            At(time, () => { TeleportWorld(tx, ty); world.Camera.SnapToPlayer(); });
            At(time + 0.5f, () => Capture($"region_{label}"));
            time += 0.9f;
        }
        At(time, () => { world.Map.RevealAll(); world.Map.Flush(); });
        At(time + 0.3f, () => Capture("region_minimap"));
        At(time + 0.5f, () => world.OpenWorldMap());
        At(time + 0.9f, () => Capture("region_worldmap"));
        At(time + 1.0f, () => world.TeleportViaMap(new Vector2I(146, 72)));
        At(time + 1.5f, () => Capture("region_teleported"));
        // The east Fracture and the sealed Azurean one, from just below. Spots are their world tiles.
        (string name, int x, int y)[] fractures = { ("fracture", 106, 76), ("fracture_sealed", 15, 63) };
        foreach ((string name, int x, int y) in fractures)
        {
            string label = name;
            Vector2 below = WorldGrid.TileCenter(x, y) + new Vector2(30f, 6f);
            At(time + 1.6f, () => { world.Player.PlaceAt(below); world.Camera.SnapToPlayer(); });
            At(time + 2.1f, () => Capture($"region_{label}"));
            time += 0.7f;
        }
        At(time + 1.6f, () => { TeleportWorld(58, 94); world.Camera.SnapToPlayer(); world.OpenBonfireForTest(); });
        At(time + 2.1f, () => Capture("region_bonfire"));
        At(time + 2.3f, () => GetTree().Quit());
    }

    // Walks Kio up to one of each new enemy type in turn and films the encounter: the sleeping or buried tell first, then the fight.
    private void BuildEnemyCheck()
    {
        (string name, Func<Entity, bool> match)[] subjects =
        {
            ("gnawer_nest", entity => entity is Gnawer { Nested: true }),
            ("gnawer", entity => entity is Gnawer { Nested: false, Bloated: false }),
            ("bloated", entity => entity is Gnawer { Bloated: true }),
            ("bonewalker", entity => entity is Bonewalker { Rimebound: false }),
            ("rimebound", entity => entity is Bonewalker { Rimebound: true }),
            ("rimeslime", entity => entity is Slime { Kind: SlimeKind.Rime }),
            ("bogslime", entity => entity is Slime { Kind: SlimeKind.Bog }),
            ("gloombat", entity => entity is GloomBat),
            ("shadeflit", entity => entity is Shadeflit),
        };
        float time = 0.8f;
        foreach ((string name, Func<Entity, bool> match) in subjects)
        {
            string label = name;
            Func<Entity, bool> test = match;
            At(time, () =>
            {
                // Enemies stream in around the screen, so the subject is found by its roster slot, then loads on arrival.
                EnemyRoster.Slot slot = world.Roster.Slots.FirstOrDefault(candidate => !candidate.Dead && IsKind(candidate.Kind, test));
                if (slot == null)
                {
                    GD.Print($"[AutoPilot] no {label} found");
                    return;
                }
                world.Player.RestoreAtRest();
                world.Player.PlaceAt(StandableNear(slot.Position, 110f));
                world.Camera.SnapToPlayer();
                world.Roster.Stream();
                GD.Print($"[AutoPilot] {label} at {WorldGrid.ToTile(slot.Position)}");
            });
            At(time + 0.4f, () => Capture($"enemy_{label}_0"));
            At(time + 0.5f, () =>
            {
                Entity subject = world.Entities.FirstOrDefault(test);
                if (subject != null)
                    world.Player.PlaceAt(StandableNear(subject.GlobalPosition, 50f));
            });
            for (int f = 1; f <= 4; f++)
            {
                int frame = f;
                At(time + 0.5f + 0.45f * f, () => Capture($"enemy_{label}_{frame}"));
            }
            time += 2.8f;
        }
        At(time, () => GetTree().Quit());
    }

    // The run starts in the Void (GameWorld reads StartsInVoid), tours every shard, photographs corrupted enemies up close,
    // then steps through the Fracture home; the reloaded Akasa captures where Kio came out and quits.
    public static bool StartsInVoid() => IsRequested() && OS.GetCmdlineUserArgs().Contains("--voidcheck");

    private void BuildVoidCheck()
    {
        if (!world.InVoid)
        {
            At(1.2f, () => { world.Camera.SnapToPlayer(); Capture("void_returned"); });
            At(1.4f, () => GD.Print($"[AutoPilot] back in Akasa at {WorldGrid.ToTile(world.Player.GlobalPosition)}"));
            At(1.5f, () => GetTree().Quit());
            return;
        }
        (string name, int x, int y)[] stops =
        {
            ("wound", 48, 66), ("west_road", 34, 66), ("frost", 21, 62), ("hanging_road", 22, 50), ("reach", 23, 34),
            ("convergence", 47, 31), ("gate_road", 48, 45), ("bones", 77, 33), ("thread", 78, 53), ("gilded", 78, 61),
        };
        float time = 1.5f;
        foreach ((string name, int x, int y) in stops)
        {
            string label = name;
            int tileX = x, tileY = y;
            At(time, () =>
            {
                world.Player.RestoreAtRest();
                TeleportWorld(tileX, tileY);
                world.Camera.SnapToPlayer();
                world.Roster.Stream();
            });
            At(time + 0.6f, () => Capture($"void_{label}"));
            time += 1.0f;
        }

        // Close-ups of corrupted enemies, one of each kind the Void holds.
        foreach (Func<Entity, bool> match in new Func<Entity, bool>[] { e => e is Brute, e => e is Bonewalker, e => e is Slime, e => e is Rotling, e => e is Hound })
        {
            Func<Entity, bool> test = match;
            int shot = (int)time;
            At(time, () =>
            {
                EnemyRoster.Slot slot = world.Roster.Slots.FirstOrDefault(candidate => !candidate.Dead && IsKind(candidate.Kind, test));
                if (slot == null)
                    return;
                world.Player.RestoreAtRest();
                world.Player.PlaceAt(StandableNear(slot.Position, 60f));
                world.Camera.SnapToPlayer();
                world.Roster.Stream();
            });
            At(time + 0.5f, () => Capture($"void_enemy_{shot}_a"));
            At(time + 0.9f, () => Capture($"void_enemy_{shot}_b"));
            time += 1.4f;
        }

        At(time, () =>
        {
            world.Player.RestoreAtRest();
            TeleportWorld(48, 64);
            world.Camera.SnapToPlayer();
        });
        At(time + 0.4f, () => GD.Print($"[AutoPilot] fracture prompt: {world.Interactables.OfType<Fracture>().FirstOrDefault()?.Prompt}"));
        At(time + 0.5f, () => Capture("void_fracture"));
        At(time + 0.6f, () => world.TravelThroughFracture(WorldGrid.TileCenter(48, 63)));
    }

    private static bool IsKind(PlacementKind kind, Func<Entity, bool> test)
    {
        Enemy probe = GameWorld.CreateEnemy(kind, false, 1, null);
        bool matches = test(probe);
        probe.Free();
        return matches;
    }

    private Vector2 StandableNear(Vector2 center, float distance)
    {
        foreach (Vector2 direction in new[] { Vector2.Left, Vector2.Right, Vector2.Down, Vector2.Up })
        {
            Vector2 spot = center + direction * distance;
            if (!world.Grid.IsPitAt(spot) && !world.Grid.Blocks(spot, 6f, world.Grid.SurfaceZAt(spot)))
                return spot;
        }
        return center;
    }

    // Frame strip of one attack per facing plus a dash, used to check body and weapon layers stay registered.
    private void BuildAnimationCheck()
    {
        Vector2[] facings = { Vector2.Right, Vector2.Down, Vector2.Left, Vector2.Up, new Vector2(1, 1), new Vector2(-1, -1) };
        float start = 1.2f;
        At(1.0f, () => Capture("anim_pre"));
        for (int i = 0; i < facings.Length; i++)
        {
            Vector2 facing = facings[i];
            int index = i;
            At(start, () => { world.Camera.SnapToPlayer(); Aim(facing); });
            At(start + 0.3f, () => Tap(GameInput.ATTACK));
            for (int f = 0; f < 8; f++)
            {
                int frame = f;
                At(start + 0.3f + 0.05f * f, () => Capture($"anim_{index}_{frame}"));
            }
            start += 1.2f;
        }
        At(start, () => { Aim(Vector2.Right); Hold(GameInput.MOVE_RIGHT, 0.4f); Tap(GameInput.DASH); });
        for (int f = 0; f < 5; f++)
        {
            int frame = f;
            At(start + 0.03f + 0.04f * f, () => Capture($"anim_dash_{frame}"));
        }
        At(start + 1.2f, () => Capture("anim_linger_1"));
        At(start + 2.6f, () => Capture("anim_linger_2"));
        At(start + 3.9f, () => Capture("anim_linger_gone"));
        At(start + 4.2f, () => GetTree().Quit());
    }

    // Plays the full five-strike string on the practice dummy. Run it under
    // --write-movie to get every frame.
    private void BuildComboCheck()
    {
        At(0.8f, () => { Teleport(14, 35); Aim(Vector2.Right); world.Camera.SnapToPlayer(); });
        for (int i = 0; i < 22; i++)
            At(1.2f + 0.11f * i, () => Tap(GameInput.ATTACK));
        At(5.6f, () => Teleport(13, 10));
        At(7f, () => GetTree().Quit());
    }

    // Two full strings on the practice dummy with a frame captured every 40 ms: the first shows the five base wakes and
    // the Arc overlay, the second lands on the dummy after its Tenacity has broken, for the staggered hit tier.
    // The schedule runs in insertion order, so each step both taps and captures.
    private void BuildStrikeCheck()
    {
        At(0.8f, () =>
        {
            Entity dummy = world.Entities.First(entity => entity is TargetDummy);
            world.Player.PlaceAt(dummy.GlobalPosition + new Vector2(-24f, 0f));
            Aim(Vector2.Right);
            world.Camera.SnapToPlayer();
        });
        for (int f = 0; f < 320; f++)
        {
            int frame = f;
            bool tap = f % 3 == 0;
            At(1.2f + 0.04f * f, () =>
            {
                if (tap)
                    Tap(GameInput.ATTACK);
                Capture($"strike_{frame:000}");
            });
        }
        At(14.2f, () => GetTree().Quit());
    }

    // Finds a pit with a cliff face hanging into it from the rim to the north and drops Kio into it beside the face,
    // the case where a falling body used to vanish behind the face.
    private void WalkIntoPitUnderCliff()
    {
        WorldGrid grid = world.Grid;
        for (int y = 1; y < grid.Height; y++)
        {
            for (int x = 1; x < grid.Width - 1; x++)
            {
                if (!grid.IsPit(x, y) || grid.HeightAt(x, y - 1) < 0 || y < 12)
                    continue;
                int side = grid.HeightAt(x - 1, y) == 0 ? -1 : grid.HeightAt(x + 1, y) == 0 ? 1 : 0;
                if (side == 0)
                    continue;
                TeleportWorld(x + side, y);
                world.Camera.SnapToPlayer();
                world.Player.PlaceAt(WorldGrid.TileCenter(x, y) + new Vector2(0, -12f));
                GD.Print($"[AutoPilot] pit fall test at {x},{y}");
                return;
            }
        }
    }

    // A jump into a full string on the dummy, logging Kio's height so the slowed air descent and the finisher's slam can be read off,
    // then a special to read the Stamina Star cooldown, then a Wasp's altitude over two cycles.
    private int hitSwings = -1;
    private int missSwings = -1;

    // Holds the string back until the jump has lifted Kio, then taps attack every third step.
    private void SwingOnceAirborne(ref int swings, bool allowed)
    {
        if (swings < 0 && world.Player.Z < 18f)
            return;
        swings++;
        if (allowed && swings % 3 == 0)
            Tap(GameInput.ATTACK);
    }

    private void BuildHoverCheck()
    {
        At(0.8f, () =>
        {
            Entity dummy = world.Entities.First(entity => entity is TargetDummy);
            world.Player.PlaceAt(dummy.GlobalPosition + new Vector2(-24f, 0f));
            Aim(Vector2.Right);
            world.Camera.SnapToPlayer();
        });
        for (int f = 0; f < 70; f++)
        {
            int frame = f;
            At(1.2f + 0.04f * f, () =>
            {
                if (frame == 0)
                    Tap(GameInput.JUMP);
                else
                    SwingOnceAirborne(ref hitSwings, frame < 56);
                GD.Print($"[Hover] t={frame * 0.04f:0.00} z={world.Player.Z:0.0} state={world.Player.StateMachine.Current} stamina={world.Player.Stamina:0}");
                if (frame % 6 == 0)
                    Capture($"hover_{frame:00}");
            });
        }
        At(4.2f, () => { Aim(Vector2.Left); Tap(GameInput.SPECIAL); });
        // The same jump string swung at nothing, to compare the faster fall without hits.
        At(6.4f, () =>
        {
            Entity dummy = world.Entities.First(entity => entity is TargetDummy);
            world.Player.PlaceAt(dummy.GlobalPosition + new Vector2(-160f, 0f));
            Aim(Vector2.Left);
        });
        for (int f = 0; f < 30; f++)
        {
            int frame = f;
            At(6.8f + 0.04f * f, () =>
            {
                if (frame == 0)
                    Tap(GameInput.JUMP);
                else
                    SwingOnceAirborne(ref missSwings, true);
                GD.Print($"[Miss] t={frame * 0.04f:0.00} z={world.Player.Z:0.0} state={world.Player.StateMachine.Current}");
            });
        }
        for (int f = 0; f < 10; f++)
            At(4.3f + 0.5f * f, () => GD.Print($"[Special] readiness={world.Player.SpecialReadiness:0.00} stamina={world.Player.Stamina:0}"));
        for (int f = 0; f < 30; f++)
            At(9.5f + 0.3f * f, () =>
            {
                Entity wasp = world.Entities.FirstOrDefault(entity => entity is Wasp);
                if (wasp != null)
                    GD.Print($"[Wasp] height={wasp.Z - wasp.SupportZ:0.0}");
            });
        At(18.6f, () => GetTree().Quit());
    }

    // Stands Kio among the corridor Rotlings and films the wind-up so the overhead dodge cue can be checked.
    private void BuildCueCheck()
    {
        At(0.8f, () => { Teleport(8, 23); world.Camera.SnapToPlayer(); });
        for (int f = 0; f < 45; f++)
        {
            int frame = f;
            At(1.2f + 0.07f * f, () => Capture($"cue_{frame:00}"));
        }
        At(4.6f, () => GetTree().Quit());
    }

    // Visits every section, then steps Kio north out of the Sanctuary into the Ash Corridor: the log shows the live
    // sections following Kio (the current one and its neighbours) and the frames show the camera following freely.
    private void BuildSectionCheck()
    {
        float time = 1.0f;
        for (int i = 0; i < world.Sections.Count; i++)
        {
            int section = i;
            At(time, () =>
            {
                Rect2I area = world.Sections.PlanOf(section).Area;
                Vector2I center = area.Position + area.Size / 2;
                Vector2I best = center;
                int bestDistance = int.MaxValue;
                for (int y = area.Position.Y; y < area.End.Y; y++)
                    for (int x = area.Position.X; x < area.End.X; x++)
                    {
                        int distance = Mathf.Abs(x - center.X) + Mathf.Abs(y - center.Y);
                        if (distance < bestDistance && world.Grid.HeightAt(x, y) == 0 && !world.Grid.Blocks(WorldGrid.TileCenter(x, y), 8f, 0f))
                        {
                            bestDistance = distance;
                            best = new Vector2I(x, y);
                        }
                    }
                world.Player.PlaceAt(WorldGrid.TileCenter(best.X, best.Y));
                world.Camera.SnapToPlayer();
            });
            At(time + 0.3f, () =>
            {
                SectionPlan plan = world.Sections.PlanOf(section);
                Capture($"tour_{section:00}_{plan.Region}_{plan.Name}".Replace(' ', '_').Replace("'", ""));
                GD.Print($"[AutoPilot] {plan.Region}/{plan.Name}: current {world.Sections.Current}, active nodes {CountActive()}, loaded enemies {world.Roster.Slots.Count(slot => slot.Live != null)}");
            });
            time += 0.4f;
        }

        At(time, () =>
        {
            Teleport(8, 33);
            world.Camera.SnapToPlayer();
        });
        for (int f = 0; f < 14; f++)
        {
            int frame = f;
            At(time + 0.4f + 0.12f * f, () =>
            {
                world.Player.PlaceAt(world.Player.GlobalPosition + new Vector2(0f, -24f));
                Capture($"seam_{frame:00}");
                GD.Print($"[AutoPilot] seam frame {frame}: kio {WorldGrid.ToTile(world.Player.GlobalPosition)}, section {world.Sections.PlanOf(world.Sections.Current).Name}, live sections {CountActive()}");
            });
        }
        At(time + 2.6f, () => GetTree().Quit());
    }

    // Glides Kio at walking pace through every section of the Hollow Reach and Glaciemera in turn, crossing a seam every few
    // seconds, and reports the frames that took far longer than usual together with where Kio was.
    private void BuildSeamCheck()
    {
        var route = new List<Vector2>();
        for (int i = 0; i < world.Sections.Count; i++)
        {
            SectionPlan plan = world.Sections.PlanOf(i);
            if (plan.Region is "Hollow Reach" or "Glaciemera")
                route.Add(world.Sections.AreaOf(i).GetCenter());
        }
        // The control run: Kio barely moves inside the Sanctuary, so any hitch it shows is not from streaming.
        if (OS.GetCmdlineUserArgs().Contains("--idle"))
        {
            seamSpeed = 0.5f;
            route = new List<Vector2> { route[0], route[0] + new Vector2(45f, 0f) };
        }
        At(1.0f, () => { world.Player.PlaceAt(route[0]); world.Camera.SnapToPlayer(); });
        At(2.5f, () => { seamRoute = route; seamLeg = 1; seamFrames.Clear(); WorldLight.ProbeUsec = 0; WorldLight.ProbeCalls = 0; });
    }

    private float seamSpeed = 140f;
    private List<Vector2> seamRoute;
    private int seamLeg;
    private readonly List<(float ms, string where)> seamFrames = new();
    private int seamGc;
    private int seamGcStart = -1;
    private long seamBytes;
    private long seamAllocated;
    private float seamProcess;
    private ulong seamLastTicks;

    private void StepSeamWalk(float delta)
    {
        if (seamRoute == null)
            return;
        long bytes = System.GC.GetTotalAllocatedBytes(false);
        if (seamBytes > 0)
            seamAllocated += bytes - seamBytes;
        int gc = System.GC.CollectionCount(0);
        string note = $"{world.Sections.PlanOf(world.Sections.Current).Name} | process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000f:0.0} physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000f:0.0} | gc {gc - seamGc} | section steps {world.Sections.StepsLastFrame} | roster {world.Roster.ChangesLastFrame} | enemies {world.Roster.Slots.Count(slot => slot.Live != null)} | draw {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} | objects {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)}";
        seamGc = gc;
        if (seamGcStart < 0)
            seamGcStart = gc;
        seamFrames.Add((delta * 1000f, note));
        seamProcess += (float)Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000f;
        seamBytes = System.GC.GetTotalAllocatedBytes(false);
        Vector2 target = seamRoute[seamLeg];
        Vector2 position = world.Player.GlobalPosition.MoveToward(target, seamSpeed * Mathf.Min(delta, 1f / 30f));
        world.Player.PlaceAt(position);
        if (position.DistanceTo(target) > 1f)
            return;
        if (++seamLeg < seamRoute.Count)
            return;
        var sorted = seamFrames.Select(frame => frame.ms).OrderBy(ms => ms).ToList();
        float median = sorted[sorted.Count / 2];
        float p99 = sorted[(int)(sorted.Count * 0.99f)];
        int spikes = seamFrames.Count(frame => frame.ms > median * 1.8f);
        int hitches = seamFrames.Count(frame => frame.ms > 20f);
        GD.Print($"[AutoPilot] seam walk: {seamFrames.Count} frames, median {median:0.00} ms, p99 {p99:0.00} ms, worst {sorted[^1]:0.00} ms, spikes over 1.8x median {spikes}, hitches over 20 ms {hitches}");
        GD.Print($"[AutoPilot] seam walk gc: {System.GC.CollectionCount(0) - seamGcStart} gen0, {System.GC.CollectionCount(1)} gen1, {System.GC.CollectionCount(2)} gen2, {seamAllocated / seamFrames.Count / 1024f:0.0} KB allocated per frame outside the probe");
        GD.Print($"[AutoPilot] probe: WorldLight draw {WorldLight.ProbeUsec / (float)seamFrames.Count:0.0} us per frame, {WorldLight.ProbeCalls / (float)seamFrames.Count:0.0} calls per frame; process avg {seamProcess / seamFrames.Count:0.00} ms");
        // A frame's own work shows in the next frame's time, so a hitch is charged to whatever ran just before it.
        int afterLoad = 0, afterSteps = 0;
        for (int i = 1; i < seamFrames.Count; i++)
        {
            if (seamFrames[i].ms <= 20f)
                continue;
            if (!seamFrames[i - 1].where.Contains("roster 0 ") || !seamFrames[i].where.Contains("roster 0 "))
                afterLoad++;
            if (!seamFrames[i - 1].where.Contains("section steps 0 ") || !seamFrames[i].where.Contains("section steps 0 "))
                afterSteps++;
        }
        GD.Print($"[AutoPilot] hitches next to an enemy load or unload {afterLoad}, next to section work {afterSteps}");
        foreach ((float ms, string where) in seamFrames.OrderByDescending(frame => frame.ms).Take(14))
            GD.Print($"[AutoPilot]   {ms:0.00} ms in {where}");
        seamRoute = null;
        GetTree().Quit();
    }

    private int CountActive()
    {
        int count = 0;
        for (int i = 0; i < world.Sections.Count; i++)
            if (world.Sections.IsLive(i))
                count++;
        return count;
    }

    // Wounds a Rotling, then takes Kio out of its sight: it should hunt, search the last seen spot, give up,
    // walk home and keep both its wounds and its health bar. Then it streams out and back in, still wounded.
    private void BuildChaseCheck()
    {
        EnemyRoster.Slot slot = null;
        Enemy Subject() => slot?.Live;
        void Report(string moment)
        {
            Enemy enemy = Subject();
            if (enemy == null)
            {
                GD.Print($"[AutoPilot] {moment}: unloaded, slot health {slot?.Health:0.0}, at {slot?.Position}");
                return;
            }
            GD.Print($"[AutoPilot] {moment}: state {enemy.StateMachine.Current}, aggro {enemy.IsAggroed}, health {enemy.Health:0.0}/{enemy.MaxHealth:0.0}, " +
                $"from home {enemy.GlobalPosition.DistanceTo(enemy.HomeAnchor):0}, to kio {enemy.GlobalPosition.DistanceTo(world.Player.GlobalPosition):0}, bar {enemy.GetNode<CanvasItem>("Body/ResourceBar").Visible}");
        }
        At(0.8f, () =>
        {
            slot = world.Roster.Slots.First(candidate => candidate.Kind == PlacementKind.Rotling);
            world.Player.PlaceAt(StandableNear(slot.Position, 70f));
            world.Camera.SnapToPlayer();
            world.Roster.Stream();
        });
        At(1.2f, () =>
        {
            Enemy enemy = Subject();
            enemy.ReceiveHit(new HitInfo { Damage = enemy.MaxHealth * 0.4f, Direction = Vector2.Right, Source = HitSource.Player });
            Report("wounded");
        });
        At(1.4f, () => Capture("chase_0_wounded"));
        // Step back a little: it should follow and keep the hunt going.
        At(2.0f, () => world.Player.PlaceAt(StandableNear(Subject().GlobalPosition, 130f)));
        for (int i = 0; i < 4; i++)
        {
            int step = i;
            At(2.5f + step * 0.5f, () => Report($"hunting {step}"));
        }
        At(4.6f, () => Capture("chase_1_hunting"));
        // Vanish: Kio steps beyond its tracking range. It searches, then turns home after the sight timer runs out.
        At(4.7f, () => world.Player.PlaceAt(StandableNear(Subject().GlobalPosition, 420f)));
        for (int i = 0; i < 10; i++)
        {
            int step = i;
            At(5.2f + step * 0.8f, () => Report($"after vanish {step * 0.8f + 0.5f:0.0}s"));
        }
        At(13.4f, () => { world.Player.PlaceAt(StandableNear(slot.Position, 90f)); world.Camera.SnapToPlayer(); world.Roster.Stream(); });
        At(13.7f, () => { Report("back"); Capture("chase_2_back"); });
        // Out of range entirely: it streams out, then back in with the same wounds.
        At(14.0f, () => { world.Player.PlaceAt(StandableNear(slot.Spawn + new Vector2(1400f, 0f), 0f)); world.Camera.SnapToPlayer(); world.Roster.Stream(); Report("away"); });
        At(14.5f, () => { world.Player.PlaceAt(StandableNear(slot.Position, 90f)); world.Camera.SnapToPlayer(); world.Roster.Stream(); });
        At(14.8f, () => { Report("reloaded"); Capture("chase_3_reloaded"); });
        At(15.2f, () => GetTree().Quit());
    }

    // Frame time sampled at the start and in the heart of each continent, with the counts that drive it.
    private void BuildPerfCheck()
    {
        (string name, Vector2I tile)[] spots =
        {
            ("start", WorldGrid.ToTile(world.Player.GlobalPosition)),
            ("frost", new Vector2I(80, 25)),
            ("bones", new Vector2I(152, 80)),
            ("grove", new Vector2I(80, 134)),
            ("verge", new Vector2I(22, 80)),
        };
        float at = 1.5f;
        foreach ((string name, Vector2I tile) in spots)
        {
            string label = name;
            Vector2I spot = tile;
            At(at, () =>
            {
                world.Player.PlaceAt(StandableNear(WorldGrid.TileCenter(spot.X, spot.Y), 200f));
                world.Camera.SnapToPlayer();
            });
            At(at + 1.5f, () => { perfFrames = 0; perfTime = 0f; perfWorst = 0f; });
            At(at + 4.5f, () =>
            {
                GD.Print($"[AutoPilot] perf {label}: avg {perfTime / Mathf.Max(1, perfFrames) * 1000f:0.00} ms, worst {perfWorst * 1000f:0.00} ms, " +
                    $"process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000f:0.00} ms, nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}, " +
                    $"draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, entities {world.Entities.Count}");
                Capture($"perf_{label}");
            });
            at += 5f;
        }
        At(at, () => GetTree().Quit());
    }

    private int perfFrames;
    private float perfTime;
    private float perfWorst;

    public override void _Process(double delta)
    {
        perfFrames++;
        perfTime += (float)delta;
        perfWorst = Mathf.Max(perfWorst, (float)delta);
        ulong seamNow = Time.GetTicksUsec();
        float realFrame = seamLastTicks == 0 ? (float)delta : (seamNow - seamLastTicks) / 1_000_000f;
        seamLastTicks = seamNow;
        StepSeamWalk(realFrame);
        ulong now = Time.GetTicksUsec();
        // Under --write-movie the engine renders a fixed step per frame however long it takes, so the tour follows frames.
        time += IsRecordingMovie ? 1f / MOVIE_FPS : Mathf.Min((now - lastTicks) / 1_000_000f, 0.1f);
        lastTicks = now;
        while (nextStep < schedule.Count && schedule[nextStep].time <= time)
        {
            schedule[nextStep].action();
            nextStep++;
        }
    }
}
