using Godot;

namespace Anomaly;

public enum PropKind
{
    Crate,
    Barrel,
    Vase,
    Pot,
    SoulCache,
}

public partial class Prop : Entity
{
    [Export] public PropKind Kind = PropKind.Crate;

    private Sprite2D sprite;
    private PropResourceBar bar;
    private float wobble;

    protected override string DefaultStatsPath => Kind switch
    {
        PropKind.Vase or PropKind.Pot => "res://#Resources/EntityStats/Vase.tres",
        PropKind.SoulCache => "res://#Resources/EntityStats/SoulCache.tres",
        _ => "res://#Resources/EntityStats/Crate.tres",
    };

    private bool IsClay => Kind is PropKind.Vase or PropKind.Pot;
    private Color DebrisColor => IsClay ? new Color(0.72f, 0.45f, 0.3f) : Kind == PropKind.SoulCache ? new Color(0.6f, 0.5f, 0.35f) : new Color(0.55f, 0.38f, 0.22f);

    protected override void InitializeEntity()
    {
        SpriteRegion region = Kind switch
        {
            PropKind.Barrel => Art.Barrel,
            PropKind.Vase => Art.Vase,
            PropKind.Pot => Art.Pot,
            PropKind.SoulCache => Art.Chest,
            _ => Art.Crate,
        };
        Radius = IsClay ? 6f : 9f;
        HurtRadius = Radius + 3f;
        HeightSpan = region.Region.Size.Y * 0.7f;
        sprite = Art.CreateSprite(region);
        sprite.Material = EntityMaterial;
        Body.AddChild(sprite);

        bar = new PropResourceBar();
        bar.Setup(this, -region.Origin.Y - 6f);
        Body.AddChild(bar);

        if (Kind == PropKind.SoulCache)
        {
            var light = LightFactory.Point(new Color(0.5f, 0.85f, 1f), 0.6f, 1f, new Vector2(0, -12), 128);
            Body.AddChild(light);
        }
    }

    protected override void TickEntity(float delta)
    {
        wobble = Mathf.MoveToward(wobble, 0f, delta * 4f);
        Body.Rotation = Mathf.Sin(wobble * 40f) * wobble * 0.25f;
        MoveVelocity = Vector2.Zero;
        if (Kind == PropKind.SoulCache && GD.Randf() < delta * 6f)
            Vfx.Burst(World.Effects, GlobalPosition + new Vector2((float)GD.RandRange(-8, 8), -Z - 6f), 1, new Color(0.55f, 0.9f, 1f), 18f, Vector2.Up, 20f, 0.8f, -20f, 1.5f, 5f, true);
    }

    protected override void OnHit(HitInfo hit, ImpactResult impact)
    {
        wobble = 1f;
        bar.Reveal();
        Vfx.Burst(World.Effects, VisualCenter, 5, DebrisColor, 110f, hit.Direction, 60f, 0.4f, 300f, 2f, 20f);
        Sfx.Play("land", -6f, IsClay ? 1.6f : 1.1f);
    }

    protected override void OnDeath(HitInfo hit)
    {
        Sfx.Play("break", -2f, IsClay ? 1.25f : 0.95f);
        Vfx.Burst(World.Effects, VisualCenter, 18, DebrisColor, 170f, hit.Direction, 120f, 0.6f, 420f, 2.6f, 30f);
        Vfx.Burst(World.Effects, VisualCenter, 6, new Color(0.85f, 0.8f, 0.7f, 0.6f), 40f, Vector2.Up, 180f, 0.5f, -10f, 3f, 40f);
        int souls = Kind switch
        {
            PropKind.SoulCache => 160,
            PropKind.Crate or PropKind.Barrel => (int)GD.RandRange(3, 7),
            _ => (int)GD.RandRange(1, 5),
        };
        World.SpawnSoulOrbs(GlobalPosition, Z + 6f, souls);
        PropSignals.EmitBroken(this);
        QueueFree();
    }
}
