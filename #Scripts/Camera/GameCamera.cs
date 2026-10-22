using Godot;
using PhantomCamera;

namespace Anomaly;

// Owns the Camera2D + PhantomCameraHost and the player's PhantomCamera2D. The PCam follows a rig node composed each frame:
// Kio, plus a lean toward the cursor (or right stick) that grows with its distance, plus CameraFocus's pull toward the
// enemy Kio is fighting. Unlocking the camera lets the lean reach much further, for scouting. PCam damping smooths it.
// Zoom is derived from the visible rect so the framed world area is identical at any window/viewport setting.
public partial class GameCamera : Node2D
{
    private const string PHANTOM_CAMERA_2D_SCRIPT = "res://addons/phantom_camera/scripts/phantom_camera/phantom_camera_2d.gd";
    private const string PHANTOM_CAMERA_HOST_SCRIPT = "res://addons/phantom_camera/scripts/phantom_camera_host/phantom_camera_host.gd";
    private const float FOLLOW_DAMPING = 0.14f;
    // Share of the Kio-to-cursor distance the screen leans by, and how far it may lean, locked and unlocked.
    private const float DRAG_WEIGHT = 0.1f;
    private const float DRAG_MAX = 32f;
    // In a fight the cursor lean shrinks to this share, leaving the frame to CameraFocus's pull toward the foe.
    private const float COMBAT_DRAG_SCALE = 0.25f;
    private const float FREE_DRAG_WEIGHT = 0.6f;
    private const float FREE_DRAG_MAX = 220f;
    // A stick held fully over leans as far as the cursor would at this distance.
    private const float STICK_REACH = 300f;
    // The lean eases in and out instead of snapping when the cursor jumps or the camera is unlocked.
    private const float OFFSET_RATE = 6f;
    private const float RETURN_BLEND_TIME = 0.5f;

    public CameraFeedback Feedback { get; } = new();
    public CameraFocus Focus { get; } = new();
    public Camera2D Camera { get; private set; }
    public bool Overview { get; private set; }
    public bool Unlocked { get; private set; }

    private GameWorld world;
    private Node2D rig;
    private Node2D phantomCameraNode;
    private PhantomCamera2D phantomCamera;
    private Vector2 offset;
    private ulong lastTicks;

    public void Setup(GameWorld gameWorld)
    {
        world = gameWorld;
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = 100;

        Camera = new Camera2D { Name = "Camera2D", ProcessCallback = Camera2D.Camera2DProcessCallback.Idle };
        AddChild(Camera);
        var host = new Node { Name = "PhantomCameraHost" };
        host.SetScript(GD.Load<Script>(PHANTOM_CAMERA_HOST_SCRIPT));
        Camera.AddChild(host);

        rig = new Node2D { Name = "FollowRig" };
        AddChild(rig);

        phantomCameraNode = new Node2D { Name = "PlayerPhantomCamera" };
        phantomCameraNode.SetScript(GD.Load<Script>(PHANTOM_CAMERA_2D_SCRIPT));
        phantomCameraNode.Set("priority", 10);
        phantomCameraNode.Set("follow_mode", (int)FollowMode2D.Simple);
        phantomCameraNode.Set("follow_target", rig);
        phantomCameraNode.Set("follow_damping", true);
        phantomCameraNode.Set("follow_damping_value", new Vector2(FOLLOW_DAMPING, FOLLOW_DAMPING));
        AddChild(phantomCameraNode);
        phantomCamera = phantomCameraNode.AsPhantomCamera2D();
        phantomCamera.TweenDuration = RETURN_BLEND_TIME;
        phantomCamera.TweenTransition = TransitionType.Quart;
        phantomCamera.TweenEase = EaseType.EaseInOut;
        ApplyLimits(world.Grid.Bounds);

        Camera.MakeCurrent();
        lastTicks = Time.GetTicksUsec();
    }

    private float BaseZoom => ViewScale.Factor(GetViewport());

    private void ApplyLimits(Rect2 bounds)
    {
        phantomCamera.LimitLeft = (int)bounds.Position.X;
        phantomCamera.LimitTop = (int)bounds.Position.Y;
        phantomCamera.LimitRight = (int)bounds.End.X;
        phantomCamera.LimitBottom = (int)bounds.End.Y;
    }

    public void SetOverview(bool enabled)
    {
        Overview = enabled;
        ApplyLimits(enabled ? new Rect2(-100000, -100000, 200000, 200000) : world.Grid.Bounds);
        if (enabled)
        {
            rig.GlobalPosition = world.Grid.Bounds.GetCenter();
            Vector2 visible = GetViewport().GetVisibleRect().Size;
            Vector2 span = world.Grid.Bounds.Size;
            phantomCamera.Zoom = Vector2.One * Mathf.Min(visible.X / span.X, visible.Y / span.Y) * 0.96f;
            phantomCamera.TeleportPosition();
        }
    }

    public void SetUnlocked(bool unlocked)
    {
        Unlocked = unlocked;
        world.Hud?.Toast(unlocked ? "Camera unlocked" : "Camera locked", 1.2f);
    }

    public void SnapToPlayer()
    {
        offset = Vector2.Zero;
        rig.GlobalPosition = world.Player.GlobalPosition;
        phantomCamera.TeleportPosition();
        world.Sections.Refresh(immediate: true);
    }

    // The world rect the screen shows right now, shake aside.
    public Rect2 VisibleWorldRect()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size / Mathf.Max(0.01f, BaseZoom);
        return new Rect2(Camera.GetScreenCenterPosition() - size * 0.5f, size);
    }

    // Where the player is pointing, relative to Kio: the cursor, or the right stick scaled out to cursor distances.
    private Vector2 PointerReach()
    {
        if (GameInput.UsingGamepad)
            return Input.GetVector(StringNames.Of(GameInput.AIM_LEFT), StringNames.Of(GameInput.AIM_RIGHT), StringNames.Of(GameInput.AIM_UP), StringNames.Of(GameInput.AIM_DOWN)) * STICK_REACH;
        return GetGlobalMousePosition() - world.Player.GlobalPosition;
    }

    private void Follow(float dt)
    {
        Player player = world.Player;
        Vector2 reach = PointerReach();
        Vector2 target = Vector2.Zero;
        if (!player.IsDead && Unlocked)
            target = (reach * FREE_DRAG_WEIGHT).LimitLength(FREE_DRAG_MAX);
        else if (!player.IsDead)
        {
            float drag = player.InCombat ? COMBAT_DRAG_SCALE : 1f;
            target = (reach * DRAG_WEIGHT * drag).LimitLength(DRAG_MAX * drag) + Focus.ComputeOffset(world, player.GlobalPosition + reach, player.InCombat);
        }
        offset = offset.Lerp(target, 1f - Mathf.Exp(-OFFSET_RATE * dt));
        rig.GlobalPosition = player.GlobalPosition + offset;
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        float realDelta = Mathf.Min((now - lastTicks) / 1_000_000f, 0.1f);
        lastTicks = now;
        Feedback.Update(realDelta);
        Camera.Offset = Feedback.Offset;
        if (world == null || Overview)
            return;

        phantomCamera.Zoom = Vector2.One * BaseZoom * (1f + Feedback.ZoomBoost);
        // Menus pause the tree while this node keeps running; Kio's frame holds until the game resumes.
        if (world.GetTree().Paused)
            return;

        Follow(realDelta);
    }
}
