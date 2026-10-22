using Godot;

namespace Anomaly;

// Every push runs through here: self-propelled lunges and steps, and the knockback a hit hands its target.
// A push that drives into something spends part of its momentum on it, and heavier targets soak up more.
public static class Push
{
    // Share of a push's speed spent on a target that weighs as much as the pusher.
    public const float HIT_REDUCTION = 0.4f;
    // However heavy the target, a push keeps at least this much of its speed.
    public const float MAX_HIT_REDUCTION = 0.9f;
    // Weight of an entity whose stats leave it unset; matches EntityStats.Weight's default.
    public const float DEFAULT_WEIGHT = 1f;
    // Floor on weight, so a featherweight never turns knockback unbounded.
    public const float MIN_WEIGHT = 0.2f;
    // Hittables without a body (shots the player swats back) weigh nothing and never slow a push.
    public const float BODILESS_WEIGHT = 0f;
    // Knockback bleeds off exponentially at this rate.
    public const float KNOCKBACK_DECAY = 9f;
    // The spear lunge bursts out and brakes at a constant rate: about 85 px over 0.2 s when it meets nothing.
    public const float SPEAR_LUNGE_SPEED = 850f;
    public const float SPEAR_LUNGE_DECELERATION = 4250f;

    public static float WeightOf(IHittable target) => target is Entity entity
        ? Mathf.Max(MIN_WEIGHT, entity.Stats?.Weight ?? DEFAULT_WEIGHT)
        : BODILESS_WEIGHT;

    // Each pusher's weight worth of target eats HIT_REDUCTION of what is left, so an equal pushes through at 60%
    // and a target four times heavier stops it nearly dead.
    public static float Retained(Entity pusher, IHittable target)
    {
        float ratio = WeightOf(target) / WeightOf(pusher);
        return 1f - Mathf.Min(1f - Mathf.Pow(1f - HIT_REDUCTION, ratio), MAX_HIT_REDUCTION);
    }

    // Dodges and ignored hits never touched the target, so only a landed hit costs momentum.
    public static bool Connected(HitResponse response) => response is HitResponse.Landed or HitResponse.Killed;

    public static Vector2 Decay(Vector2 velocity, float rate, float delta) => velocity.Lerp(Vector2.Zero, 1f - Mathf.Exp(-rate * delta));

    // The speed a hit's force drives into a body of the target's weight.
    public static float KnockbackSpeed(float force, Entity target) => force / WeightOf(target);
}
