using Godot;

namespace Anomaly;

public enum EnemyState
{
    Idle,
    Chase,
    Telegraph,
    Attack,
    Recover,
    Flinch,
    Evade,
    Staggered,
    Dead,
    // Asleep, buried or hanging: an ambusher waiting for its tell to be ignored.
    Dormant,
    // Struck down but not finished: a Bonewalker's rattling pile.
    Downed,
}

public abstract partial class Enemy : Entity
{
    private const float FLINCH_DURATION = 0.22f;
    protected const float DEATH_DURATION = 0.75f;
    private const float ALERT_RADIUS = 125f;
    private const float EVADE_TRIGGER_RANGE = 58f;
    private const float EVADE_DURATION = 0.26f;
    private const float EVADE_COOLDOWN = 1.8f;
    private const float FLINCH_STREAK_WINDOW = 1.4f;
    private const int FLINCH_STREAK_LIMIT = 3;
    private const float ARMOR_DURATION = 1.3f;
    private const float QUICK_TELEGRAPH = 0.65f;
    private const float DODGE_CUE_LEAD = 0.18f;
    // A hunt ends only once Kio has been out of sight this long; a broken line of sight sends the enemy to search
    // where Kio was last seen first.
    private const float LOSE_SIGHT_TIME = 4.5f;
    private const float SEARCH_AFTER = 0.35f;
    private const float SIGHT_INTERVAL = 0.15f;
    // Past this many leash radii from home an enemy turns back once Kio is no longer in plain view.
    private const float HARD_LEASH = 2.5f;
    private const float REAGGRO_COOLDOWN = 3f;
    private const float ALERT_MARK_COOLDOWN = 5f;
    private const float RETURN_HOME_DISTANCE = 48f;
    private const float AIM_ACCURACY = 0.85f;
    private static readonly float[] STEER_ANGLES = { 0f, 0.55f, -0.55f, 1.1f, -1.1f, 1.7f, -1.7f, 2.4f, -2.4f };
    private static readonly Color ARMOR_COLOR = new(1f, 0.72f, 0.3f);

    [Export] public float MoveSpeed = 50f;
    [Export] public float Acceleration = 500f;
    [Export] public float AggroRadius = 140f;
    [Export] public float LeashRadius = 320f;
    [Export] public float AttackRange = 26f;
    [Export] public float AttackDamage = 10f;
    [Export] public float TelegraphTime = 0.45f;
    [Export] public float RecoverTime = 0.5f;
    [Export] public float AttackCooldown = 0.9f;
    [Export] public float FlinchResistance = 0f;
    [Export] public float EvadeChance = 0f;
    [Export] public float EvadeSpeed = 170f;
    [Export] public float CounterChance = 0.35f;
    [Export] public DamageType Weakness = DamageType.Slashing;
    [Export] public int SoulReward = 15;
    [Export] public int ResidueReward = 10;

    public int EnemyLevel { get; private set; }
    public StateMachine<EnemyState> StateMachine { get; private set; }
    public bool IsAggroed { get; private set; }
    public float TimeSinceDamaged { get; private set; } = 99f;
    public float FlankAngle { get; set; }
    public abstract string DisplayName { get; }
    public bool SpawnedByEncounter { get; set; }
    // Streaming restores: the level rolled the first time this enemy loaded, and the post it guards.
    public int PresetLevel { get; init; }
    public Vector2? Home { get; init; }
    public Vector2 HomeAnchor => HomePosition;
    // An ambusher that has already sprung stays sprung when the world streams it back in.
    public bool HasStirred => StateMachine != null && !StateMachine.Is(EnemyState.Dormant);
    // Safe to stream out: not mid-swing, not a pile waiting to rise, not dying.
    public bool CanUnload => !IsDead && StateMachine.Current is EnemyState.Idle or EnemyState.Chase or EnemyState.Dormant or EnemyState.Recover;
    public virtual bool IsRanged => false;
    public virtual bool IsCombatant => true;

