using Godot;

/// <summary>
/// Manages the vertical (Z-axis) dimension for entities, handling gravity, jumping, and airborne state.
/// </summary>
public class ZAxis
{
    private Entity entity;

    // Z-axis state
    private float z = 0f;
    private float zVelocity = 0f;
    private float floorZ = 0f;

    // Z-axis configuration
    private float jumpHeight = 20f;
    private float gravity = 900f;
    private float terminalVelocity = 3000f;
    private bool ignoreWallsWhenAirborne = true;
    private int wallCollisionLayerNumber = 6;
    private float airborneEpsilon = 0.1f;

    // Wall collision tracking
    private bool captureBaseWallMask;
    private bool baseWallMaskValue;

    public float Z => z;
    public float ZVelocity => zVelocity;
    public float FloorZ => floorZ;
    public float JumpHeight { get => jumpHeight; set => jumpHeight = value; }
    public float Gravity { get => gravity; set => gravity = value; }
    public float TerminalVelocity { get => terminalVelocity; set => terminalVelocity = value; }
    public bool IgnoreWallsWhenAirborne { get => ignoreWallsWhenAirborne; set => ignoreWallsWhenAirborne = value; }
    public int WallCollisionLayerNumber { get => wallCollisionLayerNumber; set => wallCollisionLayerNumber = value; }
    public float AirborneEpsilon { get => airborneEpsilon; set => airborneEpsilon = value; }

    public bool IsGrounded => z <= floorZ + 0.01f && zVelocity <= 0.01f;
    public bool IsAirborne => !IsGrounded;

    /// <summary>
    /// Creates a new ZAxis instance for an entity.
    /// </summary>
    public ZAxis(Entity ownerEntity, float initialFloorZ = 0f, float initialJumpHeight = 20f, 
                 float initialGravity = 900f, float initialTerminalVelocity = 3000f)
    {
        entity = ownerEntity;
        floorZ = Mathf.Max(0f, initialFloorZ);
        z = floorZ;
        jumpHeight = Mathf.Max(0f, initialJumpHeight);
        gravity = Mathf.Max(0f, initialGravity);
        terminalVelocity = Mathf.Max(0f, initialTerminalVelocity);
    }

    /// <summary>
    /// Processes vertical (Z-axis) physics for this frame.
    /// </summary>
    public void ProcessVertical(float delta)
    {
        if (z < floorZ)
        {
            z = floorZ;
            zVelocity = 0f;
        }

        if (IsGrounded && zVelocity <= 0f)
        {
            UpdateWallCollisionMask();
            return;
        }

        zVelocity -= gravity * delta;
        if (zVelocity < -terminalVelocity)
            zVelocity = -terminalVelocity;

        z += zVelocity * delta;

        float targetZ = floorZ + Mathf.Max(0f, jumpHeight);
        if (zVelocity > 0f && z >= targetZ)
        {
            z = targetZ;
            zVelocity = 0f;
        }

        if (z <= floorZ)
        {
            z = floorZ;
            zVelocity = 0f;
        }

        UpdateWallCollisionMask();
        entity.OnZChanged();
    }

    /// <summary>
    /// Attempts to make the entity jump.
    /// </summary>
    public bool TryJump(float? impulse = null)
    {
        if (!IsGrounded)
            return false;

        float desiredHeight = Mathf.Max(0f, jumpHeight);
        float jumpSpeed = Mathf.Sqrt(2f * gravity * desiredHeight);

        if (impulse.HasValue)
            jumpSpeed = impulse.Value;

        zVelocity = jumpSpeed;
        z = Mathf.Max(z, floorZ);
        UpdateWallCollisionMask();
        entity.OnZChanged();
        return true;
    }

    /// <summary>
    /// Sets the floor Z value (ground level) for this entity.
    /// </summary>
    public void SetFloorZ(float newFloorZ)
    {
        newFloorZ = Mathf.Max(0f, newFloorZ);
        if (Mathf.IsEqualApprox(floorZ, newFloorZ))
            return;

        floorZ = newFloorZ;

        if (z < floorZ)
        {
            z = floorZ;
            zVelocity = 0f;
        }

        UpdateWallCollisionMask();
        entity.OnZChanged();
    }

    /// <summary>
    /// Updates the wall collision mask based on airborne state.
    /// </summary>
    private void UpdateWallCollisionMask()
    {
        if (!ignoreWallsWhenAirborne)
            return;

        CaptureBaseWallMaskIfNeeded();
        if (!baseWallMaskValue)
            return;

        bool airborne = z > floorZ + airborneEpsilon;
        entity.SetCollisionMaskValue(wallCollisionLayerNumber, !airborne);
    }

    /// <summary>
    /// Captures the base wall collision mask value on first check.
    /// </summary>
    private void CaptureBaseWallMaskIfNeeded()
    {
        if (captureBaseWallMask)
            return;

        baseWallMaskValue = entity.GetCollisionMaskValue(wallCollisionLayerNumber);
        captureBaseWallMask = true;
    }
}
