using Godot;

/// <summary>
/// Manages the "Z axis" (height) simulation for an Entity in a 2-D top-down game.
///
/// Key changes from the previous version:
///   • Elevation transitions (jump-up / step-down) are validated against
///     ElevationSystem before being permitted.
///   • During a jump-up, collision layer 10 (wall layer) is disabled so the
///     entity passes through the ledge wall.  It is re-enabled on landing.
///   • FloorZ is driven by ElevationSystem.GetElevationFloorZ(elevation).
///   • Step-down is detected when the entity walks onto the edge of its current
///     elevation and a lower ground+wall pair is confirmed adjacent.
/// </summary>
public class ZAxis
{
    // ── Owner ────────────────────────────────────────────────────────────────
    private readonly Entity entity;

    // ── Vertical state ───────────────────────────────────────────────────────
    private float z          = 0f;
    private float zVelocity  = 0f;
    private float floorZ     = 0f;

    // ── Tunable parameters ───────────────────────────────────────────────────
    // jumpHeight default is 40f — enough to clear one elevation band (32 px) with headroom.
    private float jumpHeight       = 40f;
    private float gravity          = 900f;
    private float terminalVelocity = 3000f;
    private float airborneEpsilon  = 0.1f;

    // ── Wall-collision phasing ───────────────────────────────────────────────
    public bool IgnoreWallsWhenAirborne  { get; set; } = true;
    public int  WallCollisionLayerNumber { get; set; } = 9;

    /// <summary>
    /// When true, layer 10 is forced OFF unconditionally — regardless of whether
    /// the entity had the layer enabled in the editor.  Cleared on landing.
    /// </summary>
    private bool forceWallsOff = false;

    // ── Public accessors ─────────────────────────────────────────────────────
    public float Z             => z;
    public float ZVelocity     => zVelocity;
    public float FloorZ        => floorZ;
    public float JumpHeight    { get => jumpHeight;       set => jumpHeight       = Mathf.Max(0f, value); }
    public float Gravity       { get => gravity;          set => gravity          = Mathf.Max(0f, value); }
    public float TerminalVelocity { get => terminalVelocity; set => terminalVelocity = Mathf.Max(0f, value); }
    public float AirborneEpsilon  { get => airborneEpsilon;  set => airborneEpsilon  = Mathf.Max(0f, value); }

    public bool IsGrounded => z <= floorZ + 0.01f && zVelocity <= 0.01f;
    public bool IsAirborne => !IsGrounded;