    public override float CameraPriority => IsAggroed && !IsDead ? BaseCameraPriority + (1f - HealthRatio) * 1.5f : 0f;
    protected virtual float BaseCameraPriority => 1f;
    protected abstract string SpriteFolder { get; }
    protected virtual float SpriteFeetOffset => 14f;
    protected virtual Color IchorColor => new(0.55f, 0.8f, 0.3f);
    protected virtual float StandoffDistance => AttackRange + 30f;
    protected float ScaledDamage => DifficultyScalingSystem.ScaleStat(AttackDamage, EnemyLevel);
    protected Player Target => World.Player;
    protected Vector2 ToTarget => Target.GlobalPosition - GlobalPosition;
    protected bool TargetAvailable => !Target.IsDead && Mathf.Abs(Target.Z - Z) < 40f + (IsFlying ? 20f : 0f);
    protected bool IsArmored => armorTimer > 0f;
    // A healing or winded Kio is an opening: the pack recovers faster and commits sooner.
    protected bool TargetVulnerable => Target.IsHealing || Target.IsExhausted;

    protected Sprite2D Sprite;
    protected EnemyAnimationSet Animations;
    protected Vector2 FacingDirection = Vector2.Down;
    protected Vector2 HomePosition;
    protected float CooldownTimer;
    // Share of the attack's push still left; a landed strike spends some of it (see Push).
    protected float Momentum = 1f;

    private string animation = "Walk";
    private float animationTime;
    private float animationFps = 8f;
    private bool animationLoops = true;
    private float wanderTimer;
    private Vector2 wanderTarget;
    private float telegraphPulse;
    private float telegraphDuration;
    private bool quickTelegraph;
    private bool dodgeCued;
    private AttackCue attackCue;
    private float alertTimer = -1f;
    private float sinceSeen;
    private float sightTimer;
    private Vector2 lastSeenPosition;
    private float reaggroCooldown;
    private float sinceAlertMark = 99f;
    private float evadeCooldown;
    private Vector2 evadeDirection;
    private int flinchStreak;
    private float timeSinceFlinch = 99f;
    private float armorTimer;

    protected override void InitializeEntity()
    {
        EnemyLevel = PresetLevel > 0 ? PresetLevel : DifficultyScalingSystem.SampleEnemyLevel(SaveManager.Difficulty.DifficultyLevel);
        Animations = EnemyAnimationSet.Load(SpriteFolder);
        Sprite = new Sprite2D
        {
            Name = "Sprite",
            Hframes = EnemyAnimationSet.FRAME_COUNT,
            Offset = new Vector2(0, -SpriteFeetOffset),
            Material = EntityMaterial,
        };
        Body.AddChild(Sprite);
        HomePosition = Home ?? Position;
        wanderTarget = HomePosition;
        CarefulMovement = true;
        FlankAngle = GD.Randf() * Mathf.Tau;

        ConfigureEnemy();
        // Whatever lives in the Void wears it.
        if (World.InVoid)
        {
            EntityMaterial.SetShaderParameter(StringNames.Of("corruption"), 1f);
            EntityMaterial.SetShaderParameter(StringNames.Of("corruption_seed"), GD.Randf());
        }

        ResourceBar = new EnemyResourceBar { Name = "ResourceBar" };
        ResourceBar.Setup(this, -SpriteFeetOffset - BarHeight);
        Body.AddChild(ResourceBar);
        attackCue = new AttackCue { Name = "AttackCue", Position = new Vector2(0, -SpriteFeetOffset - BarHeight - 9f) };
        Body.AddChild(attackCue);

        StateMachine = new StateMachine<EnemyState>();
        StateMachine.Add(EnemyState.Idle, update: UpdateIdle);
        StateMachine.Add(EnemyState.Chase, update: UpdateChase);
        StateMachine.Add(EnemyState.Telegraph, EnterTelegraph, UpdateTelegraphState, ExitTelegraph);
        StateMachine.Add(EnemyState.Attack, EnterAttack, UpdateAttackState, ExitAttack);
        StateMachine.Add(EnemyState.Recover, EnterRecover, UpdateRecover);
        StateMachine.Add(EnemyState.Flinch, EnterFlinch, UpdateFlinch, ExitFlinch);
        StateMachine.Add(EnemyState.Evade, EnterEvade, UpdateEvade);
        StateMachine.Add(EnemyState.Staggered, EnterStaggered, UpdateStaggered, ExitStaggered);
        StateMachine.Add(EnemyState.Dead, EnterDead, UpdateDead);
        AddStates(StateMachine);
        StateMachine.Start(InitialState);
        Play("Walk", 6f);
    }

