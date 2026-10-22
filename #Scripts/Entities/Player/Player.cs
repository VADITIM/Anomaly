using Godot;

namespace Anomaly;

public enum PlayerState
{
    Locomotion,
    Dash,
    Attack,
    Special,
    Charge,
    Heal,
    Hurt,
    Falling,
    Dead,
}

public enum BufferedAction
{
    None,
    Attack,
    Special,
    Dash,
    Jump,
    Heal,
}

public partial class Player : Entity
{
    private const float INPUT_BUFFER_TIME = 0.2f;
    private const float FOOTSTEP_INTERVAL = 0.27f;
    private const float CARRIED_LIGHT_ENERGY = 0.7f;

    [Export] public float MoveSpeed = 124f;
    [Export] public float Acceleration = 1200f;
    [Export] public float Deceleration = 1700f;
    [Export] public float AirControl = 0.6f;
    [Export] public float JumpVelocity = 345f;
    [Export] public float DashSpeed = 310f;
    [Export] public float DashDuration = 0.12f;
    [Export] public float DashInvulnerability = 0.2f;
    [Export] public float DashStaminaCost = 24f;
    [Export] public float BaseStamina = 80f;
    [Export] public float BaseStaminaRegen = 28f;
    [Export] public float StaminaRegenDelay = 0.85f;
    [Export] public float ExhaustedRegenDelay = 1.5f;
    [Export] public float BasePotency = 35f;
    [Export] public float HealChannelTime = 0.42f;
    [Export] public SoulWeaponArc CurrentArc;

    public StateMachine<PlayerState> StateMachine { get; private set; }
    public PlayerAnimator Animator { get; private set; }
    public Sprite2D Sprite => Animator.BodySprite;
    public TelekineticScythe Scythe { get; private set; }
    public bool InCombat { get; private set; }
    public bool IsAttacking => StateMachine.Is(PlayerState.Attack) || StateMachine.Is(PlayerState.Special) || StateMachine.Is(PlayerState.Charge);
    public Vector2 Facing { get; private set; } = Vector2.Down;
    public Vector2 AimDirection { get; private set; } = Vector2.Down;
    public bool IsInvulnerable => invulnerableTimer > 0f;
    public bool IsHealing => StateMachine.Is(PlayerState.Heal);
    public bool IsExhausted => Stamina < DashStaminaCost * 0.5f;
    public float MaxStamina => LevelingSystem.MaxStamina(SaveManager.PlayerStats, BaseStamina);
    public float StaminaRegenRate => LevelingSystem.StaminaRegen(SaveManager.PlayerStats, BaseStaminaRegen) * Amulets.Multiplier(AmuletStat.StaminaRegen);

    protected override string DefaultStatsPath => "res://#Resources/EntityStats/Player.tres";
    public override bool CanReceivePlayerHits => false;

    private BufferedAction bufferedAction;
    private float bufferTimer;
    private float animationTime;
    private float footstepTimer;
    private float invulnerableTimer;
    private PointLight2D carriedLight;
    private Vector2 lastSafePosition;
    private Vector2 targetBodyScale = Vector2.One;
    private Vector2 dashDirection;
    private float afterImageTimer;
    private bool perfectDodgeAvailable;
    private float stateTimer;
    private IInteractable focusedInteractable;

    protected override void InitializeEntity()
    {
        Radius = 5f;
        HurtRadius = 7f;
        HeightSpan = 24f;

        Animator = new PlayerAnimator();
        AddChild(Animator);
        Animator.Build(Body, EntityMaterial);
        Scythe = new TelekineticScythe();
        Body.AddChild(Scythe);
        Scythe.Setup(this);

        carriedLight = LightFactory.Point(new Color(0.82f, 0.9f, 1f), CARRIED_LIGHT_ENERGY, 1.05f, new Vector2(0, -10));
        Body.AddChild(carriedLight);

        BuildStateMachine();
        InitializeResources();
        lastSafePosition = Position;
    }

    protected override float ComputeMaxHealth() => SaveManager.PlayerStats.EffectiveMaxHealth(Stats);

    private void BuildStateMachine()
    {
        StateMachine = new StateMachine<PlayerState>();
        StateMachine.Add(PlayerState.Locomotion, update: UpdateLocomotion);
        StateMachine.Add(PlayerState.Dash, EnterDash, UpdateDash, ExitDash);
        StateMachine.Add(PlayerState.Attack, EnterAttack, UpdateAttack, ExitAttack);
        StateMachine.Add(PlayerState.Special, EnterSpecial, UpdateSpecial, ExitSpecial);
        StateMachine.Add(PlayerState.Charge, EnterCharge, UpdateCharge, ExitCharge);
        StateMachine.Add(PlayerState.Heal, EnterHeal, UpdateHeal, ExitHeal);
        StateMachine.Add(PlayerState.Hurt, EnterHurt, UpdateHurt);
        StateMachine.Add(PlayerState.Falling, EnterFalling, UpdateFalling);
        StateMachine.Add(PlayerState.Dead, EnterDead, UpdateDead);
        StateMachine.Start(PlayerState.Locomotion);
    }

