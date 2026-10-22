using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Soul Scythe Arc special: the bond lets Kio hurl the Scythe, and the same bond drags it back through everything in its path.
public partial class ScytheProjectile : Node2D
{
    private const float OUT_TIME = 0.34f;
    private const float OUT_SPEED = 380f;
    private const float RETURN_MAX_SPEED = 470f;
    private const float HAND_HEIGHT = 11f;

    private Player owner;
    private Vector2 direction;
    private float z;
    private float time;
    private bool returning;
    private float returnSpeed = 90f;
    private float trailTimer;
    private float whooshTimer;
    private Sprite2D sprite;
    private readonly HashSet<IHittable> hitThisPass = new();

    public void Setup(Player thrower, Vector2 throwDirection)
    {
        owner = thrower;
        direction = throwDirection;
        Position = thrower.GlobalPosition + throwDirection * 8f;
        z = thrower.Z + HAND_HEIGHT;
    }

    public override void _Ready()
    {
        sprite = new Sprite2D { Texture = Art.Texture("Weapons/Scythe/SoulScythe.png"), Scale = Vector2.One * 0.9f };
        AddChild(sprite);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (owner.IsDead || !IsInstanceValid(owner))
        {
            QueueFree();
            return;
        }
        time += dt;

        if (!returning)
        {
            float progress = time / OUT_TIME;
            Vector2 next = Position + direction * OUT_SPEED * (1f - progress * 0.85f) * dt;
            if (owner.World.Grid.SurfaceZAt(next) > z || progress >= 1f)
                BeginReturn();
            else
                Position = next;
        }
        else
        {
            returnSpeed = Mathf.Min(RETURN_MAX_SPEED, returnSpeed + 1400f * dt);
            Vector2 toOwner = owner.GlobalPosition - GlobalPosition;
            if (toOwner.Length() < 10f)
            {
                owner.OnScytheCaught();
                QueueFree();
                return;
            }
            Position += toOwner.Normalized() * Mathf.Min(returnSpeed * dt, toOwner.Length());
            z = Mathf.Lerp(z, owner.Z + HAND_HEIGHT, 1f - Mathf.Exp(-10f * dt));
        }

        sprite.Rotation += dt * 30f;
        sprite.Position = new Vector2(0, -z);
        DealHits();

        trailTimer -= dt;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.025f;
            SpawnGhost();
        }
        whooshTimer -= dt;
        if (whooshTimer <= 0f)
        {
            whooshTimer = 0.14f;
            Sfx.Play("swing2", -14f, 1.05f);
        }
        QueueRedraw();
    }

    private void BeginReturn()
    {
        returning = true;
        hitThisPass.Clear();
    }

    private void DealHits()
    {
        SoulWeaponArc arc = owner.CurrentArc;
        Vector2 travel = returning ? (owner.GlobalPosition - GlobalPosition).Normalized() : direction;
        foreach (IHittable target in owner.World.CollectPlayerTargets())
        {
            if (hitThisPass.Contains(target) || target.GroundPosition.DistanceTo(GlobalPosition) > 13f + target.HurtRadius)
                continue;
            if (Mathf.Abs(target.Z + target.HeightSpan * 0.5f - (z - 4f)) > target.HeightSpan * 0.5f + 14f)
                continue;
            hitThisPass.Add(target);
            HitInfo hit = owner.BuildHit(target, arc.SpecialDamageMultiplier * 0.8f, arc.SpecialMoveDamageType, travel, 150f, 14f, 0.045f, false);
            hit.ContactPoint = GlobalPosition + new Vector2(0, -z);
            owner.DeliverHit(target, hit, 0.12f);
        }
    }

    private void SpawnGhost()
    {
        var ghost = new Sprite2D
        {
            Texture = sprite.Texture,
            Position = GlobalPosition + sprite.Position,
            Rotation = sprite.Rotation,
            Scale = sprite.Scale,
            Modulate = new Color(owner.CurrentArc.TrailColor, 0.45f),
            Material = Vfx.Additive,
        };
        owner.World.Effects.AddChild(ghost);
        Tween tween = ghost.CreateTween();
        tween.TweenProperty(ghost, "modulate:a", 0f, 0.16f);
        tween.TweenCallback(Callable.From(ghost.QueueFree));
    }

    public override void _Draw()
    {
        float surface = owner.World.Grid.SurfaceZAt(GlobalPosition);
        if (float.IsNegativeInfinity(surface))
            return;
        DrawSetTransform(new Vector2(0, -surface), 0f, new Vector2(1f, 0.4f));
        DrawCircle(Vector2.Zero, 7f, new Color(0.02f, 0f, 0.05f, 0.3f));
    }
}
