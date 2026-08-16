/// <summary>
/// Engine-free elevation rules: physics-layer masks and height/elevation conversion.
/// Kept free of Godot types so the reachability rules stay unit-testable (see Tests/).
/// </summary>
public static class ElevationMath
{
    // "Elevation 0" is 2d_physics/layer_20 in project.godot; layers are 1-based, mask bits 0-based.
    public const int FirstElevationLayer = 20;
    public const int MaxElevation = 10;

    // One elevation step equals one wall tile of height. Must match the wall tileset art.
    public const float PixelsPerElevation = 32f;

    public static uint ElevationBit(int elevation)
        => 1u << (FirstElevationLayer - 1 + elevation);

    public static uint AllElevationBits
    {
        get
        {
            uint bits = 0u;
            for (int elevation = 0; elevation <= MaxElevation; elevation++)
                bits |= ElevationBit(elevation);
            return bits;
        }
    }

    // Bodies an airborne entity passes over: Player Collision (1), Enemy Hitbox (5), Prop (6).
    // An entity that must never be jumped over (a tree) is put on Wall (10) instead — see
    // Entity.CanBeJumpedOver.
    public const uint JumpableBodyBits = (1u << 0) | (1u << 4) | (1u << 5);
    public const uint WallBit = 1u << 9;

    public static float HeightToElevation(float heightInPixels)
        => heightInPixels / PixelsPerElevation;

    /// <summary>
    /// The highest plane a jump may reach. Sprite height keeps rising past this — only what the
    /// entity may clear or land on is capped, so a stronger JumpImpulse never skips a plane.
    /// </summary>
    public static float ReachableElevation(float groundElevation, float currentElevation, float maxJumpElevations)
    {
        float ceiling = groundElevation + maxJumpElevations;
        return currentElevation < ceiling ? currentElevation : ceiling;
    }

    /// <summary>
    /// Whether an entity may pass the wall in front of it: only if the ground on the other side is
    /// within its jump reach. A cliff two planes high stays solid for a one-plane jumper even while
    /// airborne, so it can never end up standing inside the cliff face.
    /// </summary>
    public static bool CanClear(float surfaceAhead, float groundElevation, float maxJumpElevations)
        => surfaceAhead <= groundElevation + maxJumpElevations;

    /// <summary>
    /// A wall belonging to <paramref name="wallElevation"/> blocks an entity standing at its own
    /// plane (edges and cliffs above), but an airborne entity passes over every wall it has risen
    /// above — which is what makes an edge crossable exactly when the jump is high enough.
    /// </summary>
    public static bool BlocksMovement(int wallElevation, float entityElevation, bool isAirborne)
        => isAirborne ? wallElevation > entityElevation : wallElevation >= entityElevation;

    /// <summary>
    /// Builds the collision mask for an entity from the mask its scene authored. Always derived from
    /// the authored mask rather than the live one, so landing restores exactly what took off.
    /// </summary>
    public static uint ResolveMask(uint baseMask, float entityElevation, bool isAirborne)
    {
        uint mask = baseMask & ~AllElevationBits;

        if (isAirborne)
            mask &= ~JumpableBodyBits;

        for (int elevation = 0; elevation <= MaxElevation; elevation++)
        {
            if (BlocksMovement(elevation, entityElevation, isAirborne))
                mask |= ElevationBit(elevation);
        }

        return mask;
    }

    // Falling one elevation is free — dropping off a single step is normal traversal, not injury.
    // TODO(vadim): confirm these two numbers and record them in design.md §3.2 (#elevation).
    public const float FallGraceElevations = 1f;
    public const float ImpactDamagePerElevation = 10f;

    /// <summary>Impact damage a falling body deals on landing, from distance fallen and its weight.</summary>
    public static float FallImpactDamage(float elevationsFallen, float weight)
    {
        float damaging = elevationsFallen - FallGraceElevations;
        return damaging <= 0f ? 0f : damaging * weight * ImpactDamagePerElevation;
    }

    /// <summary>Heavier bodies drop faster; weight 1 falls at the entity's authored fall speed.</summary>
    public static float FallSpeed(float baseFallSpeed, float weight)
        => baseFallSpeed * (weight < 0.1f ? 0.1f : weight);

    /// <summary>True when two entities occupy the same plane and can therefore hit each other.</summary>
    public static bool SharesPlane(float first, float second, float tolerance = 0.5f)
        => (first > second ? first - second : second - first) < tolerance;
}
