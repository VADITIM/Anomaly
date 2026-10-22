using System;
using Godot;

namespace Anomaly;

// Settings opened from the pause menu: Audio, Video, Gameplay and Controls pages. Every change applies at once and
// is written to user://settings.cfg when the menu closes.
public partial class OptionsMenu : MenuPanel
{
    private enum Page
    {
        Root,
        Audio,
        Video,
        Effects,
        Gameplay,
        Controls,
    }

    private const float VOLUME_STEP = 0.1f;

    protected override float RowHeight => 11f;

    private Page page = Page.Root;
    private Action onClosed;
    private string listeningAction;
    private OptionRow listeningRow;
    private float listenGuard;

    public void Setup(GameWorld gameWorld, Action closed)
    {
        Setup(gameWorld);
        onClosed = closed;
    }

    public override void _Ready()
    {
        base._Ready();
        Layer = 31;
        SetPanelWidth(236f);
    }

    protected override void OnOpening() => page = Page.Root;

    protected override void BuildOptions()
    {
        Title.Text = page == Page.Root ? "OPTIONS" : page.ToString().ToUpper();
        Subtitle.Text = page switch
        {
            Page.Controls => "Select an action, then press a key or mouse button.",
            Page.Root => "Changes apply immediately.",
            _ => "Left / right to adjust.",
        };
        switch (page)
        {
            case Page.Audio:
                BuildAudio();
                break;
            case Page.Video:
                BuildVideo();
                break;
            case Page.Effects:
                BuildEffects();
                break;
            case Page.Gameplay:
                BuildGameplay();
                break;
            case Page.Controls:
                BuildControls();
                break;
            default:
                AddOption("Audio", () => Open(Page.Audio), "Master, ambience, effects and interface volume.");
                AddOption("Video", () => Open(Page.Video), "Window, frame rate and brightness.");
                AddOption("Effects", () => Open(Page.Effects), "Screen shake, flashes, aberration, grain and bloom.");
                AddOption("Gameplay", () => Open(Page.Gameplay), "Damage numbers and aim assist.");
                AddOption("Controls", () => Open(Page.Controls), "Rebind keyboard and mouse. Gamepad layout is fixed.");
                AddOption("Back", Close, "Return to the pause menu.");
                break;
        }
    }

    private void BuildAudio()
    {
        Volume("Master", () => GameSettings.MasterVolume, value => GameSettings.MasterVolume = value, "Overall loudness.");
        Volume("Ambience", () => GameSettings.MusicVolume, value => GameSettings.MusicVolume = value, "The drone and wind of the region.");
        Volume("Effects", () => GameSettings.SfxVolume, value => GameSettings.SfxVolume = value, "Combat, footsteps, creatures and the world.");
        Volume("Interface", () => GameSettings.UiVolume, value => GameSettings.UiVolume = value, "Menu movement and confirmation.");
        Toggle("Mute when unfocused", () => GameSettings.MuteWhenUnfocused, value => GameSettings.MuteWhenUnfocused = value, "Silence the game while its window is in the background.");
        AddOption("Back", Back, "Return to options.");
    }

    private void BuildVideo()
    {
        Row("Window", () => GameSettings.WindowMode switch
            {
                WindowModeSetting.Borderless => "Borderless",
                WindowModeSetting.Fullscreen => "Fullscreen",
                _ => "Windowed",
            },
            direction =>
            {
                GameSettings.WindowMode = (WindowModeSetting)Wrap((int)GameSettings.WindowMode + direction, 3);
                GameSettings.ApplyVideo();
            }, "Borderless fills the screen without changing its mode. Fullscreen takes exclusive control.");
        Row("Window size", () => GameSettings.WindowScaleLabel, direction =>
            {
                GameSettings.WindowScaleIndex = Wrap(GameSettings.WindowScaleIndex + direction, GameSettings.WINDOW_SCALES.Length);
                GameSettings.ApplyVideo();
            }, "Windowed size, in whole multiples of the 480 x 270 canvas so pixels stay crisp.");
        Toggle("VSync", () => GameSettings.VSync, value =>
            {
                GameSettings.VSync = value;
                GameSettings.ApplyVideo();
            }, "Sync frames to the display to avoid tearing.");
        Row("Frame cap", () => GameSettings.FpsCapLabel, direction =>
            {
                GameSettings.FpsCapIndex = Wrap(GameSettings.FpsCapIndex + direction, GameSettings.FPS_CAPS.Length);
                GameSettings.ApplyVideo();
            }, "Upper limit on frames per second.");
        Slider("Brightness", () => GameSettings.Brightness, value => GameSettings.Brightness = value, 0.6f, 1.4f, 0.05f, "Lift or deepen the whole image. Default 100%.");
        AddOption("Back", Back, "Return to options.");
    }

