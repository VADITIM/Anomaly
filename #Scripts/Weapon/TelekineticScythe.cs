using Godot;

namespace Anomaly;

// Kio never holds the Scythe: it floats at the shoulder and is flung around Kio by will.
// It manifests on the first strike, stays while enemies are engaged, and vanishes in a burst LINGER_TIME after combat ends.
public partial class TelekineticScythe : Node2D
{
    public const float LINGER_TIME = 3f;
    private const float BLADE_SCALE = 0.62f;
    private const float ORBIT_RADIUS = 8f;
    private const float FADE_IN_TIME = 0.1f;
    private const float POP_TIME = 0.2f;
    private const float RECOVER_HOLD = 0.06f;
    private static readonly Vector2 CHEST = new(0f, -11f);
    private static readonly Vector2 GRIP = new(6f, 26f);
    private static readonly Vector2 HEAD = new(20f, 11f);
    private const float TEXTURE_WIDTH = 29f;
    // Farthest opaque pixel of the art from the grip, in texture pixels.
    private const float BLADE_LENGTH = 29.8f;
    private const float GRIND_REACH = 13f;
    private const float GRIND_SPIN_SPEED = 34f;

    // How far the blade tip travels from the hand while orbiting: the radius every wake is measured against.
    public const float VISUAL_REACH = ORBIT_RADIUS + BLADE_LENGTH * BLADE_SCALE;
    // The grind spins the blade about its own grip, so its disc is the blade alone.
    public const float SPIN_REACH = BLADE_LENGTH * BLADE_SCALE / VISUAL_REACH;

    // The Scythe's own attack pattern, one entry per strike of the light combo.
    private static readonly StrikeVfx[] STRIKES =
    {
        new() { SlashArcDegrees = 150f, SlashReach = 1f, SlashThickness = 5f },
        new() { SlashArcDegrees = 170f, SlashReach = 1f, SlashThickness = 5f },
        new() { SlashArcDegrees = 360f, SlashReach = 1f, SlashThickness = 6f, SlashLinger = 0.1f, RingReach = 1f },
        new() { SlashArcDegrees = 290f, SlashReach = SPIN_REACH, SlashThickness = 6f, RingReach = 0.85f, SparkCount = 6, StarSize = 10f },
        new() { SlashArcDegrees = 140f, SlashReach = 1.1f, SlashThickness = 8f, RingReach = 1.25f, RingThickness = 3f, SparkCount = 10, StarSize = 16f },
    };

    public static StrikeVfx Strike(int comboStep) => STRIKES[Mathf.Clamp(comboStep, 0, STRIKES.Length - 1)];

    private enum Pose
    {
        Hover,
        Windup,
        Sweep,
        Recover,
        Thrust,
        GrindWindup,
        Grind,
    }

    public bool IsManifested { get; private set; }

    private Player player;
    private Sprite2D blade;
    private Sprite2D aura;
    private Pose pose = Pose.Hover;
    private float poseTime;
    private float windupDuration;
    private float sweepDuration;
    private float thrustDuration;
    private float grindDuration;
    private float windupAngle;
    private float endAngle;
    private float orbitAngle;
    private bool clockwise;
    private Vector2 windupFromPosition;
    private float windupFromRotation;
    private Vector2 thrustDirection;
    private Vector2 hoverGlobal;
    private float presence;
    private float lingerTimer;
    private float popTimer;
    private float ghostTimer;
    private float bobTime;
    private bool thrown;

    public void Setup(Player owner)
    {
        player = owner;
        Name = "Telekinetic Scythe";
        ZAsRelative = true;
        Texture2D texture = Art.Texture("Weapons/Scythe/SoulScythe.png");
        aura = new Sprite2D { Texture = texture, Centered = false, Material = Vfx.Additive, Scale = Vector2.One * 1.18f };
        blade = new Sprite2D { Texture = texture, Centered = false };
        AddChild(aura);
        AddChild(blade);
        ApplyFlip(false);
        Visible = false;
    }

