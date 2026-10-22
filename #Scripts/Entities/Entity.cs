using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Node position is the ground-plane footprint; Z is height above the zero plane. Body renders at (0, -Z),
// so Y-sorting on Position keeps depth correct while elevation, jumps and falls stay a pure Entity concern.
public abstract partial class Entity : Node2D, IHittable
{
    public const float GRAVITY = 1250f;
    private const float VOID_FALL_THRESHOLD = -4f;
    private const float VOID_DEPTH = -56f;
    private const float VOID_CLIP_REACH = 64f;
    private static readonly Color VoidSwallow = new(0.3f, 0.08f, 0.45f, 0f);

    [Export] public EntityStats Stats;

    public GameWorld World { get; private set; }
    public Node2D Body { get; private set; }
    public ImpactBehavior Impact { get; private set; }
    public ShaderMaterial EntityMaterial { get; private set; }

    public Vector2 MoveVelocity;
    public float Z { get; set; }
    public float ZVelocity;
    public float GravityScale = 1f;
    public float SupportZ { get; private set; }
    public bool IsGrounded { get; private set; } = true;
    public bool IsFlying;
    public bool IgnorePits;
    // Walks through walls and obstacles. Debug only.
    public bool NoClip;
    public bool CarefulMovement;
    public bool Solid = true;
    public float Radius = 5f;
    public float HurtRadius { get; protected set; } = 8f;
    public float HeightSpan { get; protected set; } = 22f;
    public float Health { get; protected set; }
    public float MaxHealth { get; protected set; }
    public bool IsDead { get; protected set; }
    public Vector2 BodyOffset;

    public Vector2 GroundPosition => GlobalPosition;
    public Vector2 VisualCenter => GlobalPosition + new Vector2(0, -Z - HeightSpan * 0.5f);
    public float HealthRatio => MaxHealth > 0f ? Health / MaxHealth : 0f;
    public virtual bool CanReceivePlayerHits => !IsDead;
    public virtual float CameraPriority => 0f;
    protected virtual string DefaultStatsPath => null;

    // Held for the whole run. Once every enemy of a kind has streamed out, a weakly held stats resource can be
    // finalized by the GC just as the next one loads it from the resource cache, which hands back a dead object.
    private static readonly Dictionary<string, EntityStats> statsCache = new();

    private static EntityStats LoadStats(string path)
    {
        if (!statsCache.TryGetValue(path, out EntityStats stats))
            statsCache[path] = stats = GD.Load<EntityStats>(path);
        return stats;
    }

    private readonly List<IEntityBehavior> behaviors = new();
    private float flashRemaining;
    private float flashDuration = 1f;
    private bool fellIntoVoid;

    public override void _Ready()
    {
        World = NodeResolver.FindOnRoot<GameWorld>(this);
        Stats ??= DefaultStatsPath != null ? LoadStats(DefaultStatsPath) : new EntityStats();

        AddChild(new EntityShadow { Name = "Shadow" });
        Body = new Node2D { Name = "Body" };
        AddChild(Body);
        EntityMaterial = new ShaderMaterial { Shader = ShaderLibrary.Entity };

        Impact = new ImpactBehavior(this);
        AddBehavior(Impact);

        float surface = World.Grid.SurfaceZAt(GlobalPosition);
        SupportZ = float.IsNegativeInfinity(surface) ? 0f : surface;
        Z = SupportZ;

        InitializeEntity();
        MaxHealth = ComputeMaxHealth();
        Health = MaxHealth;
        World.RegisterEntity(this);
    }

    // Reparenting a node (sections adopt their scenery) leaves and re-enters the tree without a second _Ready.
    public override void _EnterTree() => World?.RegisterEntity(this);

    public override void _ExitTree()
    {
        World?.UnregisterEntity(this);
    }

    public void AddBehavior(IEntityBehavior behavior) => behaviors.Add(behavior);

    protected abstract void InitializeEntity();

    protected virtual float ComputeMaxHealth() => Stats.MaxHealth;

