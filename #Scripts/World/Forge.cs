using Godot;
using PhantomCamera;

namespace Anomaly;

// Maldor's forge, instanced from Forge.tscn: furnace, anvil, tools and Maldor himself. The forge owns its camera. Opening
// it hands the view to a PhantomCamera2D that frames Maldor and the anvil in the left fifth of the screen, leaving the
// right four fifths for the menu; closing it hands the view back to Kio's camera. Maldor can be switched off for story
// states in which he is away: he vanishes, blocks nothing and cannot be spoken to, but the furnace stays lit.
public partial class Forge : Node2D, IInteractable
{
    private const float GREET_RADIUS = 64f;
    private const float FOCUS_ZOOM = 1.4f;
    // Centre of Maldor and the anvil, as a share of the screen width from the left edge.
    private const float FOCUS_SCREEN_X = 0.1f;
    private const float FOCUS_BLEND_TIME = 0.5f;
    private const int FOCUS_PRIORITY = 20;
    private const string PHANTOM_CAMERA_2D_SCRIPT = "res://addons/phantom_camera/scripts/phantom_camera/phantom_camera_2d.gd";
    private static readonly Rect2 AnvilHotRegion = new(33, 135, 16, 9);

    private GameWorld world;
    private Node2D visuals;
    private Maldor maldor;
    private Sprite2D anvil;
    private Texture2D anvilCold;
    private Texture2D anvilHot;
    private PointLight2D furnaceGlow;
    private Node2D cameraNode;
    private PhantomCamera2D camera;
    private Marker2D focusPoint;
    private Marker2D sparkPoint;
    private float surface;
    private float time;
    private bool maldorPresent = true;
    private bool focused;

    public bool Focused => focused;

    public Vector2 InteractionPosition => GlobalPosition + anvil.Position;
    public float InteractionRadius => 38f;
    public string Prompt => "Maldor, Blacksmith";
    public bool CanInteract => maldorPresent && !world.Player.IsDead;

    public bool MaldorPresent
    {
        get => maldorPresent;
        set
        {
            if (maldorPresent == value)
                return;
            maldorPresent = value;
            ApplyMaldor();
        }
    }

    public void Setup(GameWorld gameWorld, Vector2 position)
    {
        world = gameWorld;
        Position = position;
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        visuals = GetNode<Node2D>("Visuals");
        visuals.Position = new Vector2(0, -surface);
        maldor = visuals.GetNode<Maldor>("Maldor");
        anvil = visuals.GetNode<Sprite2D>("Anvil");
        focusPoint = visuals.GetNode<Marker2D>("FocusPoint");
        sparkPoint = visuals.GetNode<Marker2D>("SparkPoint");
        anvilCold = anvil.Texture;
        anvilHot = new AtlasTexture { Atlas = ((AtlasTexture)anvilCold).Atlas, Region = AnvilHotRegion };
        maldor.Struck += Spark;

        Sprite2D furnace = visuals.GetNode<Sprite2D>("Furnace");
        furnaceGlow = LightFactory.Point(new Color(1f, 0.55f, 0.2f), 1.1f, 1.0f, furnace.Position + new Vector2(0, -14f));
        visuals.AddChild(furnaceGlow);

        BuildCamera();
        world.Grid.AddObstacle(GlobalPosition + furnace.Position, 20f, surface, 60f);
        world.Grid.AddObstacle(GlobalPosition + anvil.Position, 9f, surface, 14f);
        ApplyMaldor();
        world.RegisterInteractable(this);
    }

    public override void _EnterTree() => world?.RegisterInteractable(this);

    public override void _ExitTree() => world?.UnregisterInteractable(this);

    private void BuildCamera()
    {
        cameraNode = new Node2D { Name = "ForgeCamera", ProcessMode = ProcessModeEnum.Always };
        cameraNode.SetScript(GD.Load<Script>(PHANTOM_CAMERA_2D_SCRIPT));
        cameraNode.Set("priority", 0);
        AddChild(cameraNode);
        camera = cameraNode.AsPhantomCamera2D();
        camera.TweenDuration = FOCUS_BLEND_TIME;
        camera.TweenTransition = TransitionType.Quart;
        camera.TweenEase = EaseType.EaseInOut;
    }

    // Hands the view to the forge camera or back to Kio's. The camera is placed from the live viewport, so the framing
    // holds at any window size.
    public void FocusCamera(bool enabled)
    {
        focused = enabled;
        if (enabled)
        {
            Viewport viewport = GetViewport();
            float zoom = ViewScale.Factor(viewport) * FOCUS_ZOOM;
            float worldWidth = viewport.GetVisibleRect().Size.X / zoom;
            cameraNode.GlobalPosition = focusPoint.GlobalPosition + new Vector2(worldWidth * (0.5f - FOCUS_SCREEN_X), 0f);
            camera.Zoom = Vector2.One * zoom;
        }
        camera.Priority = enabled ? FOCUS_PRIORITY : 0;
    }

    private void ApplyMaldor()
    {
        maldor.Visible = maldorPresent;
        world.Grid.RemoveObstaclesNear(GlobalPosition + maldor.Position, 1f);
        if (maldorPresent)
            world.Grid.AddObstacle(GlobalPosition + maldor.Position, 13f, surface, 40f);
    }

    public void Interact(Player player)
    {
        maldor.Greet();
        world.OpenAnvilMenu();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        time += dt;
        furnaceGlow.Energy = 0.95f + 0.15f * Mathf.Sin(time * 6f) + 0.08f * Mathf.Sin(time * 15.3f) + (maldor.IsWorking ? 0.2f : 0f);
        if (!maldorPresent || (GetTree().Paused && !focused))
            return;

        if (world.Player != null && GlobalPosition.DistanceTo(world.Player.GlobalPosition) < GREET_RADIUS)
            maldor.Greet();
        maldor.Animate(dt);
        anvil.Texture = maldor.IsWorking ? anvilHot : anvilCold;
    }

    private void Spark()
    {
        Vfx.Burst(world.Effects, sparkPoint.GlobalPosition, 3, new Color(1f, 0.7f, 0.3f), 22f, Vector2.Up, 40f, 0.6f, 60f, 1.2f, 1.5f, true);
    }
}