    public override void _ExitTree()
    {
        World?.Director.Release(this);
        base._ExitTree();
    }

    protected EnemyResourceBar ResourceBar { get; private set; }
    protected virtual float BarHeight => 22f;
    protected virtual bool UsesAnimationSet => true;

    protected abstract void ConfigureEnemy();

    // Streaming in an enemy that was already wounded: it comes back as hurt as it left, never healed by the trip.
    public void RestoreWounds(float health)
    {
        Health = Mathf.Clamp(health, 1f, MaxHealth);
        TimeSinceDamaged = 99f;
    }

    // Types with states of their own (Dormant ambushers, Downed piles) register them here.
    protected virtual void AddStates(StateMachine<EnemyState> machine) { }

    protected virtual EnemyState InitialState => EnemyState.Idle;

    // Called every frame instead of the D/S/U strip lookup when the type draws its own sprite.
    protected virtual void ApplyAnimation(string name, float time, float fps, bool loops) { }

    protected override float ComputeMaxHealth() => DifficultyScalingSystem.ScaleStat(Stats.MaxHealth, EnemyLevel);

    protected override void TickEntity(float delta)
    {
        TimeSinceDamaged += delta;
        timeSinceFlinch += delta;
        if (CooldownTimer > 0f)
            CooldownTimer -= delta * (IsAggroed && TargetVulnerable ? 2f : 1f);
        if (evadeCooldown > 0f)
            evadeCooldown -= delta;
        UpdateArmor(delta);
        UpdateAlert(delta);
        reaggroCooldown -= delta;
        sinceAlertMark += delta;
        if (World.Director.Holds(this) && StateMachine.Current is not (EnemyState.Telegraph or EnemyState.Attack or EnemyState.Recover))
            World.Director.Release(this);
        TryEvade();
        StateMachine.Update(delta);
        UpdateAnimation(delta);
    }

    protected void Play(string name, float fps, bool loop = true)
    {
        if (animation == name && animationLoops == loop && Mathf.IsEqualApprox(animationFps, fps))
            return;
        animation = name;
        animationFps = fps;
        animationLoops = loop;
        animationTime = 0f;
    }

    protected void Restart(string name, float fps, bool loop = false)
    {
        animation = name;
        animationFps = fps;
        animationLoops = loop;
        animationTime = 0f;
    }

    private void UpdateAnimation(float delta)
    {
        animationTime += delta;
        Body.Scale = Body.Scale.Lerp(Vector2.One, 1f - Mathf.Exp(-14f * delta));
        if (!UsesAnimationSet)
        {
            ApplyAnimation(animation, animationTime, animationFps, animationLoops);
            return;
        }
        Sprite.Texture = Animations.Resolve(animation, FacingDirection, out bool flip);
        Sprite.FlipH = flip;
        int frame = (int)(animationTime * animationFps);
        Sprite.Frame = animationLoops ? frame % EnemyAnimationSet.FRAME_COUNT : Mathf.Min(frame, EnemyAnimationSet.FRAME_COUNT - 1);
    }

    protected void Face(Vector2 direction)
    {
        if (direction.LengthSquared() > 0.01f)
            FacingDirection = direction.Normalized();
    }

    // Leads the target by its current velocity; a little under-led so strafing still beats a careless enemy.
    protected Vector2 PredictTarget(float leadTime) =>
        Target.GlobalPosition + Target.MoveVelocity * Mathf.Clamp(leadTime, 0f, 0.6f) * AIM_ACCURACY;

    protected bool IsPathClear(Vector2 direction)
    {
        Vector2 probe = GlobalPosition + direction * 12f;
        if (World.Grid.Blocks(probe, Radius, Z))
            return false;
        if (!IsFlying && World.Grid.SurfaceZAt(probe + direction * Radius) < SupportZ - 1f)
            return false;
        return true;
    }