    public void Manifest()
    {
        lingerTimer = LINGER_TIME;
        if (IsManifested)
            return;
        IsManifested = true;
        popTimer = POP_TIME;
        if (presence < 0.05f)
        {
            pose = Pose.Hover;
            Position = HoverTarget();
            Rotation = HoverRotation();
            hoverGlobal = player.Body.ToGlobal(Position);
        }
        Vector2 at = player.Body.ToGlobal(Position + CHEST * 0.5f);
        Vfx.Burst(player.World.Effects, at, 10, Tint, 90f, Vector2.Up, 180f, 0.32f, -30f, 1.6f, 80f, true);
        Vfx.Ring(player.World.Effects, at, 2f, 16f, Tint, 0.22f, 2f, 0.8f);
        Sfx.Play("catch", -7f, 1f, 0.05f);
    }

    public void Dismiss()
    {
        if (!IsManifested)
            return;
        IsManifested = false;
        // Gone at once: the blade vanishes where it hangs and the burst plays in its place.
        presence = 0f;
        Visible = false;
        Vfx.Burst(player.World.Effects, GlobalPosition, 14, Tint, 70f, Vector2.Up, 180f, 0.45f, -40f, 1.6f, 70f, true);
        Vfx.Ring(player.World.Effects, GlobalPosition, 2f, 18f, Tint, 0.25f, 2f, 0.8f);
        Sfx.Play("aetheriac_off", -16f, 1.1f);
    }

    public void Withdraw()
    {
        IsManifested = false;
        thrown = false;
        presence = 0f;
        pose = Pose.Hover;
        Visible = false;
    }

    public void SetThrown(bool value)
    {
        thrown = value;
        if (value)
            return;
        pose = Pose.Hover;
        Position = CHEST;
        hoverGlobal = player.Body.ToGlobal(Position);
        popTimer = POP_TIME;
        Manifest();
    }

    // Windup pulls the blade behind the swing, the sweep orbits it through the arc with the tip leading.
    public void BeginSwing(Vector2 direction, float arcRadians, bool swingClockwise, float windup, float sweep)
    {
        clockwise = swingClockwise;
        float sign = clockwise ? 1f : -1f;
        float center = direction.Angle();
        windupAngle = center - sign * (arcRadians * 0.5f + 0.35f);
        endAngle = center + sign * arcRadians * 0.5f;
        windupFromPosition = Position;
        windupFromRotation = Rotation;
        windupDuration = Mathf.Max(windup, 0.01f);
        sweepDuration = Mathf.Max(sweep, 0.01f);
        orbitAngle = windupAngle;
        SetPose(Pose.Windup);
    }

    // The blade is flung out in front and spun on its own axis like a saw, buzzing in place for the whole grind.
    public void Grind(Vector2 direction, float windup, float duration)
    {
        thrustDirection = direction.Normalized();
        windupFromPosition = Position;
        windupFromRotation = Rotation;
        windupDuration = Mathf.Max(windup, 0.01f);
        grindDuration = Mathf.Max(duration, 0.01f);
        SetPose(Pose.GrindWindup);
    }

    public void Thrust(Vector2 direction, float duration)
    {
        thrustDirection = direction.Normalized();
        thrustDuration = duration;
        SetPose(Pose.Thrust);
    }