    protected override void TickEntity(float delta)
    {
        if (invulnerableTimer > 0f)
            invulnerableTimer -= delta;

        ReadInput(delta);
        AimDirection = ComputeAim();
        StateMachine.Update(delta);
        UpdateResources(delta);
        UpdateInteraction();
        TrackSafeGround();
        UpdateAnimation(delta);
        UpdateCombatTimers(delta);
        InCombat = ComputeInCombat();
        UpdateCarriedLight();
    }

    // The carried light fills in only what nearby lights leave dark, so standing by a brazier never doubles the glow.
    private void UpdateCarriedLight()
    {
        float ambient = LightFactory.EnergyAt(carriedLight.GlobalPosition, carriedLight);
        carriedLight.Energy = Mathf.Max(0f, CARRIED_LIGHT_ENERGY - ambient);
    }

    private void ReadInput(float delta)
    {
        if (bufferTimer > 0f)
        {
            bufferTimer -= delta;
            if (bufferTimer <= 0f)
                bufferedAction = BufferedAction.None;
        }
        if (IsDead || World.IsInputBlocked)
            return;

        if (Input.IsActionJustPressed(StringNames.Of(GameInput.ATTACK)))
            Buffer(BufferedAction.Attack);
        else if (Input.IsActionJustPressed(StringNames.Of(GameInput.SPECIAL)))
            Buffer(BufferedAction.Special);
        else if (Input.IsActionJustPressed(StringNames.Of(GameInput.DASH)))
            Buffer(BufferedAction.Dash);
        else if (Input.IsActionJustPressed(StringNames.Of(GameInput.JUMP)))
            Buffer(BufferedAction.Jump);
        else if (Input.IsActionJustPressed(StringNames.Of(GameInput.HEAL)))
            Buffer(BufferedAction.Heal);

        if (Input.IsActionJustPressed(StringNames.Of(GameInput.AETHERIAC)))
            ToggleAetheriacState();
        if (Input.IsActionJustPressed(StringNames.Of(GameInput.INTERACT)) && focusedInteractable != null && StateMachine.Is(PlayerState.Locomotion))
            focusedInteractable.Interact(this);
    }

    private void Buffer(BufferedAction action)
    {
        bufferedAction = action;
        bufferTimer = INPUT_BUFFER_TIME;
    }

    private bool Consume(BufferedAction action)
    {
        if (bufferedAction != action)
            return false;
        bufferedAction = BufferedAction.None;
        bufferTimer = 0f;
        return true;
    }

    private Vector2 MoveInput => IsDead || World.IsInputBlocked ? Vector2.Zero : GameInput.MoveVector();

    private Vector2 ComputeAim()
    {
        if (GameInput.AimOverride is Vector2 forced && forced != Vector2.Zero)
            return forced.Normalized();
        if (GameInput.UsingGamepad)
        {
            Vector2 stick = GameInput.AimStick();
            if (stick.Length() > 0.3f)
                return stick.Normalized();
            Vector2 move = GameInput.MoveVector();
            return move.Length() > 0.2f ? move.Normalized() : Facing;
        }
        Vector2 toMouse = GetGlobalMousePosition() - (GlobalPosition + new Vector2(0, -Z - 12f));
        return toMouse.Length() > 2f ? toMouse.Normalized() : Facing;
    }

    private void UpdateLocomotion(float delta)
    {
        ApplyMovement(delta, 1f);

        if (Consume(BufferedAction.Attack) && CanAttack())
        {
            StateMachine.Transition(PlayerState.Attack);
            return;
        }
        if (Consume(BufferedAction.Special) && CanUseSpecial())
        {
            StateMachine.Transition(PlayerState.Special);
            return;
        }
        if (Consume(BufferedAction.Dash) && CanDash())
        {
            StateMachine.Transition(PlayerState.Dash);
            return;
        }
        if (Consume(BufferedAction.Heal) && CanHeal())
        {
            StateMachine.Transition(PlayerState.Heal);
            return;
        }
        if (bufferedAction == BufferedAction.Jump && IsGrounded)
        {
            Consume(BufferedAction.Jump);
            Jump();
        }
    }