    protected void SteerToward(Vector2 destination, float speed, float delta)
    {
        Vector2 offset = destination - GlobalPosition;
        if (offset.Length() < 2f)
        {
            MoveVelocity = MoveVelocity.MoveToward(Vector2.Zero, Acceleration * delta);
            return;
        }
        SteerDirection(offset.Normalized(), speed, delta);
    }

    protected void SteerDirection(Vector2 desired, float speed, float delta)
    {
        Vector2 chosen = Vector2.Zero;
        foreach (float angle in STEER_ANGLES)
        {
            Vector2 candidate = desired.Rotated(angle);
            if (IsPathClear(candidate))
            {
                chosen = candidate;
                break;
            }
        }
        MoveVelocity = MoveVelocity.MoveToward(chosen * speed, Acceleration * delta);
        if (chosen != Vector2.Zero)
            Face(MoveVelocity);
    }

    protected void Brake(float delta, float rate = 900f) => MoveVelocity = MoveVelocity.MoveToward(Vector2.Zero, rate * delta);

    private void UpdateIdle(float delta)
    {
        wanderTimer -= delta;
        if (wanderTimer <= 0f)
        {
            wanderTimer = (float)GD.RandRange(1.5, 3.5);
            wanderTarget = HomePosition + Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * (float)GD.RandRange(0, 40);
        }
        float fromHome = GlobalPosition.DistanceTo(HomePosition);
        if (fromHome > RETURN_HOME_DISTANCE)
            SteerToward(HomePosition, MoveSpeed * 0.8f, delta);
        else if (GlobalPosition.DistanceTo(wanderTarget) > 6f)
            SteerToward(wanderTarget, MoveSpeed * 0.35f, delta);
        else
            Brake(delta);
        Play("Walk", MoveVelocity.Length() > 5f ? 7f : 4f);

        if (CanSpotTarget(fromHome))
            Aggro();
    }