    public override void _Process(double delta)
    {
        if (player == null)
            return;
        float dt = (float)delta;
        bobTime += dt;
        poseTime += dt;

        UpdatePresence(dt);
        Visible = presence > 0.01f && !thrown && !player.IsDead && player.Sprite.Visible;
        if (!Visible)
            return;

        switch (pose)
        {
            case Pose.Windup:
                UpdateWindup();
                break;
            case Pose.Sweep:
                UpdateSweep(dt);
                break;
            case Pose.Recover:
                if (poseTime >= RECOVER_HOLD)
                    SetPose(Pose.Hover);
                break;
            case Pose.Thrust:
                UpdateThrust(dt);
                break;
            case Pose.GrindWindup:
                UpdateGrindWindup();
                break;
            case Pose.Grind:
                UpdateGrind(dt);
                break;
            default:
                UpdateHover(dt);
                break;
        }

        float pop = popTimer > 0f ? Mathf.Sin(popTimer / POP_TIME * Mathf.Pi) * 0.35f : 0f;
        popTimer = Mathf.Max(0f, popTimer - dt);
        Scale = Vector2.One * BLADE_SCALE * (0.55f + 0.45f * presence + pop);
        Color tint = Tint;
        blade.Modulate = new Color(Colors.White.Lerp(tint, 0.25f), presence);
        aura.Modulate = new Color(tint, presence * (0.3f + 0.12f * Mathf.Sin(bobTime * 6f) + pop));
    }

    private Color Tint => player.AetheriacActive ? new Color(1f, 0.25f, 0.32f) : player.CurrentArc.TrailColor;

    private void UpdatePresence(float dt)
    {
        if (IsManifested)
        {
            if (player.InCombat || player.IsAttacking)
                lingerTimer = LINGER_TIME;
            else
                lingerTimer -= dt;
            if (lingerTimer <= 0f)
                Dismiss();
        }
        float target = IsManifested ? 1f : 0f;
        presence = Mathf.MoveToward(presence, target, dt / FADE_IN_TIME);
    }

    private Vector2 HoverTarget()
    {
        float side = player.Facing.X >= 0f ? -1f : 1f;
        return new Vector2(side * 10f, -13f + Mathf.Sin(bobTime * 2.4f) * 1.5f);
    }

    private float HoverRotation()
    {
        float side = player.Facing.X >= 0f ? -1f : 1f;
        return side * 0.3f + Mathf.Sin(bobTime * 1.7f) * 0.07f;
    }

    // The hover point is tracked in world space so the blade trails Kio's movement instead of being bolted on.
    private void UpdateHover(float dt)
    {
        Vector2 target = player.Body.ToGlobal(HoverTarget());
        hoverGlobal = hoverGlobal.Lerp(target, 1f - Mathf.Exp(-9f * dt));
        Position = player.Body.ToLocal(hoverGlobal);
        Rotation = Mathf.LerpAngle(Rotation, HoverRotation(), 1f - Mathf.Exp(-10f * dt));
        ApplyFlip(player.Facing.X < 0f);
        ZIndex = player.Facing.Y < -0.3f ? 1 : -1;
    }

    private void UpdateWindup()
    {
        float t = Mathf.Clamp(poseTime / windupDuration, 0f, 1f);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        Position = windupFromPosition.Lerp(OrbitPosition(windupAngle), eased);
        Rotation = Mathf.LerpAngle(windupFromRotation, windupAngle + Mathf.Pi * 0.5f, eased);
        ApplyFlip(!clockwise);
        ZIndex = Mathf.Sin(windupAngle) < -0.2f ? -1 : 1;
        if (t >= 1f)
            SetPose(Pose.Sweep);
    }

    private void UpdateSweep(float dt)
    {
        float t = Mathf.Clamp(poseTime / sweepDuration, 0f, 1f);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        orbitAngle = Mathf.Lerp(windupAngle, endAngle, eased);
        Position = OrbitPosition(orbitAngle);
        Rotation = orbitAngle + Mathf.Pi * 0.5f;
        ZIndex = Mathf.Sin(orbitAngle) < -0.2f ? -1 : 1;

        ghostTimer -= dt;
        if (ghostTimer <= 0f)
        {
            ghostTimer = 0.016f;
            SpawnGhost();
        }
        if (t >= 1f)
        {
            hoverGlobal = player.Body.ToGlobal(Position);
            SetPose(Pose.Recover);
        }
    }

