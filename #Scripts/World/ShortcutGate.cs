using Godot;

namespace Anomaly;

// One-way shortcut: a barred gate whose bar can only be lifted from its far side. From the near side it is plainly a locked
// gate with a lever out of reach, a promise of a way back. Opened once, it stays open across rests and saves.
public partial class ShortcutGate : Node2D, IInteractable
{
    private const float HALF_SPAN = 48f;
    private const float BAR_HEIGHT = 38f;
    private const float LIFT_TIME = 0.7f;
    private static readonly Color Iron = new(0.2f, 0.19f, 0.24f);
    private static readonly Color IronLight = new(0.42f, 0.4f, 0.48f);
    private static readonly Color Lever = new(0.95f, 0.75f, 0.35f);

    private GameWorld world;
    private string id;
    private Vector2 openSide;
    private float surface;
    private bool open;
    private float lift;

    public Vector2 InteractionPosition => GlobalPosition + openSide * 22f;
    public float InteractionRadius => 30f;
    public string Prompt => "Lift the bar";
    public bool CanInteract => !open && !world.Player.IsDead && (world.Player.GlobalPosition - GlobalPosition).Dot(openSide) > 4f;
    // The gate spans across the passage: a north/south passage gets a gate running east-west.
    private Vector2 Span => Mathf.Abs(openSide.Y) > 0.5f ? Vector2.Right : Vector2.Down;

    public void Setup(GameWorld gameWorld, Vector2 position, string gateId, Vector2I side)
    {
        world = gameWorld;
        Position = position;
        id = gateId;
        openSide = new Vector2(side.X, side.Y);
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
        open = SaveManager.World.OpenedShortcuts.Contains(id);
        lift = open ? 1f : 0f;
    }

    public override void _Ready()
    {
        if (!open)
            for (int i = -1; i <= 1; i++)
                world.Grid.AddObstacle(GlobalPosition + Span * i * 32f, 16f, surface, 300f);
        world.RegisterInteractable(this);
    }

    public override void _EnterTree() => world?.RegisterInteractable(this);

    public override void _ExitTree() => world?.UnregisterInteractable(this);

    public void Interact(Player player)
    {
        if (open)
            return;
        open = true;
        SaveManager.World.OpenedShortcuts.Add(id);
        world.Grid.RemoveObstaclesNear(GlobalPosition, HALF_SPAN);
        Sfx.Play("break", -4f, 0.6f);
        Sfx.Play("hit_heavy", -8f, 0.5f);
        world.Camera.Feedback.AddTrauma(0.25f);
        world.Hud.Toast("A shortcut opens. The way back is shorter now.");
        Vfx.Burst(world.Effects, GlobalPosition + new Vector2(0, -surface - 6f), 20, new Color(0.62f, 0.58f, 0.52f), 90f, Vector2.Up, 160f, 0.6f, 200f, 2.4f, 30f);
    }

    public override void _Process(double delta)
    {
        if (open && lift < 1f)
        {
            lift = Mathf.Min(1f, lift + (float)delta / LIFT_TIME);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Vector2 ground = new(0, -surface);
        Vector2 span = Span;
        Vector2 left = ground - span * HALF_SPAN;
        Vector2 right = ground + span * HALF_SPAN;
        Vector2 up = new(0, -BAR_HEIGHT);
        float raised = Mathf.SmoothStep(0f, 1f, lift) * (BAR_HEIGHT - 6f);

        DrawSetTransform(ground, 0f, new Vector2(1f, 0.4f));
        DrawLine(-span * HALF_SPAN * 2.5f, span * HALF_SPAN * 2.5f, new Color(0f, 0f, 0f, 0.3f), 10f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        // Bars, lifted into the beam as the gate opens.
        int bars = 11;
        for (int i = 0; i < bars; i++)
        {
            Vector2 foot = left.Lerp(right, (i + 0.5f) / bars);
            Vector2 bottom = foot + new Vector2(0, -raised);
            if (bottom.Y > foot.Y + up.Y + 4f)
            {
                DrawLine(bottom, foot + up, Iron, 3f);
                DrawLine(bottom + new Vector2(-1, 0), foot + up + new Vector2(-1, 0), IronLight, 1f);
                DrawCircle(bottom, 1.6f, IronLight);
            }
        }
        if (lift < 0.5f)
            DrawLine(left + up * 0.45f + new Vector2(0, -raised), right + up * 0.45f + new Vector2(0, -raised), Iron, 3f);

        foreach (Vector2 post in new[] { left, right })
        {
            DrawRect(new Rect2(post + new Vector2(-4, up.Y - 6), new Vector2(8, -up.Y + 6)), Iron);
            DrawRect(new Rect2(post + new Vector2(-4, up.Y - 6), new Vector2(2, -up.Y + 6)), IronLight);
        }
        DrawRect(new Rect2(left + up + new Vector2(-6, -8), right - left + new Vector2(12, 7)), Iron);
        DrawLine(left + up + new Vector2(-6, -8), right + up + new Vector2(6, -8), IronLight, 1f);

        // The lever stands on the far side only: the near side sees it, out of reach.
        Vector2 lever = right + openSide * 14f;
        float tilt = open ? 0.9f : -0.5f;
        DrawLine(lever, lever + new Vector2(Mathf.Sin(tilt), -Mathf.Cos(tilt)) * 14f, Iron, 2.5f);
        DrawCircle(lever + new Vector2(Mathf.Sin(tilt), -Mathf.Cos(tilt)) * 14f, 2.5f, open ? IronLight : Lever);
    }
}