    private void BuildEffects()
    {
        Slider("Screen shake", () => GameSettings.ScreenShake, value => GameSettings.ScreenShake = value, 0f, 1.5f, 0.25f, "Camera shake and zoom punch on impacts.");
        Slider("Flashes", () => GameSettings.Flashes, value => GameSettings.Flashes = value, 0f, 1f, 0.25f, "Full-screen colour flashes on hits, deaths and Void events.");
        Toggle("Chromatic aberration", () => GameSettings.ChromaticAberration, value => GameSettings.ChromaticAberration = value, "Colour fringing when Kio is hurt or time freezes.");
        Toggle("Film grain", () => GameSettings.FilmGrain, value => GameSettings.FilmGrain = value, "Fine moving grain over the image.");
        Toggle("Bloom", () => GameSettings.Bloom, value => GameSettings.Bloom = value, "Soft glow around lights, souls and Void energy.");
        AddOption("Back", Back, "Return to options.");
    }

    private void BuildGameplay()
    {
        Toggle("Damage numbers", () => GameSettings.DamageNumbers, value => GameSettings.DamageNumbers = value, "Show damage dealt and taken as floating numbers.");
        Slider("Aim assist", () => GameSettings.AimAssist, value => GameSettings.AimAssist = value, 0f, 1f, 0.25f, "How strongly strikes bend toward a nearby enemy.");
        AddOption("Restore defaults", RestoreDefaults, "Reset every audio, video and gameplay option. Bindings are kept.");
        AddOption("Back", Back, "Return to options.");
    }

    private void BuildControls()
    {
        foreach (string action in GameInput.REBINDABLE)
        {
            string bound = action;
            OptionRow row = null;
            row = OptionRow.Create(GameInput.DisplayName(action), () => listeningAction == bound ? "press a key…" : GameInput.Describe(bound), _ => Listen(bound, row));
            AddRow(row, $"{GameInput.DisplayName(action)}. Confirm, then press the new key or mouse button. Esc cancels.");
        }
        AddOption("Reset bindings", ResetBindings, "Restore the default keyboard and mouse layout.");
        AddOption("Back", Back, "Return to options.");
    }

    private void Volume(string label, Func<float> get, Action<float> set, string description)
    {
        Slider(label, get, value =>
        {
            set(value);
            GameSettings.ApplyAudio();
        }, 0f, 1f, VOLUME_STEP, description);
    }

    private void Slider(string label, Func<float> get, Action<float> set, float min, float max, float step, string description)
    {
        OptionRow row = OptionRow.Create(label, () => $"{Mathf.RoundToInt(get() * 100f)}%",
            direction => set(Mathf.Snapped(Mathf.Clamp(get() + direction * step, min, max), 0.001f)),
            () => (get() - min) / (max - min));
        AddRow(row, description);
    }

    private void Toggle(string label, Func<bool> get, Action<bool> set, string description)
    {
        AddRow(OptionRow.Create(label, () => get() ? "On" : "Off", _ => set(!get())), description);
    }

    private void Row(string label, Func<string> value, Action<int> change, string description)
    {
        AddRow(OptionRow.Create(label, value, change), description);
    }

    private static int Wrap(int value, int count) => (value % count + count) % count;

    private void Open(Page next)
    {
        page = next;
        Rebuild(0);
    }

    private void Back()
    {
        int focus = page switch
        {
            Page.Video => 1,
            Page.Effects => 2,
            Page.Gameplay => 3,
            Page.Controls => 4,
            _ => 0,
        };
        CancelListening();
        page = Page.Root;
        Rebuild(focus);
    }

    private void RestoreDefaults()
    {
        GameSettings.ResetToDefaults();
        Rebuild(FocusedIndex());
    }

    private void ResetBindings()
    {
        GameInput.ResetBindings();
        Rebuild(FocusedIndex());
    }

    private void Listen(string action, OptionRow row)
    {
        listeningAction = action;
        listeningRow = row;
        listenGuard = 0.12f;
        row.Refresh();
    }

    private void CancelListening()
    {
        OptionRow row = listeningRow;
        listeningAction = null;
        listeningRow = null;
        row?.Refresh();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (listenGuard > 0f)
            listenGuard -= Motion.Unscaled(delta);
    }

    // While listening, the next key or mouse button press becomes the binding before the GUI can act on it.
    public override void _Input(InputEvent inputEvent)
    {
        if (!Visible || listeningAction == null || listenGuard > 0f || !inputEvent.IsPressed() || inputEvent.IsEcho())
            return;
        if (inputEvent is InputEventKey { PhysicalKeycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            CancelListening();
            return;
        }
        if (!GameInput.IsBindable(inputEvent) || inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown })
            return;
        GetViewport().SetInputAsHandled();
        GameInput.Rebind(listeningAction, inputEvent);
        Sfx.Play("ui_confirm", -4f);
        listeningAction = null;
        listeningRow = null;
        Rebuild(FocusedIndex());
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible)
            return;
        if (inputEvent.IsActionPressed(GameInput.PAUSE) || inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            if (page == Page.Root)
                Close();
            else
                Back();
        }
    }

    public override void Close()
    {
        CancelListening();
        GameSettings.Save();
        HideKeepingPause();
        Sfx.Play("ui_move", -6f, 0.7f);
        onClosed?.Invoke();
    }
}