    private void UpdateThrust(float dt)
    {
        float t = Mathf.Clamp(poseTime / thrustDuration, 0f, 1f);
        float reach = t < 0.3f ? Mathf.Lerp(2f, 12f, t / 0.3f) : Mathf.Lerp(12f, 6f, (t - 0.3f) / 0.7f);
        Position = CHEST + thrustDirection * reach;
        Rotation = Mathf.LerpAngle(Rotation, thrustDirection.Angle() + Mathf.Pi * 0.5f, 1f - Mathf.Exp(-30f * dt));
        ZIndex = thrustDirection.Y < -0.3f ? -1 : 1;
        if (t >= 1f)
        {
            hoverGlobal = player.Body.ToGlobal(Position);
            SetPose(Pose.Hover);
        }
    }

    private Vector2 GrindPosition => CHEST + thrustDirection * GRIND_REACH;

    // Pulled back over the shoulder first, then flung forward to the saw point.
    private void UpdateGrindWindup()
    {
        float t = Mathf.Clamp(poseTime / windupDuration, 0f, 1f);
        Vector2 cocked = CHEST - thrustDirection * 6f + new Vector2(0, -6f);
        Position = t < 0.6f
            ? windupFromPosition.Lerp(cocked, t / 0.6f)
            : cocked.Lerp(GrindPosition, Mathf.Pow((t - 0.6f) / 0.4f, 2f));
        Rotation = Mathf.LerpAngle(windupFromRotation, thrustDirection.Angle() - Mathf.Pi * 0.5f, t);
        ApplyFlip(false);
        ZIndex = thrustDirection.Y < -0.3f ? -1 : 1;
        if (t >= 1f)
            SetPose(Pose.Grind);
    }

    private void UpdateGrind(float dt)
    {
        float t = Mathf.Clamp(poseTime / grindDuration, 0f, 1f);
        float jitter = Mathf.Sin(poseTime * 90f) * 0.8f;
        Position = GrindPosition + thrustDirection.Orthogonal() * jitter;
        Rotation += GRIND_SPIN_SPEED * dt * (1f - t * 0.4f);
        ZIndex = thrustDirection.Y < -0.3f ? -1 : 1;
        ghostTimer -= dt;
        if (ghostTimer <= 0f)
        {
            ghostTimer = 0.02f;
            SpawnGhost();
        }
        if (t >= 1f)
        {
            hoverGlobal = player.Body.ToGlobal(Position);
            SetPose(Pose.Recover);
        }
    }

    private Vector2 OrbitPosition(float angle) => CHEST + Vector2.FromAngle(angle) * ORBIT_RADIUS;

    private void SetPose(Pose next)
    {
        pose = next;
        poseTime = 0f;
        ghostTimer = 0f;
    }

    // The art is drawn diagonally with the blade curling left, so it is mirrored and tilted until the shaft points
    // straight up from the grip with the blade on its right; orbit maths can then treat the shaft as local up.
    private void ApplyFlip(bool flip)
    {
        bool mirror = !flip;
        Vector2 grip = mirror ? new Vector2(TEXTURE_WIDTH - GRIP.X, GRIP.Y) : GRIP;
        Vector2 head = mirror ? new Vector2(TEXTURE_WIDTH - HEAD.X, HEAD.Y) : HEAD;
        float tilt = -Mathf.Pi * 0.5f - (head - grip).Angle();
        foreach (Sprite2D sprite in new[] { blade, aura })
        {
            sprite.FlipH = mirror;
            sprite.Offset = -grip;
            sprite.Rotation = tilt;
        }
    }

    private void SpawnGhost()
    {
        var ghost = new Sprite2D
        {
            Texture = blade.Texture,
            Centered = false,
            FlipH = blade.FlipH,
            Offset = blade.Offset,
            GlobalTransform = blade.GlobalTransform,
            Material = Vfx.Additive,
            Modulate = new Color(Tint, 0.45f),
            ZIndex = 4,
        };
        player.World.Effects.AddChild(ghost);
        ghost.GlobalTransform = blade.GlobalTransform;
        Tween tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate:a", 0f, 0.14f);
        tween.TweenCallback(Callable.From(ghost.QueueFree));
    }
}