    private void ApplyMovement(float delta, float speedScale)
    {
        Vector2 input = MoveInput;
        float control = IsGrounded ? 1f : AirControl;
        float speed = MoveSpeed * Amulets.Multiplier(AmuletStat.MoveSpeed) * speedScale * (AetheriacActive ? 1.1f : 1f) * (GodMode ? GOD_SPEED_SCALE : 1f);
        Vector2 target = input * speed;
        float rate = (input == Vector2.Zero ? Deceleration : Acceleration) * control;
        MoveVelocity = MoveVelocity.MoveToward(target, rate * delta);
        if (input.Length() > 0.1f)
            Facing = input.Normalized();

        if (IsGrounded && MoveVelocity.Length() > 30f)
        {
            footstepTimer -= delta;
            if (footstepTimer <= 0f)
            {
                footstepTimer = FOOTSTEP_INTERVAL;
                if (GD.Randf() < 0.5f)
                    Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 2, new Color(0.75f, 0.72f, 0.6f, 0.6f), 22f, -MoveVelocity, 50f, 0.3f, -10f, 1.5f, 40f);
            }
        }
    }

    private void Jump()
    {
        Launch(JumpVelocity);
        Sfx.Play("jump", -4f);
        targetBodyScale = new Vector2(0.75f, 1.3f);
        Body.Scale = targetBodyScale;
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 6, new Color(0.8f, 0.78f, 0.7f, 0.7f), 40f, Vector2.Up, 160f, 0.3f, 0f, 1.5f, 80f);
    }

    protected override void OnLanded(float impactSpeed)
    {
        if (impactSpeed < 80f || IsDead)
            return;
        Sfx.Play("land", -6f + Mathf.Min(6f, impactSpeed / 120f));
        float strength = Mathf.Clamp(impactSpeed / 450f, 0.2f, 1f);
        Body.Scale = new Vector2(1f + 0.35f * strength, 1f - 0.3f * strength);
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 6 + (int)(strength * 6), new Color(0.82f, 0.8f, 0.72f, 0.7f), 60f * strength + 20f, Vector2.Up, 180f, 0.32f, 0f, 1.6f, 90f);
        if (impactSpeed > 420f)
            World.Camera.Feedback.AddTrauma(0.15f);
    }

    // A hovering combo may still dash out.
    private bool CanDash() => (IsGrounded || StateMachine.Is(PlayerState.Attack)) && Stamina >= DashStaminaCost * 0.5f;

    private void EnterDash()
    {
        Vector2 input = MoveInput;
        dashDirection = input.Length() > 0.2f ? input.Normalized() : AimDirection;
        Facing = dashDirection;
        MoveVelocity = dashDirection * DashSpeed;
        Animator.PlayOneShot("Dodge_" + PlayerAnimator.Direction4(dashDirection), DashDuration + 0.12f);
        invulnerableTimer = Mathf.Max(invulnerableTimer, DashInvulnerability);
        perfectDodgeAvailable = true;
        IgnorePits = true;
        SpendStamina(DashStaminaCost);
        afterImageTimer = 0f;
        Sfx.Play("dash", -3f);
        Body.Scale = new Vector2(1.35f, 0.75f);
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 8, new Color(0.85f, 0.82f, 0.75f, 0.7f), 70f, -dashDirection, 60f, 0.35f, -20f, 1.8f, 100f);
    }

    private void UpdateDash(float delta)
    {
        float t = StateMachine.TimeInState;
        if (t < DashDuration)
            MoveVelocity = dashDirection * DashSpeed;
        else
            MoveVelocity = MoveVelocity.MoveToward(MoveInput * MoveSpeed, Deceleration * 1.6f * delta);

        afterImageTimer -= delta;
        if (afterImageTimer <= 0f && t < DashDuration + 0.04f)
        {
            afterImageTimer = 0.028f;
            Color ghost = AetheriacActive ? new Color(1f, 0.15f, 0.25f, 0.55f) : new Color(0.45f, 0.9f, 1f, 0.5f);
            Vfx.AfterImage(World.Actors, this, Sprite, Z, ghost);
        }

        if (t >= DashDuration * 0.6f && Consume(BufferedAction.Attack) && CanAttack())
        {
            StateMachine.Transition(PlayerState.Attack);
            return;
        }
        if (t >= DashDuration + 0.07f)
            StateMachine.Transition(PlayerState.Locomotion);
    }

    private void ExitDash() => IgnorePits = GodMode;

    private bool CanHeal() => HealReadyNow && !AetheriacActive && IsGrounded && Health < MaxHealth;

    private void EnterHeal()
    {
        stateTimer = 0f;
        healConsumed = false;
        Sfx.Play("ui_move", -6f, 0.6f);
        healEffect = Vfx.Sheet(World.Effects, GlobalPosition + new Vector2(0, -Z - 6f), "heal", 7, 7f / HealChannelTime);
    }

    private void UpdateHeal(float delta)
    {
        ApplyMovement(delta, 0.35f);
        stateTimer += delta;
        if (IsInstanceValid(healEffect))
            healEffect.Position = GlobalPosition + new Vector2(0, -Z - 6f);
        // The star drains through the channel; the heal itself lands on the last frame.
        SetHealCharge(1f - Mathf.Clamp(stateTimer / HealChannelTime, 0f, 1f));
        if (stateTimer >= HealChannelTime)
        {
            ConsumeHeal();
            StateMachine.Transition(PlayerState.Locomotion);
        }
    }

    // An interrupted channel spends nothing: the star refills to where it was.
    private void ExitHeal()
    {
        if (!healConsumed)
        {
            SetHealCharge(1f);
            if (IsInstanceValid(healEffect))
                healEffect.QueueFree();
        }
    }

    private void EnterHurt()
    {
        stateTimer = 0f;
        MoveVelocity = Vector2.Zero;
    }

    private void UpdateHurt(float delta)
    {
        stateTimer += delta;
        if (stateTimer >= 0.24f)
            StateMachine.Transition(PlayerState.Locomotion);
    }

    private void TrackSafeGround()
    {
        if (!IsGrounded || IsDead || StateMachine.Is(PlayerState.Dash) || StateMachine.Is(PlayerState.Falling))
            return;
        const float margin = 10f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 probe = Position + new Vector2(i % 2 == 0 ? -margin : margin, i < 2 ? -margin : margin);
            if (!Mathf.IsEqualApprox(World.Grid.SurfaceZAt(probe), SupportZ))
                return;
        }
        lastSafePosition = Position;
    }

    private void UpdateInteraction()
    {
        IInteractable best = null;
        if (!IsDead && StateMachine.Is(PlayerState.Locomotion))
        {
            float bestDistance = float.MaxValue;
            foreach (IInteractable interactable in World.Interactables)
            {
                if (!interactable.CanInteract)
                    continue;
                float distance = interactable.InteractionPosition.DistanceTo(GlobalPosition);
                if (distance < interactable.InteractionRadius && distance < bestDistance)
                {
                    best = interactable;
                    bestDistance = distance;
                }
            }
        }
        if (best == focusedInteractable)
            return;
        focusedInteractable = best;
        WorldSignals.EmitInteractionPromptChanged(best?.Prompt);
    }

    private void UpdateAnimation(float delta)
    {
        animationTime += delta;
        string direction = PlayerAnimator.Direction8(Facing);
        float speed = MoveVelocity.Length();

        switch (StateMachine.Current)
        {
            case PlayerState.Attack:
            case PlayerState.Special:
                break;
            case PlayerState.Dash:
                break;
            case PlayerState.Dead:
            case PlayerState.Hurt:
            case PlayerState.Heal:
                Animator.Hold(PlayerAnimator.ClipName("Idle", direction), 0f);
                break;
            default:
                if (!IsGrounded)
                    Animator.Hold(PlayerAnimator.ClipName("Move", direction), ZVelocity > 0 ? 0.075f : 0.3f);
                else if (speed > 12f)
                    Animator.PlayLoop(PlayerAnimator.ClipName("Move", direction), Mathf.Clamp(1.3f * speed / MoveSpeed, 0.8f, 1.7f));
                else
                    Animator.PlayLoop(PlayerAnimator.ClipName("Idle", direction));
                break;
        }

        if (!IsGrounded && StateMachine.Is(PlayerState.Locomotion))
            targetBodyScale = ZVelocity > 0 ? new Vector2(0.88f, 1.12f) : new Vector2(1.04f, 0.97f);
        else
            targetBodyScale = Vector2.One;
        Body.Scale = Body.Scale.Lerp(targetBodyScale, 1f - Mathf.Exp(-16f * delta));

        bool blinking = invulnerableTimer > 0f && !StateMachine.Is(PlayerState.Dash) && !IsDead;
        Animator.SetVisible(!StateMachine.Is(PlayerState.Falling) && (!blinking || (int)(animationTime * 24f) % 2 == 0));
        EntityMaterial.SetShaderParameter(StringNames.Of("tint_strength"), AetheriacActive ? 0.55f + 0.15f * Mathf.Sin(animationTime * 9f) : 0f);
        EntityMaterial.SetShaderParameter(StringNames.Of("tint_color"), new Color(1f, 0.25f, 0.3f));
    }
}
