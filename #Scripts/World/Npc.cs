using Godot;

namespace Anomaly;

// A talkable villager. The body is a placeholder (Maldor's idle strip, tinted) until NPC sprites exist; what it says
// comes from Dialogues, keyed by the placement id. A soft light marks it from a distance.
public partial class Npc : Node2D, IInteractable
{
    private const string BODY = "Entities/Maldor/idle.png";
    private const int FRAMES = 4;
    private const float FPS = 4f;
    private const float SCALE = 1.25f;

    private GameWorld world;
    private Dialogues.NpcDef def;
    private Sprite2D sprite;
    private float surface;
    private float time;

    public Vector2 InteractionPosition => GlobalPosition;
    public float InteractionRadius => 26f;
    public string Prompt => $"{def.DisplayName}, Talk";
    public bool CanInteract => !world.Player.IsDead && !world.Narration.IsOpen;

    public void Setup(GameWorld gameWorld, Vector2 position, string id)
    {
        world = gameWorld;
        def = Dialogues.Npc(id);
        Position = position;
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
    }

    public override void _Ready()
    {
        sprite = new Sprite2D
        {
            Texture = Art.Texture(BODY),
            RegionEnabled = true,
            RegionRect = new Rect2(0, 0, 32, 32),
            Centered = false,
            Offset = new Vector2(-16, -31),
            Scale = Vector2.One * SCALE,
            Position = new Vector2(0, -surface),
            Modulate = def.Tint,
        };
        AddChild(sprite);
        AddChild(LightFactory.Point(new Color(0.75f, 0.9f, 1f), 0.5f, 0.6f, new Vector2(0, -surface - 14f)));
        world.Grid.AddObstacle(GlobalPosition, 7f, surface, 26f);
        world.RegisterInteractable(this);
    }

    public override void _EnterTree() => world?.RegisterInteractable(this);

    public override void _ExitTree() => world?.UnregisterInteractable(this);

    public void Interact(Player player)
    {
        sprite.FlipH = player.GlobalPosition.X < GlobalPosition.X;
        world.Narration.Play(def.Script());
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        sprite.RegionRect = new Rect2((int)(time * FPS) % FRAMES * 32, 0, 32, 32);
    }
}