    // Sight needs range, a clear line and an enemy that is back inside its leash; a freshly calmed enemy
    // also looks away for a moment, so standing on the edge of its range cannot flip it on and off every frame.
    private bool CanSpotTarget(float fromHome)
    {
        if (!TargetAvailable || ToTarget.Length() >= AggroRadius)
            return false;
        // Walking up to one that just gave up still gets noticed.
        bool pointBlank = ToTarget.Length() < 56f;
        if (reaggroCooldown > 0f && !pointBlank)
            return false;
        if (!SpawnedByEncounter && fromHome > LeashRadius * 0.75f && !pointBlank)
            return false;
        return World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Z);
    }

    // Giving up only turns the enemy back toward its post. It keeps every wound it took.
    private void LoseAggro()
    {
        IsAggroed = false;
        reaggroCooldown = REAGGRO_COOLDOWN;
        sinceSeen = 0f;
        StateMachine.Transition(EnemyState.Idle);
    }

    public void Aggro()
    {
        if (IsDead)
            return;
        if (!IsAggroed)
        {
            IsAggroed = true;
            alertTimer = -1f;
            sinceSeen = 0f;
            sightTimer = 0f;
            lastSeenPosition = Target.GlobalPosition;
            EnemySignals.EmitAggroed(this);
            if (sinceAlertMark >= ALERT_MARK_COOLDOWN)
            {
                sinceAlertMark = 0f;
                Vfx.Number(World.Overlay, VisualCenter + new Vector2(0, -HeightSpan), "!", new Color(1f, 0.35f, 0.3f), 11);
            }
            AlertPack();
        }
        if (StateMachine.Is(EnemyState.Idle))
            StateMachine.Transition(EnemyState.Chase);
    }

    // A cry carries: nearby enemies with sight of this one join the hunt a beat later instead of waiting their turn.
    private void AlertPack()
    {
        if (!IsCombatant)
            return;
        foreach (Entity entity in World.Entities)
        {
            if (entity is not Enemy other || other == this || other.IsAggroed || other.IsDead || !other.IsCombatant || other.alertTimer >= 0f)
                continue;
            if (other.GlobalPosition.DistanceTo(GlobalPosition) > ALERT_RADIUS || !World.Grid.HasLineOfSight(GlobalPosition, other.GlobalPosition, Mathf.Max(Z, other.Z)))
                continue;
            other.alertTimer = (float)GD.RandRange(0.15, 0.45);
        }
    }

    private void UpdateAlert(float delta)
    {
        if (alertTimer < 0f)
            return;
        alertTimer -= delta;
        if (alertTimer < 0f && !IsAggroed && !IsDead)
            Aggro();
    }

    protected void Calm()
    {
        IsAggroed = false;
        reaggroCooldown = REAGGRO_COOLDOWN;
        if (StateMachine.Current is EnemyState.Chase or EnemyState.Flinch or EnemyState.Recover)
            StateMachine.Transition(EnemyState.Idle);
    }

    // Sight is memory, not a frame check: a jump, a ledge or a pillar between them does not make Kio vanish.
    // The hunt goes on while Kio has been seen recently, then to where Kio was last seen, and only then ends.
    private void UpdateSight(float delta)
    {
        sinceSeen += delta;
        sightTimer -= delta;
        if (sightTimer > 0f)
            return;
        sightTimer = SIGHT_INTERVAL;
        if (Target.IsDead || ToTarget.Length() > TrackRadius)
            return;
        if (!World.Grid.HasLineOfSight(GlobalPosition, Target.GlobalPosition, Mathf.Max(Z, Target.Z)))
            return;
        sinceSeen = 0f;
        lastSeenPosition = Target.GlobalPosition;
    }

    protected virtual float TrackRadius => Mathf.Max(AggroRadius * 2.5f, 300f);

    protected bool CanSeeTarget => sinceSeen < SEARCH_AFTER;

    private void UpdateChase(float delta)
    {
        UpdateSight(delta);
        bool overLeash = !SpawnedByEncounter && GlobalPosition.DistanceTo(HomePosition) > LeashRadius * HARD_LEASH;
        if (Target.IsDead || sinceSeen > LOSE_SIGHT_TIME || overLeash && !CanSeeTarget)
        {
            LoseAggro();
            return;
        }
        if (!CanSeeTarget)
        {
            Search(delta);
            return;
        }
        bool mayEngage = World.Director.CanEngage(this);
        if (!mayEngage && ToTarget.Length() < StandoffDistance * 1.4f)
            HoldStandoff(delta);
        else
            ChaseBehaviour(delta);
        // A melee swing at Kio on a ledge above would only cut air; ranged enemies still take the shot.
        if (CooldownTimer <= 0f && (IsRanged || TargetAvailable) && WantsToAttack() && World.Director.TryClaim(this))
            StateMachine.Transition(EnemyState.Telegraph);
    }

    // Out of sight: walk to the last place Kio stood and look around there.
    private void Search(float delta)
    {
        if (GlobalPosition.DistanceTo(lastSeenPosition) > 10f)
        {
            SteerToward(lastSeenPosition, MoveSpeed * 0.85f, delta);
            Play("Walk", 8f);
            return;
        }
        Brake(delta);
        Face(Vector2.FromAngle(sinceSeen * 2.2f + FlankAngle));
        Play("Walk", 4f);
    }

    protected virtual void ChaseBehaviour(float delta)
    {
        SteerToward(FlankApproach(), MoveSpeed, delta);
        Play("Walk", 9f);
    }

    // Approach along the assigned flank so a group arrives from several sides rather than in a line.
    protected Vector2 FlankApproach()
    {
        float distance = ToTarget.Length();
        float sideways = Mathf.Min(distance * 0.5f, AttackRange * 0.8f);
        return Target.GlobalPosition + Vector2.FromAngle(FlankAngle) * sideways;
    }

    protected Vector2 StandoffPoint() => Target.GlobalPosition + Vector2.FromAngle(FlankAngle) * StandoffDistance;

    // While another holds the attack token, circle at the flank slot facing Kio, ready to rotate in.
    protected virtual void HoldStandoff(float delta)
    {
        SteerToward(StandoffPoint(), MoveSpeed * 0.7f, delta);
        Face(ToTarget);
        Play("Walk", 8f);
    }

    protected virtual bool WantsToAttack() => ToTarget.Length() <= AttackRange;

    protected virtual float TelegraphFor() => TelegraphTime;

    private void EnterTelegraph()
    {
        telegraphPulse = 0f;
        telegraphDuration = TelegraphFor() * (quickTelegraph ? QUICK_TELEGRAPH : 1f) * (Target.IsHealing ? 0.8f : 1f);
        quickTelegraph = false;
        dodgeCued = false;
        attackCue.SetCharge(0f);
        Face(ToTarget);
        Sfx.Play("telegraph", -10f, 1f + GD.Randf() * 0.1f);
        OnTelegraphStart();
    }

    private void UpdateTelegraphState(float delta)
    {
        Brake(delta, 1400f);
        telegraphPulse += delta;
        float ratio = StateMachine.TimeInState / telegraphDuration;
        float pulse = 0.55f + 0.45f * Mathf.Sin(telegraphPulse * 28f);
        SetOutline(Mathf.Clamp(ratio * 1.5f, 0.2f, 1f) * pulse, new Color(1f, 0.15f, 0.25f));
        UpdateTelegraph(delta, ratio);
        float remaining = telegraphDuration - StateMachine.TimeInState;
        if (!dodgeCued)
            attackCue.SetCharge(StateMachine.TimeInState / Mathf.Max(0.01f, telegraphDuration - DODGE_CUE_LEAD));
        if (!dodgeCued && remaining <= DODGE_CUE_LEAD)
            SignalDodge();
        if (StateMachine.TimeInState >= telegraphDuration)
            StateMachine.Transition(EnemyState.Attack);
    }

    private void ExitTelegraph()
    {
        SetOutline(0f, Colors.White);
        attackCue.Cancel();
    }

    // The commit point of an attack: the overhead mark bursts into a glint, timed so a dash
    // started on reaction lands inside the perfect-dodge window. Follow-up strikes call this directly.
    protected void SignalDodge()
    {
        dodgeCued = true;
        attackCue.Strike();
        Flash(0.07f, new Color(1f, 0.95f, 0.85f));
    }

    protected virtual void OnTelegraphStart() => Restart("Attack", 4f);

    protected virtual void UpdateTelegraph(float delta, float ratio) { }

    private void EnterAttack()
    {
        Momentum = 1f;
        OnAttackStart();
    }

    private void UpdateAttackState(float delta)
    {
        if (UpdateAttack(delta))
            StateMachine.Transition(EnemyState.Recover);
    }

    private void ExitAttack() => OnAttackEnd();

    protected abstract void OnAttackStart();

    protected abstract bool UpdateAttack(float delta);

    protected virtual void OnAttackEnd() { }

    private void EnterRecover() => CooldownTimer = AttackCooldown * (float)GD.RandRange(0.8, 1.2);

    private void UpdateRecover(float delta)
    {
        Brake(delta);
        Play("Walk", 4f);
        if (StateMachine.TimeInState >= RecoverTime * (TargetVulnerable ? 0.6f : 1f))
            StateMachine.Transition(EnemyState.Chase);
    }

    private void EnterFlinch()
    {
        Play("Walk", 0f);
        MoveVelocity = Vector2.Zero;
    }

    private void UpdateFlinch(float delta)
    {
        Brake(delta);
        if (StateMachine.TimeInState >= FLINCH_DURATION)
            StateMachine.Transition(EnemyState.Chase);
    }

    // Shaking off a flinch sometimes turns straight into a quick counter, so trading hits up close is never free.
    private void ExitFlinch()
    {
        if (IsDead || !TargetAvailable || ToTarget.Length() > AttackRange * 1.1f || GD.Randf() >= CounterChance)
            return;
        CooldownTimer = 0f;
        quickTelegraph = true;
    }

    private void TryEvade()
    {
        if (EvadeChance <= 0f || evadeCooldown > 0f || !IsAggroed || IsArmored)
            return;
        if (StateMachine.Current is not (EnemyState.Chase or EnemyState.Recover) || Target.TimeSinceAttackStart > 0.06f || !TargetAvailable)
            return;
        if (ToTarget.Length() > EVADE_TRIGGER_RANGE)
            return;
        evadeCooldown = 0.45f;
        if (GD.Randf() >= EvadeChance)
            return;
        Vector2 away = -ToTarget.Normalized();
        float side = GD.Randf() < 0.5f ? 1f : -1f;
        foreach (float angle in new[] { 0.9f * side, -0.9f * side, 0f })
        {
            Vector2 candidate = away.Rotated(angle);
            if (!IsPathClear(candidate))
                continue;
            evadeDirection = candidate;
            evadeCooldown = EVADE_COOLDOWN;
            StateMachine.Transition(EnemyState.Evade);
            return;
        }
    }

    private void EnterEvade()
    {
        MoveVelocity = evadeDirection * EvadeSpeed;
        Face(-evadeDirection);
        if (!IsFlying && IsGrounded)
            Launch(110f);
        Body.Scale = new Vector2(1.25f, 0.8f);
        Sfx.Play("dash", -14f, 0.8f);
        Vfx.AfterImage(World.Actors, this, Sprite, Z, new Color(IchorColor, 0.35f), 0.2f);
    }

    private void UpdateEvade(float delta)
    {
        if (StateMachine.TimeInState < EVADE_DURATION * 0.6f)
            MoveVelocity = evadeDirection * EvadeSpeed;
        else
            Brake(delta, 1200f);
        Face(ToTarget);
        if (StateMachine.TimeInState >= EVADE_DURATION)
        {
            CooldownTimer = Mathf.Min(CooldownTimer, 0.15f);
            StateMachine.Transition(EnemyState.Chase);
        }
    }

    private void UpdateArmor(float delta)
    {
        if (armorTimer <= 0f)
            return;
        armorTimer -= delta;
        if (StateMachine.Current is not (EnemyState.Telegraph or EnemyState.Dead))
            SetOutline(armorTimer > 0f ? 0.5f + 0.5f * Mathf.Sin(armorTimer * 30f) : 0f, ARMOR_COLOR);
    }

    // Three flinches in quick succession harden the enemy for a moment: it shrugs off light hits and swings back.
    private bool RegisterFlinch()
    {
        flinchStreak = timeSinceFlinch < FLINCH_STREAK_WINDOW ? flinchStreak + 1 : 1;
        timeSinceFlinch = 0f;
        if (flinchStreak < FLINCH_STREAK_LIMIT)
            return true;
        flinchStreak = 0;
        armorTimer = ARMOR_DURATION;
        CooldownTimer = 0f;
        quickTelegraph = true;
        Flash(0.12f, ARMOR_COLOR);
        Vfx.Ring(World.Effects, VisualCenter, 3f, 20f, ARMOR_COLOR, 0.22f, 2f, 0.9f);
        return false;
    }

    private void EnterStaggered()
    {
        MoveVelocity = Vector2.Zero;
        armorTimer = 0f;
        SetOutline(0f, Colors.White);
        Play("Walk", 2f);
        EnemySignals.EmitTenacityShattered(this);
    }

    private void UpdateStaggered(float delta)
    {
        Brake(delta);
        float wobble = Mathf.Sin(StateMachine.TimeInState * 18f) * 0.08f;
        Body.Rotation = wobble;
        if (GD.Randf() < 0.15f)
            Vfx.Burst(World.Effects, VisualCenter + new Vector2(0, -HeightSpan * 0.6f), 1, new Color(1f, 0.9f, 0.4f), 20f, Vector2.Up, 90f, 0.5f, 0f, 1.5f, 10f, true);
        if (!Impact.IsStaggered)
            StateMachine.Transition(EnemyState.Chase);
    }

    private void ExitStaggered() => Body.Rotation = 0f;

    protected override void OnHit(HitInfo hit, ImpactResult impact)
    {
        TimeSinceDamaged = 0f;
        ResourceBar.OnDamaged(hit);
        Aggro();
        Body.Scale = new Vector2(1.25f, 0.8f);

        string text = Mathf.RoundToInt(hit.Damage).ToString();
        Color color = hit.IsWeaknessHit ? new Color(1f, 0.82f, 0.25f) : hit.IsCritical ? new Color(1f, 0.55f, 0.25f) : Colors.White;
        int size = hit.IsWeaknessHit || hit.IsCritical ? 12 : 10;
        if (hit.IsWeaknessHit)
            text += "!";
        Vfx.Number(World.Overlay, VisualCenter + new Vector2(0, -HeightSpan * 0.5f - 4f), text, color, size);
        Vfx.Burst(World.Effects, VisualCenter, 6, IchorColor, 120f, hit.Direction, 45f, 0.4f, 260f, 2f, 30f);

        if (impact.TenacityShattered)
        {
            Vfx.TenacityShatter(World.Effects, VisualCenter);
            Sfx.Play("tenacity_break", -1f);
            TimeControl.HitStop(0.085f, 0.02f);
            World.Camera.Feedback.TenacityShatter();
            if (StateMachine.Current != EnemyState.Dead)
                StateMachine.Transition(EnemyState.Staggered);
            return;
        }

        if (IsArmored && !hit.IsFinisher)
            return;
        bool overpowered = impact.AppliedForce > FlinchResistance || hit.IsFinisher && FlinchResistance < 200f;
        bool interruptible = StateMachine.Current is EnemyState.Idle or EnemyState.Chase or EnemyState.Telegraph or EnemyState.Recover or EnemyState.Flinch or EnemyState.Evade;
        if (overpowered && !Impact.IsStaggered && interruptible && RegisterFlinch())
            StateMachine.Transition(EnemyState.Flinch);
    }

    protected override void OnDeath(HitInfo hit)
    {
        StateMachine.Transition(EnemyState.Dead);
        ResourceBar.OnKilled(hit);
        Vfx.Burst(World.Effects, VisualCenter, 16, IchorColor, 170f, hit.Direction, 70f, 0.55f, 300f, 2.4f, 30f);
        Vfx.Ring(World.Effects, VisualCenter, 4f, 26f, new Color(1f, 1f, 1f, 0.8f), 0.25f, 3f, 0.7f);
        Sfx.Play("kill", -1f);
        World.SpawnSoulOrbs(GlobalPosition, Z + 8f, ScaledSouls);
        Target.AddResidue(ScaledResidue);
    }

    private int ScaledSouls => Mathf.RoundToInt(SoulReward * (1f + (EnemyLevel - 1) * 0.35f));

    private int ScaledResidue => LevelingSystem.ScaleResidue(ResidueReward, EnemyLevel);

    protected override void OnFellIntoVoid()
    {
        if (IsDead)
            return;
        IsDead = true;
        Solid = false;
        EnemySignals.EmitKilled(this);
        int reward = ScaledSouls;
        Target.AddSouls(reward);
        Target.AddResidue(ScaledResidue);
        Target.AddCorruption(6f);
        Sfx.Play("soul", -4f, 0.8f);
        Vfx.Number(World.Overlay, Target.GlobalPosition + new Vector2(0, -Target.Z - 34f), $"+{reward}", new Color(0.6f, 0.9f, 1f), 8);
        QueueFree();
    }

    private void EnterDead()
    {
        Solid = false;
        MoveVelocity = Vector2.Zero;
        armorTimer = 0f;
        SetOutline(0f, Colors.White);
        Restart("Death", EnemyAnimationSet.FRAME_COUNT / DEATH_DURATION);
        EnemySignals.EmitKilled(this);
    }

    private void UpdateDead(float delta)
    {
        if (StateMachine.TimeInState > DEATH_DURATION * 0.7f)
            Sprite.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - (StateMachine.TimeInState - DEATH_DURATION * 0.7f) * 3f, 0f, 1f));
        if (StateMachine.TimeInState >= DEATH_DURATION + 0.3f)
            QueueFree();
    }

    protected bool TryStrikePlayer(Vector2 center, float radius, float damage, float knockback, Vector2 direction)
    {
        if (Target.IsDead)
            return false;
        if (center.DistanceTo(Target.GlobalPosition) > radius + Target.Radius + 2f)
            return false;
        if (!Target.IsWithinVerticalReach(Z, HeightSpan))
            return false;
        var hit = new HitInfo
        {
            Damage = damage,
            Direction = direction.LengthSquared() > 0.01f ? direction.Normalized() : ToTarget.Normalized(),
            Knockback = knockback,
            Source = HitSource.Enemy,
            Attacker = this,
        };
        HitResponse response = Target.ReceiveHit(hit);
        if (Push.Connected(response))
        {
            float retained = Push.Retained(this, Target);
            Momentum *= retained;
            MoveVelocity *= retained;
        }
        return response != HitResponse.Ignored;
    }
}
