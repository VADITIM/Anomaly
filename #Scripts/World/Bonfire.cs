using Godot;

namespace Anomaly;

// Bonfire: where Kio is attuned and the Vessel deepened. Weapons and Rend belong to Maldor.
public partial class Bonfire : Node2D, IInteractable
{
    private GameWorld world;
    private float time;
    private float surface;

    public Vector2 InteractionPosition => GlobalPosition;
    public float InteractionRadius => 28f;
    public string Prompt => "Bonfire";
    public bool CanInteract => !world.Player.IsDead;

    public void Setup(GameWorld gameWorld, Vector2 position)
    {
        world = gameWorld;
        Position = position;
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
    }

    public override void _Ready()
    {
        AddChild(LightFactory.Point(new Color(0.55f, 0.5f, 1f), 1.0f, 0.8f, new Vector2(0, -surface - 6f)));
        world.RegisterInteractable(this);
    }

    public override void _EnterTree() => world?.RegisterInteractable(this);

    public override void _ExitTree() => world?.UnregisterInteractable(this);

    public void Interact(Player player)
    {
        world.AttuneBonfire(GlobalPosition);
        world.OpenBonfireMenu();
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        if (GD.Randf() < 0.3f)
        {
            Vector2 offset = new((float)GD.RandRange(-12, 12), -surface - (float)GD.RandRange(2, 6));
            Vfx.Burst(world.Effects, GlobalPosition + offset, 1, new Color(0.65f, 0.6f, 1f), 16f, Vector2.Up, 12f, 1.4f, -20f, 1.4f, 2f, true);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.5f);
        Vector2 c = new(0, -surface);
        DrawSetTransform(c, 0f, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, 17f, new Color(0.12f, 0.1f, 0.25f));
        DrawArc(Vector2.Zero, 15f, 0f, Mathf.Tau, 28, new Color(0.6f, 0.55f, 1f, 0.7f + 0.3f * pulse), 2f);
        DrawArc(Vector2.Zero, 9f + pulse * 2f, 0f, Mathf.Tau, 22, new Color(0.8f, 0.95f, 1f, 0.6f), 1.5f);
        DrawCircle(Vector2.Zero, 5f, new Color(0.5f, 0.45f, 1f, 0.35f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawLine(c + new Vector2(0, -2), c + new Vector2(0, -26 - pulse * 4f), new Color(0.7f, 0.65f, 1f, 0.25f), 8f);
    }
}
