using System.Collections.Generic;
using Godot;

namespace Anomaly;

public partial class SoundBank : Node
{
    private const int POOL_SIZE = 28;
    private const string AUDIO_DIRECTORY = "res://#Assets/Audio/";

    private readonly Dictionary<string, AudioStream> streams = new();
    private readonly List<AudioStreamPlayer> pool = new();
    private readonly Dictionary<string, ulong> lastPlayed = new();
    private int nextPlayer;
    private AudioStreamPlayer ambiencePlayer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var player = new AudioStreamPlayer { Bus = GameSettings.SFX_BUS };
            AddChild(player);
            pool.Add(player);
        }
        ambiencePlayer = new AudioStreamPlayer { VolumeDb = -9f, Bus = GameSettings.MUSIC_BUS };
        AddChild(ambiencePlayer);
        Sfx.Bind(this);
    }

    public override void _ExitTree() => Sfx.Bind(null);

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
            GameSettings.SetFocusMute(false);
        else if (what == NotificationApplicationFocusIn)
            GameSettings.SetFocusMute(true);
    }

    public void Play(string name, float volumeDb, float pitch, float pitchVariance)
    {
        AudioStream stream = Load(name);
        if (stream == null)
            return;

        ulong now = Time.GetTicksMsec();
        if (lastPlayed.TryGetValue(name, out ulong previous) && now - previous < 30)
            return;
        lastPlayed[name] = now;

        AudioStreamPlayer player = pool[nextPlayer];
        nextPlayer = (nextPlayer + 1) % pool.Count;
        player.Stream = stream;
        player.Bus = name.StartsWith("ui_") ? GameSettings.UI_BUS : GameSettings.SFX_BUS;
        player.VolumeDb = volumeDb;
        player.PitchScale = Mathf.Max(0.05f, pitch + (float)GD.RandRange(-pitchVariance, pitchVariance));
        player.Play();
    }

    public void PlayAmbience(string name)
    {
        if (Load(name) is not AudioStreamWav wav)
            return;
        wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        wav.LoopBegin = 0;
        wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
        ambiencePlayer.Stream = wav;
        ambiencePlayer.Play();
    }

    private AudioStream Load(string name)
    {
        if (streams.TryGetValue(name, out AudioStream cached))
            return cached;
        string path = AUDIO_DIRECTORY + name + ".wav";
        AudioStream stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
        streams[name] = stream;
        return stream;
    }
}

public static class Sfx
{
    private static SoundBank bank;

    public static void Bind(SoundBank soundBank) => bank = soundBank;

    public static void Play(string name, float volumeDb = 0f, float pitch = 1f, float pitchVariance = 0.07f)
    {
        bank?.Play(name, volumeDb, pitch, pitchVariance);
    }
}
