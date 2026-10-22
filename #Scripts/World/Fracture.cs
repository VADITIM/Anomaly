using Godot;

namespace Anomaly;

// A Corrupted Fracture: a wound in Akasa that joins two distant points of it. Hollow Reach is torn open by them, one toward
// each continent Maldor has mapped. The Azurean tear in the west is sealed and only gives under the last Void Heart.
public partial class Fracture : Node2D, IInteractable
{
    public const int HEARTS_TO_UNSEAL = 4;
    private const float TEAR_STEP = 6.5f;
    private const float TEAR_WIDTH = 44f;
    private const float TEAR_MARGIN = 4f;
    private static readonly Color Rim = new(0.95f, 0.3f, 0.85f);
    private static readonly Color Core = new(0.75f, 0.95f, 1f);
    private static readonly Color Sealed = new(0.35f, 0.3f, 0.45f);

    private GameWorld world;
    private string destination;
    private bool isSealed;
    private float surface;
    private float time;
    private PointLight2D light;
    private Vector2[] edge;

    // An open Fracture can be stepped through into the Void; the one in the Void leads back to where Kio came from.
    public Vector2 InteractionPosition => GlobalPosition;
    public float InteractionRadius => 34f;
    public string Prompt => isSealed ? "Sealed Fracture" : world.InVoid ? $"Return to {destination}" : "Enter the Void";
    public bool CanInteract => !world.Player.IsDead;

    public void Setup(GameWorld gameWorld, Vector2 position, string destinationName, bool sealedShut)
    {
        world = gameWorld;
        Position = position;
        destination = destinationName;
        isSealed = sealedShut && SaveManager.World.DestroyedVoidHearts.Count < HEARTS_TO_UNSEAL;
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
    }

    public override void _Ready()
    {
        // A jagged vertical tear, fixed per Fracture so it reads as one place.
        var random = new RandomNumberGenerator { Seed = (ulong)(Position.X * 31f + Position.Y * 17f) };
        edge = new Vector2[9];
        float[] centers = new float[edge.Length];
        float[] widths = new float[edge.Length];
        for (int i = 0; i < edge.Length; i++)
        {
            centers[i] = Mathf.Round(random.RandfRange(-5f, 5f) * Mathf.Sin(i / 8f * Mathf.Pi));
            widths[i] = random.RandfRange(0.55f, 1f);
            edge[i] = new Vector2(centers[i], -surface - 6f - i * TEAR_STEP);
        }
        AddChild(BuildTear(centers, widths, random.Randf() * 100f));
        light = LightFactory.Point(isSealed ? Sealed : Rim, isSealed ? 0.5f : 1.1f, 1.1f, new Vector2(0, -surface - 30f));
        AddChild(light);
        world.Grid.AddObstacle(GlobalPosition, 10f, surface, 300f);
        world.RegisterInteractable(this);
    }

    // The tear itself is a pixel sprite drawn by rift.gdshader, a window onto the Void in the shape of the edge points.
    private ColorRect BuildTear(float[] centers, float[] widths, float seed)
    {
        float length = TEAR_STEP * (edge.Length - 1);
        var size = new Vector2(TEAR_WIDTH, length + TEAR_MARGIN * 2f);
        var material = new ShaderMaterial { Shader = ShaderLibrary.Rift };
        material.SetShaderParameter(StringNames.Of("rect_size"), size);
        material.SetShaderParameter(StringNames.Of("tear_length"), length);
        material.SetShaderParameter(StringNames.Of("margin"), TEAR_MARGIN);
        material.SetShaderParameter(StringNames.Of("center_knots"), centers);
        material.SetShaderParameter(StringNames.Of("width_knots"), widths);
        material.SetShaderParameter(StringNames.Of("seed"), seed);
        material.SetShaderParameter(StringNames.Of("sealed"), isSealed);
        return new ColorRect
        {
            Name = "Tear",
            Size = size,
            Position = new Vector2(-TEAR_WIDTH * 0.5f, -surface - 6f + TEAR_MARGIN - size.Y),
            Material = material,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    public override void _EnterTree() => world?.RegisterInteractable(this);

    public override void _ExitTree() => world?.UnregisterInteractable(this);

    public void Interact(Player player)
    {
        if (!isSealed)
        {
            world.TravelThroughFracture(GlobalPosition);
            return;
        }
        int remaining = HEARTS_TO_UNSEAL - SaveManager.World.DestroyedVoidHearts.Count;
        world.Hud.Toast($"The Fracture to {destination} is sealed. {remaining} Void Heart{(remaining == 1 ? "" : "s")} still beat.", 3f);
        Sfx.Play("heartbeat", -6f, 0.5f);
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        light.Energy = (isSealed ? 0.4f : 0.9f) + 0.25f * Mathf.Sin(time * 4f);
        if (!isSealed && GD.Randf() < (float)delta * 12f)
        {
            Vector2 at = GlobalPosition + edge[GD.RandRange(0, edge.Length - 1)];
            Vfx.Burst(world.Effects, at, 1, GD.Randf() < 0.5f ? Rim : Core, 30f, Vector2.FromAngle(GD.Randf() * Mathf.Tau), 180f, 0.6f, -10f, 1.6f, 15f, true);
        }
    }

    // Only the glow pooled on the ground; the tear is the child sprite, drawn over this.
    public override void _Draw()
    {
        DrawSetTransform(new Vector2(0, -surface), 0f, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, 18f, new Color(isSealed ? Sealed : Rim, 0.18f));
    }
}
