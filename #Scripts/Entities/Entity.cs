using Godot;

public partial class Entity : CharacterBody2D
{
    [Export] public bool canBeKnockedBack { get; set; } = true;
    [Export] public float weight { get; set; } = 1f;
    [Export] public float knockbackDecay { get; set; } = 2000f;
    protected Vector2 knockbackVelocity = Vector2.Zero;
    protected float knockbackDuration = 0f;
    [Export] public AnimationPlayer AnimationPlayer { get; set; }

    [ExportGroup("Health")]
    [Export] public float maxHealth { get; set; } = 99999f;
    [Export] public float health { get; set; }

    [ExportGroup("Z Axis")]
    [Export(PropertyHint.Range, "0,4096,1")] public float FloorZ { get; private set; } = 0f;
    [Export(PropertyHint.Range, "0,256,1")] public float JumpHeight { get; set; } = 20f;
    [Export(PropertyHint.Range, "0,4096,1")] public float Gravity { get; set; } = 900f;
    [Export(PropertyHint.Range, "0,8192,1")] public float TerminalVelocity { get; set; } = 3000f;

    [ExportGroup("Z Collision")]
    [Export] public bool IgnoreWallsWhenAirborne { get; set; } = true;
    [Export(PropertyHint.Range, "1,32,1")] public int WallCollisionLayerNumber { get; set; } = 6;
    [Export(PropertyHint.Range, "0,4096,0.1")] public float AirborneEpsilon { get; set; } = 0.1f;

    private bool captureBaseWallMask;
    private bool baseWallMaskValue;

    protected Control ResourceBarControl;
    protected TextureProgressBar HealthBar;

    public float Z { get; private set; } = 0f;
    public float ZVelocity { get; private set; } = 0f;

    public bool IsGrounded => Z <= FloorZ + 0.01f && ZVelocity <= 0.01f;
    public bool IsAirborne => !IsGrounded;

    protected virtual bool CanTakeDamage(float damage, Vector2 sourcePosition) => true;
    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition) => damage;
    protected virtual void OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth) { }
    protected virtual void OnDeath(Vector2 sourcePosition) { }

    protected virtual void OnZChanged() { }

    protected virtual void OnKnockbackFinished() { }

    protected virtual float GetHealth() => health;
    protected virtual void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, GetMaxHealth());
        UpdateResourceBars();
    }

    protected virtual float GetMaxHealth() => maxHealth;
    protected virtual void SetMaxHealth(float value)
    {
        maxHealth = Mathf.Max(0f, value);
        UpdateResourceBars();
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessVertical((float)delta);
        ProcessKnockback((float)delta);
    }

    public override void _Ready()
    {
        CaptureBaseWallMaskIfNeeded();
        UpdateWallCollisionMask();
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

    protected void ProcessKnockback(float delta)
    {
        if (!canBeKnockedBack) return;

        if (knockbackDuration > 0 || knockbackVelocity.Length() > 0.1f)
        {
            knockbackDuration -= delta;
            
            Velocity = knockbackVelocity;
            MoveAndSlide();

            knockbackVelocity = knockbackVelocity.MoveToward(Vector2.Zero, knockbackDecay * delta);
            
            if (knockbackDuration <= 0 && knockbackVelocity.Length() < 1f)
            {
                knockbackVelocity = Vector2.Zero;
                OnKnockbackFinished();
            }
        }
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

    protected TextureProgressBar FindTextureProgressBar(Node node, string nodePath)
    {
        if (node == null)
            return null;

        Node child = node.GetNodeOrNull(nodePath);
        if (child is TextureProgressBar textureProgressBar)
            return textureProgressBar;

        return node.FindChild(nodePath, true, false) as TextureProgressBar;
    }

    protected virtual string[] GetResourceBarCandidates()
    {
        return new[] { "Resource Bar", "Enemy Resource Bar", "Enemy Health Bar" };
    }

    public void InitializeResourceBars()
    {
        ResourceBarControl = FindResourceBarControl();
        HealthBar = GetNodeOrNull<TextureProgressBar>("Health Bar")
                    ?? FindTextureProgressBar(ResourceBarControl, "Health Bar")
                    ?? FindTextureProgressBar(this, "Resource Bar/Health Bar")
                    ?? FindTextureProgressBar(this, "Enemy Resource Bar/Health Bar")
                    ?? FindTextureProgressBar(this, "Prop Resource Bar/Health Bar");

        UpdateResourceBars();
    }

    protected virtual void UpdateResourceBars()
    {
        if (HealthBar == null)
            return;

        float maxHealth = Mathf.Max(GetMaxHealth(), 1f);
        HealthBar.MaxValue = maxHealth;
        HealthBar.Value = Mathf.Clamp(GetHealth(), 0f, maxHealth);
    }


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

    public virtual void ApplyKnockback(Vector2 direction, float force, float duration = 0.2f)
    {
        // Weight influence: force is divided by weight (Weight 1.0 = 100% force, 2.0 = 50%)
        float effectiveForce = force / Mathf.Max(weight, 0.1f);
        
        knockbackVelocity = direction.Normalized() * effectiveForce;
        knockbackDuration = duration;
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

    private void UpdateWallCollisionMask()
    {
        if (!IgnoreWallsWhenAirborne)
            return;

        CaptureBaseWallMaskIfNeeded();
        if (!baseWallMaskValue)
            return;

        bool airborne = Z > FloorZ + AirborneEpsilon;
        SetCollisionMaskValue(WallCollisionLayerNumber, !airborne);
    }


    private void CaptureBaseWallMaskIfNeeded()
    {
        if (captureBaseWallMask)
            return;

        baseWallMaskValue = GetCollisionMaskValue(WallCollisionLayerNumber);
        captureBaseWallMask = true;
    }



    public virtual void PlayAnimation(string animName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animName))
            return;

        if (AnimationPlayer.CurrentAnimation != animName)
        {
            AnimationPlayer.Play(animName);
        }
    }
}
