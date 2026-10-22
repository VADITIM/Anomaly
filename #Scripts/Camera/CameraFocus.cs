using Godot;

namespace Anomaly;

// Picks the entity to frame during combat from cameraPriority, missing health and pointer proximity,
// then biases the camera toward the midpoint between Kio and that entity.
public sealed class CameraFocus
{
    private const float FOCUS_RADIUS = 170f;
    private const float FOCUS_WEIGHT = 0.32f;
    private const float MAX_FOCUS_OFFSET = 70f;
    private const float POINTER_BONUS_RADIUS = 40f;
    // In combat the frame leans harder toward the foe, and the current one is kept unless another clearly outranks it.
    private const float COMBAT_FOCUS_WEIGHT = 0.42f;
    private const float COMBAT_MAX_FOCUS_OFFSET = 90f;
    private const float STICKY_BONUS = 0.75f;

    public Entity Focused { get; private set; }
    public bool Enabled { get; set; } = true;

    public Vector2 ComputeOffset(GameWorld world, Vector2 pointer, bool inCombat = false)
    {
        Entity previous = Focused;
        Focused = null;
        Player player = world.Player;
        if (!Enabled || player.IsDead)
            return Vector2.Zero;

        float bestScore = 0f;
        foreach (Entity entity in world.Entities)
        {
            if (entity == player || entity.IsDead || entity.CameraPriority <= 0f)
                continue;
            float distance = entity.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (distance > FOCUS_RADIUS)
                continue;
            float score = entity.CameraPriority * (1f - distance / FOCUS_RADIUS * 0.5f);
            if (entity.VisualCenter.DistanceTo(pointer) < POINTER_BONUS_RADIUS)
                score += 2f;
            if (inCombat && entity == previous)
                score += STICKY_BONUS;
            if (score > bestScore)
            {
                bestScore = score;
                Focused = entity;
            }
        }
        if (Focused == null)
            return Vector2.Zero;
        Vector2 offset = (Focused.VisualCenter - player.VisualCenter) * (inCombat ? COMBAT_FOCUS_WEIGHT : FOCUS_WEIGHT);
        return offset.LimitLength(inCombat ? COMBAT_MAX_FOCUS_OFFSET : MAX_FOCUS_OFFSET);
    }
}
