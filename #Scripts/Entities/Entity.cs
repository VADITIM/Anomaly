using Godot;

/// <summary>
/// Base class for all game entities.  Handles health, knockback, and
/// coordinates with <see cref="ZAxis"/> and <see cref="ElevationSystem"/>
/// for height / elevation logic.
///
/// Elevation flow:
///   1. Each physics frame <see cref="ProcessElevation"/> is called.
///   2. When grounded, the entity's CurrentElevation is derived from FloorZ.
///   3. When airborne, the system detects if the entity has risen past the
///      threshold for a higher elevation level and sets IsPhasingElevation.
///   4. On landing, IsPhasingElevation is cleared, CurrentElevation is updated,
///      and wall collisions are restored.
/// </summary>
public partial class Entity : CharacterBody2D
{
    // ── Knockback ─────────────────────────────────────────────────────────────
    [Export] public bool  canBeKnockedBack  { get; set; } = true;
    /// <summary>Alias kept for backwards-compatibility.</summary>
    [Export] public bool  canBeKnockbacked  { get => canBeKnockedBack; set => canBeKnockedBack = value; }
    [Export] public float weight            { get; set; } = 1f;
    [Export] public float knockbackDecay    { get; set; } = 2000f;

    protected Vector2 knockbackVelocity = Vector2.Zero;
    protected float   knockbackDuration = 0f;

    // ── Misc exports ──────────────────────────────────────────────────────────
    [Export] public AnimationPlayer AnimationPlayer { get; set; }

    // ── Health ────────────────────────────────────────────────────────────────
    [ExportGroup("Health")]
    [Export] public float maxHealth { get; set; } = 99999f;
    [Export] public float health    { get; set; }

    // ── Z-Axis parameters (forwarded to ZAxis instance) ───────────────────────
    [ExportGroup("Z Axis")]
    [Export(PropertyHint.Range, "0,256,1")]
    public float JumpHeight
    {
        get => zAxis?.JumpHeight ?? 20f;
        set { if (zAxis != null) zAxis.JumpHeight = value; }
    }

    [Export(PropertyHint.Range, "0,4096,1")]
    public float Gravity
    {
        get => zAxis?.Gravity ?? 900f;
        set { if (zAxis != null) zAxis.Gravity = value; }
    }

    [Export(PropertyHint.Range, "0,8192,1")]
    public float TerminalVelocity
    {
        get => zAxis?.TerminalVelocity ?? 3000f;
        set { if (zAxis != null) zAxis.TerminalVelocity = value; }
    }

    // ── Z-Collision parameters ────────────────────────────────────────────────
    [ExportGroup("Z Collision")]
    [Export]
    public bool IgnoreWallsWhenAirborne
    {
        get => zAxis?.IgnoreWallsWhenAirborne ?? true;
        set { if (zAxis != null) zAxis.IgnoreWallsWhenAirborne = value; }
    }

    [Export(PropertyHint.Range, "1,32,1")]
    public int WallCollisionLayerNumber
    {
        get => zAxis?.WallCollisionLayerNumber ?? 9;
        set { if (zAxis != null) zAxis.WallCollisionLayerNumber = value; }
    }

    [Export(PropertyHint.Range, "0,4096,0.1")]
    public float AirborneEpsilon
    {
        get => zAxis?.AirborneEpsilon ?? 0.1f;
        set { if (zAxis != null) zAxis.AirborneEpsilon = value; }
    }

    // ── Runtime Z state (read-only convenience) ───────────────────────────────
    public float Z         => zAxis?.Z         ?? 0f;
    public float ZVelocity => zAxis?.ZVelocity ?? 0f;
    public float FloorZ    => zAxis?.FloorZ    ?? 0f;

    public bool IsGrounded => zAxis?.IsGrounded ?? false;
    public bool IsAirborne => zAxis?.IsAirborne ?? false;

    // ── Elevation state ───────────────────────────────────────────────────────
    /// <summary>The elevation index the entity currently stands on (grounded).</summary>
    public int  CurrentElevation   { get; protected set; } = 0;

    /// <summary>
    /// True while the entity is airborne AND has risen above the threshold
    /// for a higher elevation level (i.e. it is phasing through walls).
    /// </summary>
    public bool IsPhasingElevation { get; protected set; } = false;

    /// <summary>Elevation the entity was on when it started its current jump.</summary>
    private int _jumpStartElevation = 0;

    // ── Internal references ───────────────────────────────────────────────────
    protected ZAxis    zAxis;
    protected Control  ResourceBarControl;
    protected TextureProgressBar HealthBar;

    // ═════════════════════════════════════════════════════════════════════════
    //  Virtual hooks for subclasses
    // ═════════════════════════════════════════════════════════════════════════

