using System;
using Godot;

namespace Anomaly;

// Maldor Arakhan standing at his anvil: an idle strip that breaks into work and bored clips, and a greeting when Kio nears.
// He does not tick himself; the Forge calls Animate so he keeps moving while its menu pauses the tree.
public partial class Maldor : Node2D
{
    private const string FOLDER = "Entities/Maldor/";
    private const float SCALE = 1.5f;

    private readonly record struct Clip(string File, int Frames, float Fps, bool Loop);

    private static readonly Clip Idle = new("idle", 4, 4f, true);
    private static readonly Clip Greeting = new("greeting", 4, 6f, false);
    private static readonly Clip[] Works = { new("work_01", 5, 6f, false), new("work_02", 6, 6f, false) };
    private static readonly Clip[] Boreds = { new("bored_01", 10, 6f, false), new("bored_02", 10, 6f, false) };

    private Sprite2D sprite;
    private Clip clip = Idle;
    private float frameTime;
    private int frame;
    private float nextAction = 3f;

    public bool IsWorking => clip.File.StartsWith("work");

    // Raised on each hammer strike so the Forge can throw sparks off the anvil.
    public event Action Struck;

    public override void _Ready()
    {
        sprite = new Sprite2D
        {
            RegionEnabled = true,
            Centered = false,
            Offset = new Vector2(-16, -31),
            Scale = Vector2.One * SCALE,
        };
        AddChild(sprite);
        Play(Idle);
    }

    public void Greet()
    {
        if (clip.File != Greeting.File)
            Play(Greeting);
    }

    private void Play(Clip next)
    {
        clip = next;
        frame = 0;
        frameTime = 0f;
        sprite.Texture = Art.Texture(FOLDER + next.File + ".png");
        sprite.RegionRect = new Rect2(0, 0, 32, 32);
    }

    public void Animate(float dt)
    {
        frameTime += dt;
        float step = 1f / clip.Fps;
        while (frameTime >= step)
        {
            frameTime -= step;
            frame++;
            if (frame >= clip.Frames)
            {
                if (clip.Loop)
                    frame = 0;
                else
                {
                    Play(Idle);
                    nextAction = (float)GD.RandRange(3.0, 7.0);
                    break;
                }
            }
            sprite.RegionRect = new Rect2(frame * 32, 0, 32, 32);
            if (IsWorking && frame % 2 == 1)
                Struck?.Invoke();
        }

        if (clip.Loop)
        {
            nextAction -= dt;
            if (nextAction <= 0f)
                Play(GD.Randf() < 0.65f ? Works[GD.Randi() % Works.Length] : Boreds[GD.Randi() % Boreds.Length]);
        }
    }
}
