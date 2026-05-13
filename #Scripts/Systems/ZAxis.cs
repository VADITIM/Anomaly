using Godot;

/// <summary>
/// Base vertical-motion component used by entities.
/// Elevation extends this class to add tilemap-aware elevation transitions.
/// </summary>
public class ZAxis
{
    protected readonly Entity entity;

    protected float z = 0f;
    protected float zVelocity = 0f;
    protected float floorZ = 0f;

    protected float jumpHeight = 34f;
    protected float gravity = 900f;
    protected float terminalVelocity = 3000f;
    protected float airborneEpsilon = 0.1f;

    protected bool ignoreWallsWhenAirborne = true;
    protected int wallCollisionLayerNumber = 10;
    protected bool forceWallsOff = false;

    public virtual float Z => z;
    public virtual float ZVelocity => zVelocity;
    public virtual float FloorZ => floorZ;
    public virtual float JumpHeight { get => jumpHeight; set => jumpHeight = Mathf.Max(0f, value); }
    public virtual float Gravity { get => gravity; set => gravity = Mathf.Max(0f, value); }
    public virtual float TerminalVelocity { get => terminalVelocity; set => terminalVelocity = Mathf.Max(0f, value); }
    public virtual float AirborneEpsilon { get => airborneEpsilon; set => airborneEpsilon = Mathf.Max(0f, value); }
    public virtual bool IgnoreWallsWhenAirborne { get => ignoreWallsWhenAirborne; set => ignoreWallsWhenAirborne = value; }
    public virtual int WallCollisionLayerNumber { get => wallCollisionLayerNumber; set => wallCollisionLayerNumber = value; }

    public virtual bool IsGrounded => z <= floorZ + 0.01f && zVelocity <= 0.01f;
    public virtual bool IsAirborne => !IsGrounded;
}