    protected virtual bool  CanTakeDamage(float damage, Vector2 sourcePosition)                                          => true;
    protected virtual float ApplyDamageModifiers(float damage, Vector2 sourcePosition)                                   => damage;
    protected virtual void  OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth)   { }
    protected virtual void  OnDeath(Vector2 sourcePosition)                                                              { }
    protected virtual void  OnKnockbackFinished()                                                                        { }

    /// <summary>Called when the entity's CurrentElevation index changes.</summary>
    protected virtual void OnElevationChanged(int previousElevation, int newElevation) { }

    /// <summary>Called the first frame the entity becomes "phasing" (rising past a ledge threshold).</summary>
    protected virtual void OnElevationPhaseStarted() { }

    /// <summary>Called when phasing ends (landed, or descended back below the threshold).</summary>
    protected virtual void OnElevationPhaseEnded() { }

    /// <summary>Called by ZAxis whenever Z changes.</summary>
    public virtual void OnZChanged() { }

    protected virtual float GetHealth()               => health;
    protected virtual void  SetHealth(float value)    { health = Mathf.Clamp(value, 0f, GetMaxHealth()); UpdateResourceBars(); }
    protected virtual float GetMaxHealth()            => maxHealth;
    protected virtual void  SetMaxHealth(float value) { maxHealth = Mathf.Max(0f, value); UpdateResourceBars(); }

    // ═════════════════════════════════════════════════════════════════════════
    //  Godot lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        zAxis = new ZAxis(this, initialFloorZ: ElevationSystem.GetElevationFloorZ(CurrentElevation));
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessElevation();
        zAxis?.ProcessVertical((float)delta);
        ProcessKnockback((float)delta);
        YSortSystem.Update(this);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Elevation processing  (called every physics frame)
    // ═════════════════════════════════════════════════════════════════════════

    protected void ProcessElevation()
    {
        if (ElevationSystem.Instance == null)
            return;

        if (IsAirborne)
            ProcessElevationAirborne();
        else
            ProcessElevationGrounded();
    }

    // ── Airborne ─────────────────────────────────────────────────────────────

    private void ProcessElevationAirborne()
    {
        var sys = ElevationSystem.Instance;

        // The entity is phasing when it has risen strictly above the ledge wall
        // of its starting elevation (i.e. Z has cleared the floor-height of the
        // next elevation level).  We use a 50% threshold so the entity is
        // considered "over the wall" halfway through the elevation band.
        float phaseThreshold = (_jumpStartElevation + 0.5f) * ElevationSystem.ELEVATION_HEIGHT;
        bool shouldPhase     = Z > phaseThreshold;

        if (shouldPhase && !IsPhasingElevation)
        {
            IsPhasingElevation = true;
            OnElevationPhaseStarted();
        }
        else if (!shouldPhase && IsPhasingElevation)
        {
            IsPhasingElevation = false;
            OnElevationPhaseEnded();
        }

        // Once the entity has fully cleared the ledge threshold, raise the floor
        // so it can land on the upper elevation.  We only do this when:
        //   • the entity is still rising (ZVelocity > 0), so we don't raise the
        //     floor prematurely on the way down and cause an instant re-land.
        //   • ground tiles exist at the target elevation near the XY position.
        if (ZVelocity > 0f)
        {
            int   targetElev  = _jumpStartElevation + 1;
            float targetFloorZ = ElevationSystem.GetElevationFloorZ(targetElev);

            if (targetElev <= ElevationSystem.MAX_ELEVATION
                && targetFloorZ > FloorZ
                && sys.HasGroundAt(GlobalPosition, targetElev, radius: 2))
            {
                zAxis?.SetFloorZ(targetFloorZ);
            }
        }
    }

    // ── Grounded ──────────────────────────────────────────────────────────────

    private void ProcessElevationGrounded()
    {
        // Clear phasing the moment we touch down.
        if (IsPhasingElevation)
        {
            IsPhasingElevation = false;
            OnElevationPhaseEnded();
        }

        // Derive the authoritative elevation from where ZAxis placed the floor.
        int groundedElev = Mathf.RoundToInt(FloorZ / ElevationSystem.ELEVATION_HEIGHT);
        groundedElev     = Mathf.Clamp(groundedElev, 0, ElevationSystem.MAX_ELEVATION);

        if (groundedElev != CurrentElevation)
        {
            int prev            = CurrentElevation;
            CurrentElevation    = groundedElev;
            _jumpStartElevation = groundedElev;
            OnElevationChanged(prev, groundedElev);
            // Notify subclasses (e.g. Player.OnZChanged) so the visual sprite
            // offset snaps to the new elevation floor on the landing frame.
            OnZChanged();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Public jump API
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempts a jump.  If <paramref name="targetElevation"/> is one above the
    /// current elevation, the jump is validated against ElevationSystem geometry
    /// (E{current} Wall + E{target} Ground must be adjacent).  On success, wall
    /// collisions are automatically suspended for the leap.
    ///
    /// For plain cosmetic jumps (no elevation change) pass null or omit the parameter.
    /// </summary>
    public virtual bool TryJump(int? targetElevation = null, float? impulse = null)
    {
        bool jumped = zAxis?.TryJump(targetElevation, impulse) ?? false;
        if (jumped)
            _jumpStartElevation = CurrentElevation;
        return jumped;
    }

    /// <summary>
    /// Attempts to step the entity down one elevation level (no jump).
    /// ElevationSystem must confirm E{current-1} Wall + E{current-1} Ground
    /// are adjacent to the entity's position.
    /// </summary>
    public virtual bool TryStepDown()
    {
        bool stepped = zAxis?.TryStepDown() ?? false;
        if (stepped)
            _jumpStartElevation = CurrentElevation - 1; // Will be confirmed on landing.
        return stepped;
    }

    /// <summary>Directly sets the entity's floor height and updates elevation accordingly.</summary>
    public void SetFloorZ(float newFloorZ)
    {
        zAxis?.SetFloorZ(newFloorZ);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Knockback
    // ═════════════════════════════════════════════════════════════════════════

    protected void ProcessKnockback(float delta)
    {
        if (!canBeKnockbacked) return;

        if (knockbackDuration > 0 || knockbackVelocity.Length() > 0.1f)
        {
            knockbackDuration -= delta;
            Velocity           = knockbackVelocity;
            MoveAndSlide();
            knockbackVelocity  = knockbackVelocity.MoveToward(Vector2.Zero, knockbackDecay * delta);

            if (knockbackDuration <= 0 && knockbackVelocity.Length() < 1f)
            {
                knockbackVelocity = Vector2.Zero;
                OnKnockbackFinished();
            }
        }
    }

    public virtual void ApplyKnockback(Vector2 direction, float force, float duration = 0.2f)
    {
        float effectiveForce = force / Mathf.Max(weight, 0.1f);
        knockbackVelocity    = direction.Normalized() * effectiveForce;
        knockbackDuration    = duration;
    }

    public virtual void TakeKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (!canBeKnockbacked) return;
        Vector2 dir = (GlobalPosition - sourcePosition).Normalized();
        ApplyKnockback(dir, force, duration);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Damage
    // ═════════════════════════════════════════════════════════════════════════

    public virtual void TakeDamage(float damage, Vector2 sourcePosition)
    {
        if (damage <= 0f || !CanTakeDamage(damage, sourcePosition))
            return;

        float effectiveDamage = Mathf.Max(0f, ApplyDamageModifiers(damage, sourcePosition));
        if (effectiveDamage <= 0f)
            return;

        float currentHealth = GetHealth();
        float newHealth     = Mathf.Max(0f, currentHealth - effectiveDamage);

        SetHealth(newHealth);
        SpawnDamageNumber(effectiveDamage, DamageNumberStyle.Standard);
        OnDamageTaken(effectiveDamage, sourcePosition, currentHealth, newHealth);

        if (newHealth <= 0f)
            OnDeath(sourcePosition);
    }

    public virtual void TakeDamage(WeaponArc weapon, Node2D damageSource)
    {
        if (weapon == null) return;
        Vector2 sourcePosition  = damageSource?.GlobalPosition ?? GlobalPosition;
        TakeDamage(weapon.Damage, sourcePosition);
        TakeKnockback(sourcePosition, weapon.Knockback);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Resource bars
    // ═════════════════════════════════════════════════════════════════════════

    public virtual void InitializeBars() => InitializeResourceBars();

    public void InitializeResourceBars()
    {
        (ResourceBarControl, HealthBar) = InitializeEntity.InitializeResourceBars(this);
        UpdateResourceBars();
    }

    protected virtual void UpdateResourceBars()
    {
        if (HealthBar == null) return;
        float max       = Mathf.Max(GetMaxHealth(), 1f);
        HealthBar.MaxValue = max;
        HealthBar.Value    = Mathf.Clamp(GetHealth(), 0f, max);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Misc
    // ═════════════════════════════════════════════════════════════════════════

    protected void SpawnDamageNumber(float damageAmount, DamageNumberStyle style)
        => DamageNumberSpawner.Spawn(this, damageAmount, style);

    public virtual void PlayAnimation(string animName)
    {
        if (AnimationPlayer == null || !AnimationPlayer.HasAnimation(animName)) return;
        if (AnimationPlayer.CurrentAnimation != animName)
            AnimationPlayer.Play(animName);
    }
}