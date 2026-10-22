using Godot;

namespace Anomaly;

public enum WindowModeSetting
{
    Windowed,
    Borderless,
    Fullscreen,
}

// Player-facing options, persisted to user://settings.cfg apart from the save game so erasing progress keeps them.
public static class GameSettings
{
    public const string MUSIC_BUS = "Music";
    public const string SFX_BUS = "SFX";
    public const string UI_BUS = "UI";
    public static readonly int[] FPS_CAPS = { 30, 60, 120, 144, 0 };
    public static readonly int[] WINDOW_SCALES = { 2, 3, 4 };
    private const string PATH = "user://settings.cfg";

    public static float MasterVolume = 0.8f;
    public static float MusicVolume = 0.7f;
    public static float SfxVolume = 0.8f;
    public static float UiVolume = 0.6f;
    public static bool MuteWhenUnfocused = true;

    public static WindowModeSetting WindowMode = WindowModeSetting.Borderless;
    public static int WindowScaleIndex = 2;
    public static bool VSync = true;
    public static int FpsCapIndex = 3;
    public static float Brightness = 1f;
    public static float ScreenShake = 1f;
    public static float Flashes = 1f;
    public static bool ChromaticAberration = true;
    public static bool FilmGrain = true;
    public static bool Bloom = true;

    public static bool DamageNumbers = true;
    public static float AimAssist = 1f;
    public static bool ControlHints = true;

    private static bool loaded;

