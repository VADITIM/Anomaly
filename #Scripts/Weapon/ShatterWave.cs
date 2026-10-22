using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Soul Hammer Arc special: the impact travels through the ground as a line of ruptures that cut what stands above them.
public partial class ShatterWave : Node2D
{
    private const int RUPTURE_COUNT = 6;
    private const float RUPTURE_INTERVAL = 0.055f;
    private const float RUPTURE_SPACING = 17f;
    private const float RUPTURE_RADIUS = 17f;

    private Player owner;
    private Vector2 origin;
    private Vector2 direction;
    private float baseZ;
    private float timer;
    private int spawned;
    private readonly HashSet<IHittable> struck = new();

    public void Setup(Player caster, Vector2 waveDirection)
    {
        owner = caster;
        origin = caster.GlobalPosition;
        direction = waveDirection;
        baseZ = caster.SupportZ;
    }

    public override void _Process(double delta)
    {
        timer -= (float)delta;
        if (timer > 0f)
            return;
        timer = RUPTURE_INTERVAL;
        if (spawned >= RUPTURE_COUNT)
        {
            QueueFree();
            return;
        }
        Vector2 point = origin + direction * (14f + spawned * RUPTURE_SPACING);
        if (!Mathf.IsEqualApprox(owner.World.Grid.SurfaceZAt(point), baseZ))
        {
            QueueFree();
            return;
        }
        spawned++;
        Rupture(point);
    }

    private void Rupture(Vector2 point)
    {
        SoulWeaponArc arc = owner.CurrentArc;
        Vector2 screen = point + new Vector2(0, -baseZ);
        Vfx.Ring(owner.World.Effects, screen, 3f, RUPTURE_RADIUS + 6f, new Color(arc.TrailColor, 0.9f), 0.3f, 3f);
        Vfx.ImpactStar(owner.World.Effects, screen + new Vector2(0, -3f), 12f, arc.TrailColor);
        Vfx.Burst(owner.World.Effects, screen, 10, new Color(0.6f, 0.56f, 0.5f), 150f, Vector2.Up, 50f, 0.5f, 520f, 2.4f, 10f);
        owner.World.Camera.Feedback.AddTrauma(0.1f);
        Sfx.Play("land", -4f, 0.7f + spawned * 0.08f);

        foreach (IHittable target in owner.World.CollectPlayerTargets())
        {
            if (struck.Contains(target) || target.GroundPosition.DistanceTo(point) > RUPTURE_RADIUS + target.HurtRadius)
                continue;
            if (target.Z > baseZ + 28f || target.Z + target.HeightSpan < baseZ - 6f)
                continue;
            struck.Add(target);
            HitInfo hit = owner.BuildHit(target, arc.SpecialDamageMultiplier, arc.SpecialMoveDamageType, direction, 200f, 22f, 0.05f, false);
            hit.Lift = 170f;
            hit.ContactPoint = screen + new Vector2(0, -6f);
            owner.DeliverHit(target, hit, 0.12f);
        }
    }
}
