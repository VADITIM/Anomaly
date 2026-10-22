using Godot;

namespace Anomaly;

public enum WorldLightKind
{
    Brazier,
    VoidGlow,
}

// Placed light sources that carry the scene now that ambient light is low. Braziers are warm, flickering and solid;
// Void glows well up from pit edges in the Void's magenta, with no body at all.
public partial class WorldLight : Node2D
{
    private static readonly Color FIRE = new(1f, 0.58f, 0.26f);
    private static readonly Color EMBER = new(1f, 0.36f, 0.12f);
    private static readonly Color VOID = new(0.78f, 0.22f, 0.86f);

    private WorldLightKind kind;
    private GameWorld world;
    private float surface;
    private PointLight2D light;
    private PointLight2D core;
    private float baseEnergy;
    private float time;
    private float phase;

    public void Setup(GameWorld gameWorld, WorldLightKind lightKind, Vector2 position)
    {
        world = gameWorld;
        kind = lightKind;
        Position = position;
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
        phase = position.X * 0.013f + position.Y * 0.007f;
    }

    public override void _Ready()
    {
        switch (kind)
        {
            case WorldLightKind.Brazier:
                baseEnergy = 1.15f;
                light = AddLight(FIRE, baseEnergy, 1.5f, -surface - 16f);
                core = AddLight(new Color(1f, 0.8f, 0.55f), 0.6f, 0.45f, -surface - 14f);
                AddChild(Flames());
                world.Grid.AddObstacle(GlobalPosition, 7f, surface, 22f);
                break;
            default:
                baseEnergy = 0.55f;
                light = AddLight(VOID, baseEnergy, 2.4f, -surface + 6f);
                AddChild(Motes(VOID, -surface + 4f, 18f, 8, 3.5f));
                ZIndex = -40;
                break;
        }
    }

    private PointLight2D AddLight(Color color, float energy, float scale, float y)
    {
        var point = LightFactory.Point(color, energy, scale, new Vector2(0, y));
        AddChild(point);
        return point;
    }

    private CpuParticles2D Flames()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 0.9f, 0.6f, 1f));
        gradient.SetColor(1, new Color(EMBER, 0f));
        gradient.AddPoint(0.35f, new Color(FIRE, 0.9f));
        return new CpuParticles2D
        {
            Position = new Vector2(0, -surface - 13f),
            Amount = 18,
            Lifetime = 0.55f,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(3.5f, 1f),
            Direction = Vector2.Up,
            Spread = 12f,
            Gravity = new Vector2(0, -60f),
            InitialVelocityMin = 10f,
            InitialVelocityMax = 22f,
            ScaleAmountMin = 1.5f,
            ScaleAmountMax = 3f,
            ColorRamp = gradient,
            Material = Vfx.Additive,
        };
    }

    private static CpuParticles2D Motes(Color color, float y, float extent, int amount, float lifetime)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(color, 0f));
        gradient.SetColor(1, new Color(color, 0f));
        gradient.AddPoint(0.3f, new Color(color.Lightened(0.3f), 0.85f));
        return new CpuParticles2D
        {
            Position = new Vector2(0, y),
            Amount = amount,
            Lifetime = lifetime,
            Preprocess = lifetime,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(extent, extent * 0.4f),
            Direction = Vector2.Up,
            Spread = 25f,
            Gravity = new Vector2(0, -4f),
            InitialVelocityMin = 3f,
            InitialVelocityMax = 9f,
            ScaleAmountMin = 1f,
            ScaleAmountMax = 1.6f,
            ColorRamp = gradient,
            Material = Vfx.Additive,
        };
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        float t = time + phase;
        switch (kind)
        {
            case WorldLightKind.Brazier:
                // Two detuned sines plus a fast jitter read as fire without the strobing of pure noise.
                float flicker = 0.82f + 0.1f * Mathf.Sin(t * 7.3f) + 0.06f * Mathf.Sin(t * 13.1f + 1.7f) + 0.05f * (GD.Randf() - 0.5f);
                light.Energy = baseEnergy * flicker;
                light.TextureScale = 1.5f + 0.05f * Mathf.Sin(t * 5.2f);
                core.Energy = 0.6f * flicker;
                QueueRedraw();
                break;
            default:
                light.Energy = baseEnergy * (0.75f + 0.25f * Mathf.Sin(t * 0.9f));
                break;
        }
    }

    public static ulong ProbeUsec;
    public static int ProbeCalls;
    public override void _Draw()
    {
        ulong probeStart = Time.GetTicksUsec();
        ProbeCalls++;
        try { DrawBody(); } finally { ProbeUsec += Time.GetTicksUsec() - probeStart; }
    }

    private void DrawBody()
    {
        if (kind == WorldLightKind.Brazier)
            DrawBrazier();
    }

    private void DrawBrazier()
    {
        float top = -surface - 12f;
        var stone = new Color(0.32f, 0.3f, 0.34f);
        var dark = new Color(0.16f, 0.14f, 0.18f);
        DrawRect(new Rect2(-2f, top + 3f, 4f, 9f), dark);
        DrawRect(new Rect2(-4f, -surface - 1f, 8f, 2f), dark);
        DrawColoredPolygon(new[] { new Vector2(-7f, top), new Vector2(7f, top), new Vector2(4f, top + 4f), new Vector2(-4f, top + 4f) }, stone);
        DrawLine(new Vector2(-7f, top), new Vector2(7f, top), stone.Lightened(0.25f), 1f);
        float glow = 0.6f + 0.4f * Mathf.Sin((time + phase) * 9f);
        DrawRect(new Rect2(-5f, top - 1f, 10f, 1f), new Color(1f, 0.55f + 0.2f * glow, 0.25f));
    }
}