    public static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        EnsureBuses();
        var config = new ConfigFile();
        if (config.Load(PATH) == Error.Ok)
        {
            MasterVolume = (float)config.GetValue("audio", "master", MasterVolume);
            MusicVolume = (float)config.GetValue("audio", "music", MusicVolume);
            SfxVolume = (float)config.GetValue("audio", "sfx", SfxVolume);
            UiVolume = (float)config.GetValue("audio", "ui", UiVolume);
            MuteWhenUnfocused = (bool)config.GetValue("audio", "mute_unfocused", MuteWhenUnfocused);
            WindowMode = (WindowModeSetting)(int)config.GetValue("video", "window_mode", (int)WindowMode);
            WindowScaleIndex = (int)config.GetValue("video", "window_scale", WindowScaleIndex);
            VSync = (bool)config.GetValue("video", "vsync", VSync);
            FpsCapIndex = (int)config.GetValue("video", "fps_cap", FpsCapIndex);
            Brightness = (float)config.GetValue("video", "brightness", Brightness);
            ScreenShake = (float)config.GetValue("video", "screen_shake", ScreenShake);
            Flashes = (float)config.GetValue("video", "flashes", Flashes);
            ChromaticAberration = (bool)config.GetValue("video", "aberration", ChromaticAberration);
            FilmGrain = (bool)config.GetValue("video", "film_grain", FilmGrain);
            Bloom = (bool)config.GetValue("video", "bloom", Bloom);
            DamageNumbers = (bool)config.GetValue("gameplay", "damage_numbers", DamageNumbers);
            AimAssist = (float)config.GetValue("gameplay", "aim_assist", AimAssist);
            ControlHints = (bool)config.GetValue("gameplay", "control_hints", ControlHints);
            foreach (string action in GameInput.REBINDABLE)
                if (config.HasSectionKey("bindings", action))
                    GameInput.ApplyBindingCodes(action, (string[])config.GetValue("bindings", action));
        }
        WindowScaleIndex = Mathf.Clamp(WindowScaleIndex, 0, WINDOW_SCALES.Length - 1);
        FpsCapIndex = Mathf.Clamp(FpsCapIndex, 0, FPS_CAPS.Length - 1);
        ApplyAudio();
        ApplyVideo();
    }

    public static void Save()
    {
        var config = new ConfigFile();
        config.SetValue("audio", "master", MasterVolume);
        config.SetValue("audio", "music", MusicVolume);
        config.SetValue("audio", "sfx", SfxVolume);
        config.SetValue("audio", "ui", UiVolume);
        config.SetValue("audio", "mute_unfocused", MuteWhenUnfocused);
        config.SetValue("video", "window_mode", (int)WindowMode);
        config.SetValue("video", "window_scale", WindowScaleIndex);
        config.SetValue("video", "vsync", VSync);
        config.SetValue("video", "fps_cap", FpsCapIndex);
        config.SetValue("video", "brightness", Brightness);
        config.SetValue("video", "screen_shake", ScreenShake);
        config.SetValue("video", "flashes", Flashes);
        config.SetValue("video", "aberration", ChromaticAberration);
        config.SetValue("video", "film_grain", FilmGrain);
        config.SetValue("video", "bloom", Bloom);
        config.SetValue("gameplay", "damage_numbers", DamageNumbers);
        config.SetValue("gameplay", "aim_assist", AimAssist);
        config.SetValue("gameplay", "control_hints", ControlHints);
        foreach (string action in GameInput.REBINDABLE)
            config.SetValue("bindings", action, GameInput.BindingCodes(action));
        config.Save(PATH);
    }

    // Music and UI go straight to Master. SFX passes a short room reverb and a soft compressor first, which gives
    // impacts a body and tail instead of dry clicks.
    private static void EnsureBuses()
    {
        AddBus(MUSIC_BUS);
        AddBus(UI_BUS);
        if (AddBus(SFX_BUS))
        {
            int sfx = AudioServer.GetBusIndex(SFX_BUS);
            AudioServer.AddBusEffect(sfx, new AudioEffectReverb { RoomSize = 0.42f, Damping = 0.7f, Spread = 0.8f, Wet = 0.14f, Dry = 1f, Hipass = 0.15f });
            AudioServer.AddBusEffect(sfx, new AudioEffectCompressor { Threshold = -14f, Ratio = 3f, AttackUs = 2000f, ReleaseMs = 160f });
        }
        if (AudioServer.GetBusEffectCount(0) == 0)
            AudioServer.AddBusEffect(0, new AudioEffectHardLimiter { CeilingDb = -0.6f });
    }

    private static bool AddBus(string name)
    {
        if (AudioServer.GetBusIndex(name) >= 0)
            return false;
        AudioServer.AddBus();
        int index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, "Master");
        return true;
    }

    public static void ApplyAudio()
    {
        SetVolume("Master", MasterVolume);
        SetVolume(MUSIC_BUS, MusicVolume);
        SetVolume(SFX_BUS, SfxVolume);
        SetVolume(UI_BUS, UiVolume);
    }

    private static void SetVolume(string bus, float linear)
    {
        int index = AudioServer.GetBusIndex(bus);
        if (index < 0)
            return;
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(linear, 0.0001f)));
        AudioServer.SetBusMute(index, linear <= 0.001f);
    }

    public static void SetFocusMute(bool focused)
    {
        AudioServer.SetBusMute(0, MuteWhenUnfocused && !focused || MasterVolume <= 0.001f);
    }

    public static void ApplyVideo()
    {
        if (DisplayServer.GetName() == "headless")
            return;
        // Autopilot test runs stay windowed so tools/autopilot.ps1 can park them on monitor 2.
        switch (AutoPilot.IsRequested() ? WindowModeSetting.Windowed : WindowMode)
        {
            case WindowModeSetting.Fullscreen:
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
            case WindowModeSetting.Borderless:
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            default:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                Vector2I size = (Vector2I)(ViewScale.DESIGN_SIZE * WINDOW_SCALES[WindowScaleIndex]);
                int current = DisplayServer.WindowGetCurrentScreen();
                Vector2I origin = DisplayServer.ScreenGetPosition(current) + (DisplayServer.ScreenGetSize(current) - size) / 2;
                DisplayServer.WindowSetSize(size);
                DisplayServer.WindowSetPosition(origin);
                break;
        }
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = FPS_CAPS[FpsCapIndex];
    }

    public static string FpsCapLabel => FPS_CAPS[FpsCapIndex] == 0 ? "Unlimited" : FPS_CAPS[FpsCapIndex].ToString();

    public static string WindowScaleLabel
    {
        get
        {
            Vector2I size = (Vector2I)(ViewScale.DESIGN_SIZE * WINDOW_SCALES[WindowScaleIndex]);
            return $"{size.X} x {size.Y}";
        }
    }

    public static void ResetToDefaults()
    {
        MasterVolume = 0.8f;
        MusicVolume = 0.7f;
        SfxVolume = 0.8f;
        UiVolume = 0.6f;
        MuteWhenUnfocused = true;
        WindowMode = WindowModeSetting.Borderless;
        WindowScaleIndex = 2;
        VSync = true;
        FpsCapIndex = 3;
        Brightness = 1f;
        ScreenShake = 1f;
        Flashes = 1f;
        ChromaticAberration = true;
        FilmGrain = true;
        Bloom = true;
        DamageNumbers = true;
        AimAssist = 1f;
        ControlHints = true;
        ApplyAudio();
        ApplyVideo();
    }
}