    protected virtual void TickEntity(float delta) { }

    protected virtual void OnLanded(float impactSpeed) { }

    protected virtual void OnFellIntoVoid() { }

    protected virtual void OnHit(HitInfo hit, ImpactResult impact) { }

    protected virtual void OnDeath(HitInfo hit) { }

    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 1f / 30f);
        if (dt <= 0f)
            return;
        foreach (IEntityBehavior behavior in behaviors)
            behavior.Process(dt);
        TickEntity(dt);
        Integrate(dt);
        UpdateFlash(dt);
        UpdateVoidFallVisual();
    }

    // A body dropping into the Void keeps its Y-sort slot and sinks straight down on screen, as it would in 3D.
    // Clipping it to the pit opening lets the ground in front of the pit swallow it, while everything north of
    // its footprint (the far cliff face, the rim behind it) stays behind it.
    private void UpdateVoidFallVisual()
    {
        bool voidFalling = Z < VOID_FALL_THRESHOLD && float.IsNegativeInfinity(SupportZ);
        Body.Position = new Vector2(0, -Z) + BodyOffset;
        if (!voidFalling)
        {
            if (ClipChildren != ClipChildrenMode.Disabled)
            {
                ClipChildren = ClipChildrenMode.Disabled;
                Modulate = Colors.White;
                QueueRedraw();
            }
            return;
        }
        ClipChildren = ClipChildrenMode.Only;
        float depth = Mathf.Clamp(Z / VOID_DEPTH, 0f, 1f);
        Modulate = Colors.White.Lerp(VoidSwallow, depth * depth);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (ClipChildren == ClipChildrenMode.Disabled)
            return;
        const int T = WorldGrid.TILE_SIZE;
        Vector2 origin = GlobalPosition;
        DrawRect(new Rect2(-VOID_CLIP_REACH, -VOID_CLIP_REACH * 2f, VOID_CLIP_REACH * 2f, VOID_CLIP_REACH * 2f), Colors.White);
        Vector2I tile = WorldGrid.ToTile(origin);
        for (int y = tile.Y; y <= tile.Y + 3; y++)
            for (int x = tile.X - 2; x <= tile.X + 2; x++)
                if (World.Grid.IsPit(x, y))
                    DrawRect(new Rect2(new Vector2(x * T, y * T) - origin, T, T), Colors.White);
    }

    private void Integrate(float delta)
    {
        MoveBy(MoveVelocity * delta, CarefulMovement);
        MoveBy(Impact.KnockbackVelocity * delta, false);
        if (Solid && !IsDead)
            SeparateFromNeighbours(delta);
        ResolveVertical(delta);
    }

    protected void MoveBy(Vector2 motion, bool careful)
    {
        float length = motion.Length();
        if (length < 0.0001f)
            return;
        int steps = Mathf.CeilToInt(length / 4f);
        Vector2 step = motion / steps;
        for (int i = 0; i < steps; i++)
        {
            bool movedX = TryMove(new Vector2(step.X, 0), careful);
            bool movedY = TryMove(new Vector2(0, step.Y), careful);
            if (!movedX && !movedY)
                break;
        }
    }

    private bool TryMove(Vector2 step, bool careful)
    {
        if (step == Vector2.Zero)
            return false;
        Vector2 target = Position + step;
        if (!NoClip && World.Grid.Blocks(target, Radius, Z))
            return false;
        if (careful && !IsFlying && IsGrounded)
        {
            Vector2 probe = target + step.Normalized() * Radius;
            if (World.Grid.SurfaceZAt(probe) < SupportZ - 1f)
                return false;
        }
        Position = target;
        return true;
    }

    private void SeparateFromNeighbours(float delta)
    {
        foreach (Entity other in World.Entities)
        {
            if (other == this || !other.Solid || other.IsDead || Mathf.Abs(other.Z - Z) > 18f)
                continue;
            Vector2 offset = Position - other.Position;
            float minimum = Radius + other.Radius;
            float distanceSquared = offset.LengthSquared();
            if (distanceSquared >= minimum * minimum)
                continue;
            float distance = Mathf.Sqrt(distanceSquared);
            Vector2 normal = distance > 0.01f ? offset / distance : Vector2.Right.Rotated(GetInstanceId() % 628 / 100f);
            float share = other.Stats.Weight / (Stats.Weight + other.Stats.Weight);
            MoveBy(normal * (minimum - distance) * share * Mathf.Min(1f, delta * 20f), false);
        }
    }

    private void ResolveVertical(float delta)
    {
        if (IsFlying)
        {
            SupportZ = World.Grid.SurfaceZAt(GlobalPosition);
            IsGrounded = false;
            return;
        }

        float support = World.Grid.SupportZ(Position, Radius, Z);
        if (float.IsNegativeInfinity(support) && IgnorePits)
            support = Z;
        SupportZ = support;

        if (Z > support + 0.5f || ZVelocity > 0f)
        {
            IsGrounded = false;
            ZVelocity -= GRAVITY * GravityScale * delta;
            Z += ZVelocity * delta;
            if (Z <= support)
            {
                float impact = -ZVelocity;
                Z = support;
                ZVelocity = 0f;
                IsGrounded = true;
                OnLanded(impact);
            }
        }
        else
        {
            Z = support;
            ZVelocity = 0f;
            IsGrounded = true;
        }

        if (float.IsNegativeInfinity(support) && !fellIntoVoid && Z < VOID_DEPTH)
        {
            fellIntoVoid = true;
            OnFellIntoVoid();
        }
    }

    public void ResetVoidFall()
    {
        fellIntoVoid = false;
        ZVelocity = 0f;
    }

    public void PlaceAt(Vector2 groundPosition)
    {
        Position = groundPosition;
        float surface = World.Grid.SurfaceZAt(groundPosition);
        SupportZ = float.IsNegativeInfinity(surface) ? 0f : surface;
        Z = SupportZ;
        ZVelocity = 0f;
        IsGrounded = true;
        Impact.ClearKnockback();
        ResetVoidFall();
    }

    public void Launch(float verticalVelocity)
    {
        ZVelocity = Mathf.Max(ZVelocity, verticalVelocity);
        IsGrounded = false;
    }

    public void Flash(float duration, Color color)
    {
        flashDuration = duration;
        flashRemaining = duration;
        EntityMaterial.SetShaderParameter(StringNames.Of("flash_color"), color);
        EntityMaterial.SetShaderParameter(StringNames.Of("flash"), 1f);
    }

    private void UpdateFlash(float delta)
    {
        if (flashRemaining <= 0f)
            return;
        flashRemaining -= delta;
        float strength = flashRemaining <= 0f ? 0f : Mathf.Clamp(flashRemaining / flashDuration, 0f, 1f);
        EntityMaterial.SetShaderParameter(StringNames.Of("flash"), strength > 0.45f ? 1f : strength * 1.6f);
    }

    public void SetOutline(float strength, Color color)
    {
        EntityMaterial.SetShaderParameter(StringNames.Of("outline"), strength);
        EntityMaterial.SetShaderParameter(StringNames.Of("outline_color"), color);
    }

    public bool IsWithinVerticalReach(float otherZ, float otherSpan)
    {
        return Z < otherZ + otherSpan + 6f && otherZ < Z + HeightSpan + 6f;
    }

    public virtual HitResponse ReceiveHit(HitInfo hit)
    {
        if (IsDead)
            return HitResponse.Ignored;
        ImpactResult impact = Impact.Resolve(hit);
        Health = Mathf.Max(0f, Health - hit.Damage);
        Flash(0.1f, Colors.White);
        OnHit(hit, impact);
        if (Health <= 0f)
        {
            IsDead = true;
            OnDeath(hit);
            return HitResponse.Killed;
        }
        return HitResponse.Landed;
    }

    public void RestoreHealth(float amount)
    {
        Health = Mathf.Min(MaxHealth, Health + amount);
    }
}
