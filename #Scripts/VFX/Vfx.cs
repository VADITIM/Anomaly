using Godot;

namespace Anomaly;

public static class Vfx
{
    private static CanvasItemMaterial additive;

    public static CanvasItemMaterial Additive => additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

    public static void Burst(Node parent, Vector2 position, int amount, Color color, float speed, Vector2 direction, float spreadDegrees,
        float lifetime = 0.35f, float gravity = 0f, float scale = 2f, float damping = 60f, bool glow = false)
    {
        var particles = new CpuParticles2D
        {
            Position = position,
            Amount = Mathf.Max(1, amount),
            Lifetime = lifetime,
            OneShot = true,
            Explosiveness = 1f,
            Direction = direction == Vector2.Zero ? Vector2.Up : direction.Normalized(),
            Spread = spreadDegrees,
            InitialVelocityMin = speed * 0.45f,
            InitialVelocityMax = speed,
            Gravity = new Vector2(0, gravity),
            ScaleAmountMin = scale * 0.6f,
            ScaleAmountMax = scale,
            DampingMin = damping * 0.6f,
            DampingMax = damping,
            Color = color,
            ColorRamp = FadeRamp(color),
            LocalCoords = false,
            Emitting = true,
        };
        if (glow)
            particles.Material = Additive;
        parent.AddChild(particles);
        particles.GetTree().CreateTimer(lifetime + 0.2f).Timeout += particles.QueueFree;
    }

    private static Gradient FadeRamp(Color color)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, color);
        gradient.SetColor(1, new Color(color, 0f));
        return gradient;
    }

    public static void ImpactStar(Node parent, Vector2 position, float size, Color color)
    {
        parent.AddChild(new ImpactStar { Position = position, Size = size, Color = color, Material = Additive });
    }

    public static void Ring(Node parent, Vector2 position, float startRadius, float endRadius, Color color, float duration = 0.3f, float thickness = 3f, float squash = 0.5f)
    {
        parent.AddChild(new ShockwaveRing
        {
            Position = position,
            StartRadius = startRadius,
            EndRadius = endRadius,
            Color = color,
            Duration = duration,
            Thickness = thickness,
            Squash = squash,
        });
    }

    // One-shot animation from a horizontal strip of square frames in #Assets/VFX/Kio (exported from effects.aseprite).
    public static SpriteSheetEffect Sheet(Node parent, Vector2 position, string strip, int frames, float framesPerSecond, float scale = 1f, bool reverse = false)
    {
        var effect = new SpriteSheetEffect
        {
            Texture = Art.Texture($"VFX/Kio/{strip}.png"),
            FrameCount = frames,
            FramesPerSecond = framesPerSecond,
            Position = position,
            Scale = Vector2.One * scale,
            Reverse = reverse,
        };
        parent.AddChild(effect);
        return effect;
    }

    public static void Slash(Node parent, Vector2 position, float angle, float arcRadians, float radius, float thickness, Color color, bool clockwise, float duration = 0.2f)
    {
        parent.AddChild(new SlashEffect
        {
            Position = position,
            Angle = angle,
            ArcRadians = arcRadians,
            Radius = radius,
            Thickness = thickness,
            Color = color,
            Clockwise = clockwise,
            Duration = duration,
        });
    }

    public static void Number(Node parent, Vector2 position, string text, Color color, int fontSize = 10)
    {
        if (!GameSettings.DamageNumbers && text.Length > 0 && char.IsDigit(text[0]))
            return;
        parent.AddChild(new DamageNumber { Position = position, Text = text, Color = color, FontSize = fontSize });
    }

    public static void TenacityShatter(Node parent, Vector2 position)
    {
        parent.AddChild(new SpriteSheetEffect
        {
            Texture = Art.Texture("VFX/Tenacity VFX/cue.png"),
            FrameCount = 15,
            FramesPerSecond = 36f,
            Position = position,
            Scale = Vector2.One * 0.2f,
            Material = Additive,
            Modulate = new Color(1f, 0.92f, 0.45f),
        });
        Ring(parent, position, 6f, 44f, new Color(1f, 0.9f, 0.4f), 0.35f, 4f, 0.7f);
        Burst(parent, position, 18, new Color(1f, 0.88f, 0.35f), 190f, Vector2.Up, 180f, 0.45f, 160f, 2.5f, 90f, true);
    }

    public static void AfterImage(Node parent, Node2D anchor, Sprite2D source, float zLift, Color color, float duration = 0.24f)
    {
        var container = new Node2D { Position = anchor.Position + new Vector2(0, -0.5f) };
        var ghost = new Sprite2D
        {
            Texture = source.Texture,
            Hframes = source.Hframes,
            Vframes = source.Vframes,
            Frame = source.Frame,
            FlipH = source.FlipH,
            Offset = source.Offset,
            Centered = source.Centered,
            Position = new Vector2(0, -zLift) + source.Position,
            Scale = source.Scale,
            Modulate = color,
            Material = Additive,
        };
        container.AddChild(ghost);
        parent.AddChild(container);
        Tween tween = container.CreateTween();
        tween.TweenProperty(ghost, "modulate:a", 0f, duration);
        tween.TweenCallback(Callable.From(container.QueueFree));
    }
}
