using Godot;

public partial class Entity : CharacterBody2D
{
    [Export] public float Weight { get; set; } = 1f;

    [ExportGroup("Health")]
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float Health { get; set; } = 100f;

    [ExportGroup("Z Axis")]
    [Export(PropertyHint.Range, "0,4096,1")] public float FloorZ { get; private set; } = 0f;
    [Export(PropertyHint.Range, "0,256,1")] public float JumpHeight { get; set; } = 20f;
    [Export(PropertyHint.Range, "0,4096,1")] public float Gravity { get; set; } = 900f;
    [Export(PropertyHint.Range, "0,8192,1")] public float TerminalVelocity { get; set; } = 3000f;

    [ExportGroup("Z Collision")]
    [Export] public bool IgnoreWallsWhenAirborne { get; set; } = true;
    [Export(PropertyHint.Range, "1,32,1")] public int WallCollisionLayerNumber { get; set; } = 6;
    [Export(PropertyHint.Range, "0,4096,0.1")] public float AirborneEpsilon { get; set; } = 0.1f;

    private bool _capturedBaseWallMask;
    private bool _baseWallMaskValue;

    protected Control ResourceBarControl;
    protected ProgressBar HealthBar;
    [Export] public AnimationPlayer AnimationPlayer { get; set; }

    public float Z { get; private set; } = 0f;
    public float ZVelocity { get; private set; } = 0f;

    public bool IsGrounded => Z <= FloorZ + 0.01f && ZVelocity <= 0.01f;
    public bool IsAirborne => !IsGrounded;

    public virtual void TakeDamage(float damage, Vector2 sourcePosition)
    {
        if (damage <= 0f || !CanTakeDamage(damage, sourcePosition))
            return;

        float effectiveDamage = Mathf.Max(0f, ApplyDamageModifiers(damage, sourcePosition));
        if (effectiveDamage <= 0f)
            return;

        float currentHealth = GetHealth();
        float newHealth = Mathf.Max(0f, currentHealth - effectiveDamage);

        SetHealth(newHealth);
        OnDamageTaken(effectiveDamage, sourcePosition, currentHealth, newHealth);

        if (newHealth <= 0f)
            OnDeath(sourcePosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessVertical((float)delta);
    }

    public override void _Ready()
    {
        CaptureBaseWallMaskIfNeeded();
        UpdateWallCollisionMask();
    }

    public void SetFloorZ(float newFloorZ)
    {
        newFloorZ = Mathf.Max(0f, newFloorZ);
        if (Mathf.IsEqualApprox(FloorZ, newFloorZ))
            return;

        FloorZ = newFloorZ;

        if (Z < FloorZ)
        {
            Z = FloorZ;
            ZVelocity = 0f;
        }

        UpdateWallCollisionMask();
        OnZChanged();
    }

    public bool TryJump(float? impulse = null)
    {
        if (!IsGrounded)
            return false;

        float desiredHeight = Mathf.Max(0f, JumpHeight);
        float jumpSpeed = Mathf.Sqrt(2f * Gravity * desiredHeight);

        if (impulse.HasValue)
            jumpSpeed = impulse.Value;

        ZVelocity = jumpSpeed;
        Z = Mathf.Max(Z, FloorZ);
        UpdateWallCollisionMask();
        OnZChanged();
        return true;
    }

    protected void ProcessVertical(float delta)
    {
        if (Z < FloorZ)
        {
            Z = FloorZ;
            ZVelocity = 0f;
        }

        if (IsGrounded && ZVelocity <= 0f)
        {
            UpdateWallCollisionMask();
            return;
        }

        ZVelocity -= Gravity * delta;
        if (ZVelocity < -TerminalVelocity)
            ZVelocity = -TerminalVelocity;

        Z += ZVelocity * delta;

        float targetZ = FloorZ + Mathf.Max(0f, JumpHeight);
        if (ZVelocity > 0f && Z >= targetZ)
        {
            Z = targetZ;
            ZVelocity = 0f;
        }

        if (Z <= FloorZ)
        {
            Z = FloorZ;
            ZVelocity = 0f;
        }

        UpdateWallCollisionMask();
        OnZChanged();
    }

    public void InitializeResourceBars()
    {
        ResourceBarControl = FindResourceBarControl();
        HealthBar = GetNodeOrNull<ProgressBar>("Health Bar")
                    ?? FindProgressBar(ResourceBarControl, "Health Bar")
                    ?? FindProgressBar(this, "Resource Bar/Health Bar")
                    ?? FindProgressBar(this, "Enemy Resource Bar/Health Bar")
                    ?? FindProgressBar(this, "Enemy Health Bar");

        UpdateResourceBars();
    }

    protected virtual float GetHealth() => Health;

    protected virtual float GetMaxHealth() => MaxHealth;

    protected virtual void SetHealth(float value)
    {
        Health = Mathf.Clamp(value, 0f, GetMaxHealth());
        UpdateResourceBars();
    }

    protected virtual void SetMaxHealth(float value)
    {
        MaxHealth = Mathf.Max(0f, value);
        UpdateResourceBars();
    }

    protected virtual bool CanTakeDamage(float damage, Vector2 sourcePosition) => true;

    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition) => damage;

    protected virtual void OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth) { }

    protected virtual void OnDeath(Vector2 sourcePosition) { }

    protected virtual void UpdateResourceBars()
    {
        if (HealthBar == null)
            return;

        float maxHealth = Mathf.Max(GetMaxHealth(), 1f);
        HealthBar.MaxValue = maxHealth;
        HealthBar.Value = Mathf.Clamp(GetHealth(), 0f, maxHealth);
    }

    protected virtual Control FindResourceBarControl()
    {
        foreach (string candidate in GetResourceBarCandidates())
        {
            Control control = GetNodeOrNull<Control>(candidate) ?? FindChildControl(this, candidate);
            if (control != null)
                return control;
        }

        return null;
    }

    protected virtual string[] GetResourceBarCandidates()
    {
        return new[] { "Resource Bar", "Enemy Resource Bar", "Enemy Health Bar" };
    }

    protected Control FindChildControl(Node node, string nodeName)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Control control && child.Name == nodeName)
                return control;

            Control nested = FindChildControl(child, nodeName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    protected ProgressBar FindProgressBar(Node node, string nodePath)
    {
        if (node == null)
            return null;

        Node child = node.GetNodeOrNull(nodePath);
        if (child is ProgressBar progressBar)
            return progressBar;

        return node.FindChild(nodePath, true, false) as ProgressBar;
    }

    private void CaptureBaseWallMaskIfNeeded()
    {
        if (_capturedBaseWallMask)
            return;

        _baseWallMaskValue = GetCollisionMaskValue(WallCollisionLayerNumber);
        _capturedBaseWallMask = true;
    }

    private void UpdateWallCollisionMask()
    {
        if (!IgnoreWallsWhenAirborne)
            return;

        CaptureBaseWallMaskIfNeeded();
        if (!_baseWallMaskValue)
            return;

        bool airborne = Z > FloorZ + AirborneEpsilon;
        SetCollisionMaskValue(WallCollisionLayerNumber, !airborne);
    }

    protected virtual void OnZChanged() { }
}
