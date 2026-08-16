using Godot;

// Special hit: every Nth landed swing fires the slotted Arc's signature effect.
// The interval counter is continuous — it is deliberately NOT reset when the
// combo wraps or when Tenacity resets, so an interval of 3 against the 4-step
// combo lands on combo steps 3, 2, 1, 4, ... (design.md §3.5).
public partial class Weapon
{
    private void AdvanceHitCounter()
    {
        _hitCount++;

        int interval = SpecialHitInterval;
        _isSpecialHitSwing = interval > 0 && (_hitCount % interval) == 0;
    }

    private void TriggerSpecialHit(Entity target)
    {
        if (!_isSpecialHitSwing || _currentArc?.Data == null || target == null)
            return;

        SoulWeaponArc arc = _currentArc.Data;

        SpawnSpecialHitEffect(arc, target.GlobalPosition);

        switch (arc.SpecialHit)
        {
            case SpecialHitKind.MultiHit:
                StartMultiHit(arc, target);
                break;

            case SpecialHitKind.DelayedShatter:
                StartDelayedShatter(arc, target.GlobalPosition, target.Elevation);
                break;
        }
    }

    private void SpawnSpecialHitEffect(SoulWeaponArc arc, Vector2 globalPosition)
    {
        if (arc.SpecialHitEffect == null)
            return;

        Node2D effect = arc.SpecialHitEffect.Instantiate<Node2D>();
        GetTree().CurrentScene.AddChild(effect);
        effect.GlobalPosition = globalPosition;
    }

    // Spear: MultiHitCount piercing strikes spread across MultiHitWindow. The
    // first strike is the swing's own hit, so only the remainder are scheduled.
    private void StartMultiHit(SoulWeaponArc arc, Entity target)
    {
        int followUpHits = Mathf.Max(arc.MultiHitCount - 1, 0);
        if (followUpHits == 0)
            return;

        float spacing = arc.MultiHitWindow / followUpHits;
        ulong targetId = target.GetInstanceId();

        for (int i = 1; i <= followUpHits; i++)
        {
            Timer timer = WeaponArc.QuickTimer(this, spacing * i);
            timer.Timeout += () => ApplyFollowUpHit(targetId, arc);
        }
    }

    private void ApplyFollowUpHit(ulong targetId, SoulWeaponArc arc)
    {
        if (GodotObject.InstanceFromId(targetId) is not Entity target || !IsInstanceValid(target))
            return;

        target.TakeDamage(_currentArc.Damage, GlobalPosition, _currentArc);
        SpawnSpecialHitEffect(arc, target.GlobalPosition);
    }

    // Hammer: the force lands late, shattering outward from the impact point.
    private void StartDelayedShatter(SoulWeaponArc arc, Vector2 impactPosition, float impactElevation)
    {
        Timer timer = WeaponArc.QuickTimer(this, arc.ShatterDelay);
        timer.Timeout += () => ApplyShatter(arc, impactPosition, impactElevation);
    }

    private void ApplyShatter(SoulWeaponArc arc, Vector2 impactPosition, float impactElevation)
    {
        if (!IsInstanceValid(this) || _currentArc == null)
            return;

        SpawnSpecialHitEffect(arc, impactPosition);

        var shape = new CircleShape2D { Radius = arc.ShatterRadius };
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape,
            Transform = new Transform2D(0f, impactPosition),
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = _hitbox?.CollisionMask ?? uint.MaxValue
        };

        float shatterDamage = _currentArc.Damage * arc.ShatterDamageMultiplier;

        foreach (Godot.Collections.Dictionary hit in GetWorld2D().DirectSpaceState.IntersectShape(query))
        {
            if (hit["collider"].As<GodotObject>() is not Entity entity || entity == OwnerPlayer)
                continue;

            if (!ElevationMath.SharesPlane(impactElevation, entity.Elevation, 0.5f + arc.ShatterElevationSpan))
                continue;

            entity.TakeDamage(shatterDamage, impactPosition, _currentArc);
        }
    }
}