    // ── Constructor ──────────────────────────────────────────────────────────
    public ZAxis(Entity ownerEntity, float initialFloorZ = 0f)
    {
        entity  = ownerEntity;
        floorZ  = Mathf.Max(0f, initialFloorZ);
        z       = floorZ;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Per-frame update
    // ═════════════════════════════════════════════════════════════════════════

    public void ProcessVertical(float delta)
    {
        // Safety clamp below floor.
        if (z < floorZ)
        {
            z         = floorZ;
            zVelocity = 0f;
        }

        // Already settled on the floor with no upward velocity.
        // Only clear forceWallsOff here if it wasn't just set this frame by TryJump.
        // We distinguish this by checking zVelocity == 0 (TryJump sets it > 0).
        if (IsGrounded && zVelocity <= 0f)
        {
            forceWallsOff = false;
            UpdateWallCollisionMask();
            return;
        }

        // ── Airborne integration ──────────────────────────────────────────────
        zVelocity -= gravity * delta;
        if (zVelocity < -terminalVelocity)
            zVelocity = -terminalVelocity;

        z += zVelocity * delta;

        // Cap at apex.
        float apex = floorZ + jumpHeight;
        if (zVelocity > 0f && z >= apex)
        {
            z         = apex;
            zVelocity = 0f;
        }

        // ── Landing ───────────────────────────────────────────────────────────
        bool justLanded = false;
        if (z <= floorZ)
        {
            z             = floorZ;
            zVelocity     = 0f;
            forceWallsOff = false;
            justLanded    = true;
        }

        UpdateWallCollisionMask();
        entity.OnZChanged();

        if (justLanded)
            entity.OnZChanged();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jump (elevation-aware)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempts to make the entity jump.
    ///
    /// If <paramref name="targetElevation"/> is provided and is one level above the
    /// entity's current elevation, ElevationSystem is asked to confirm the geometry
    /// before the jump is allowed.  On a confirmed elevation jump, wall collisions
    /// are disabled for the duration of the jump.
    ///
    /// Passing no <paramref name="targetElevation"/> performs a plain in-place jump
    /// with no elevation change (no geometry check needed).
    /// </summary>
    /// <param name="targetElevation">The elevation the entity intends to land on, or null for a cosmetic jump.</param>
    /// <param name="impulse">Override the jump speed (pixels/s).  null = derive from jumpHeight.</param>
    public bool TryJump(int? targetElevation = null, float? impulse = null)
    {
        if (!IsGrounded)
            return false;

        // ── Elevation-change validation ───────────────────────────────────────
        if (targetElevation.HasValue)
        {
            var sys = ElevationSystem.Instance;
            if (sys == null)
            {
                GD.PrintErr("[ZAxis] TryJump: ElevationSystem.Instance is null — cannot validate elevation jump.");
                return false;
            }

            int currentElev = entity.CurrentElevation;
            int target      = targetElevation.Value;

            if (target == currentElev + 1)
            {
                bool canJump = sys.CanJumpUp(entity.GlobalPosition, currentElev, target);
                GD.Print($"[ZAxis] Elevation jump {currentElev}→{target}: CanJumpUp={canJump} pos={entity.GlobalPosition}");

                if (!canJump)
                    return false;

                // Disable the wall collision layer unconditionally for this leap.
                forceWallsOff = true;
                GD.Print($"[ZAxis] forceWallsOff=true, disabling collision mask layer {WallCollisionLayerNumber}");
            }
            else if (target != currentElev)
            {
                return false;
            }
        }

        // ── Execute jump ──────────────────────────────────────────────────────
        // Ensure jump height is always at least one full elevation band so the
        // entity can physically reach the upper floor (32 px minimum).
        float effectiveHeight = Mathf.Max(jumpHeight, ElevationSystem.ELEVATION_HEIGHT);
        float speed           = impulse ?? Mathf.Sqrt(2f * gravity * effectiveHeight);

        zVelocity = speed;
        z         = Mathf.Max(z, floorZ);

        // Apply the mask change immediately — this frame's ProcessVertical has
        // already run, so this will persist until next frame when z > floorZ.
        UpdateWallCollisionMask();

        GD.Print($"[ZAxis] Jump executed: speed={speed:F1} effectiveHeight={effectiveHeight} forceWallsOff={forceWallsOff} maskLayer{WallCollisionLayerNumber}={entity.GetCollisionMaskValue(WallCollisionLayerNumber)}");

        entity.OnZChanged();
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Step-down (elevation-aware, no jump required)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempts to step the entity down one elevation level.
    /// Confirms geometry via ElevationSystem, then lowers <see cref="FloorZ"/>
    /// and disables wall collisions briefly so the entity passes through the ledge.
    ///
    /// Returns true if the step-down was initiated.
    /// </summary>
    public bool TryStepDown()
    {
        var sys = ElevationSystem.Instance;
        if (sys == null)
            return false;

        int currentElev = entity.CurrentElevation;
        int targetElev  = currentElev - 1;

        if (!sys.CanStepDown(entity.GlobalPosition, currentElev, targetElev))
            return false;

        // Phase through walls during descent.
        forceWallsOff = true;
        UpdateWallCollisionMask();

        // Lower the floor so the entity drops.
        float newFloorZ = ElevationSystem.GetElevationFloorZ(targetElev);
        SetFloorZ(newFloorZ);

        entity.OnZChanged();
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Floor management
    // ═════════════════════════════════════════════════════════════════════════

    public void SetFloorZ(float newFloorZ)
    {
        newFloorZ = Mathf.Max(0f, newFloorZ);
        if (Mathf.IsEqualApprox(floorZ, newFloorZ))
            return;

        floorZ = newFloorZ;

        // Only snap z down when the entity is not actively rising.
        if (z < floorZ && zVelocity <= 0f)
        {
            z         = floorZ;
            zVelocity = 0f;
        }

        UpdateWallCollisionMask();
        entity.OnZChanged();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wall-collision mask helpers
    // ═════════════════════════════════════════════════════════════════════════

    public void SetForceWallsOff(bool off)
    {
        forceWallsOff = off;
        UpdateWallCollisionMask();
    }

    private void UpdateWallCollisionMask()
    {
        if (!IgnoreWallsWhenAirborne)
            return;

        // forceWallsOff is set explicitly for elevation jumps — always honour it.
        // Otherwise, disable the wall layer whenever the entity is above the floor.
        // We do NOT gate on whether the entity originally had the layer enabled;
        // for elevation jumps we must force it off unconditionally.
        if (forceWallsOff)
        {
            entity.SetCollisionMaskValue(WallCollisionLayerNumber, false);
            return;
        }

        // Normal airborne wall-phasing (non-elevation jump).
        bool isAirborne = z > floorZ + airborneEpsilon;
        entity.SetCollisionMaskValue(WallCollisionLayerNumber, !isAirborne);
    }
}