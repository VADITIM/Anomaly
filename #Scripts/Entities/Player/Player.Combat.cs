using System.Collections.Generic;
using Godot;

namespace Anomaly;

public partial class Player
{
    private enum StrikeKind
    {
        Sweep,
        Spin,
        Grind,
        Slam,
    }

    private sealed record AttackProfile(
        StrikeKind Kind, float DamageMultiplier, float ArcDegrees, float Range, float Step, float Duration, float StrikeFraction,
        float Knockback, float Tenacity, float HitStop, float Trauma, float Stamina, string Sound);

    // Five strikes: a forward sweep, the backhand, a full spin, a lunging buzzsaw that grinds several times,
    // then a leaping overhead slam. The slam is the Arc's manifestation interval: the pattern stays, the Arc rewrites the impact.
    private static readonly AttackProfile[] COMBO =
    {
        new(StrikeKind.Sweep, 0.9f, 150f, 36f, 55f, 0.3f, PlayerAnimator.ATTACK_STRIKE_FRACTION, 130f, 9f, 0.03f, 0.12f, 3.5f, "swing1"),
        new(StrikeKind.Sweep, 0.95f, 170f, 38f, 60f, 0.3f, PlayerAnimator.ATTACK_STRIKE_FRACTION, 140f, 10f, 0.032f, 0.14f, 4f, "swing2"),
        new(StrikeKind.Spin, 1.1f, 360f, 38f, 35f, 0.42f, 0.33f, 220f, 14f, 0.04f, 0.2f, 5f, "swing3"),
        new(StrikeKind.Grind, 0.42f, 360f, 15f, 170f, 0.64f, 0.25f, 12f, 5f, 0.018f, 0.07f, 5f, "throw"),
        new(StrikeKind.Slam, 1.6f, 140f, 44f, 70f, 0.62f, 0.55f, 340f, 26f, 0.065f, 0.34f, 7f, "swing3"),
    };

    private const float GRIND_DURATION = 0.34f;
    private const float GRIND_TICK = 0.075f;
    private const float GRIND_REACH = 17f;
    // Swinging in the air caps Kio's fall, and landing hits caps it harder, so a jump can carry a string up to flyers
    // as long as it keeps connecting; the slam drives him back down.
    private const float AIR_DESCENT_HIT = 18f;
    private const float AIR_DESCENT_MISS = 85f;
    private const float AIR_DESCENT_BRAKE = 1400f;
    private const float AIR_HIT_LINGER = 0.45f;
    private const float SLAM_GRAVITY_SCALE = 1.4f;
    private const float SLAM_PLUNGE_TIME = 0.07f;
    private const float SLAM_PLUNGE_SPEED = 420f;

    private const float COMBO_CONTINUE_WINDOW = 0.28f;
    private const float AIM_ASSIST_RANGE = 78f;
    private const float AIM_ASSIST_ANGLE = 0.75f;
    private const float STAGGER_STAMINA_RETURN = 1.5f;

    private int comboStep = -1;
    private float grindTickTimer;
    private float comboContinueTimer;
    private bool attackQueued;
    private Vector2 attackDirection = Vector2.Down;
    private bool strikeFeedbackDone;
    private bool swingClockwise;
    private float attackSpeedScale = 1f;
    private readonly HashSet<IHittable> hitThisAction = new();
    private float specialCooldownTimer;
    private float airHitTimer;
    private float slamPlungeTimer;
    private bool slamPlunge;
    private bool scytheThrown;
    private bool specialImpactDone;
    private float spearLungeSpeed;
    // What the last strike or special cost; its hits (thrown scythe, reflected shots included) carry it for the stagger refund.
    private float actionStaminaCost;

    private AttackProfile CurrentAttack => COMBO[Mathf.Clamp(comboStep, 0, COMBO.Length - 1)];
    private float AttackDuration => CurrentAttack.Duration * attackSpeedScale;
    private float StrikeTime => AttackDuration * CurrentAttack.StrikeFraction;
    private float RecoverTime => CurrentAttack.Kind == StrikeKind.Grind
        ? StrikeTime + GRIND_DURATION * attackSpeedScale
        : StrikeTime + AttackDuration * (PlayerAnimator.ATTACK_RECOVER_FRACTION - PlayerAnimator.ATTACK_STRIKE_FRACTION);
    // The body clip is timed so its strike frame lands on this strike's hit, whatever the strike's own pacing.
    private float AnimationDuration => StrikeTime / PlayerAnimator.ATTACK_STRIKE_FRACTION;
    // Past this point the swing's hits are out, so the next strike, a dash or the special may cut the follow-through.
    private float CancelTime => Mathf.Lerp(StrikeTime, RecoverTime, 0.55f);
    private bool IsFinisher => comboStep == COMBO.Length - 1;
    // The string starts three strikes long; the skill tree adds the grind and then the slam.
    private static int ComboLength => CombatSkills.Has(CombatSkill.FifthStrike) ? 5 : CombatSkills.Has(CombatSkill.FourthStrike) ? 4 : 3;
    private bool IsLastStrike => comboStep >= ComboLength - 1;

    public float TimeSinceAttackStart { get; private set; } = 99f;

    // Strikes draw stamina; any stamina left allows a swing, so an empty bar is the only hard stop.
    private bool CanAttack() => !scytheThrown && Stamina > 0f;

    // The special is free; the Stamina Star is its cooldown.
    private bool CanUseSpecial() => !scytheThrown && specialCooldownTimer <= 0f;

    public int ArcLevel => ArcUpgrades.LevelOf(CurrentArc.ArcId);
    public float SpecialCooldown => ArcUpgrades.Cooldown(CurrentArc, ArcLevel);

    public float SpecialReadiness => scytheThrown ? 0f : 1f - Mathf.Clamp(specialCooldownTimer / SpecialCooldown, 0f, 1f);

    private void EnterAttack()
    {
        comboStep = comboContinueTimer > 0f && comboStep < ComboLength - 1 ? comboStep + 1 : 0;
        comboContinueTimer = 0f;
        attackQueued = false;
        strikeFeedbackDone = false;
        hitThisAction.Clear();
        TimeSinceAttackStart = 0f;
        attackSpeedScale = AetheriacActive ? 0.8f : 1f;
        // The sheet has eight facings, so the hit cone snaps with it: what the slash covers is what it hits.
        attackDirection = PlayerAnimator.Snap8(ApplyAimAssist(AimDirection));
        Facing = attackDirection;
        slamPlunge = false;
        MoveVelocity = attackDirection * CurrentAttack.Step * (IsGrounded ? 1f : 0.4f);
        swingClockwise = comboStep % 2 == 0;
        grindTickTimer = 0f;
        Animator.PlayOneShot("Attack_" + PlayerAnimator.Direction8(attackDirection), AnimationDuration);
        actionStaminaCost = CurrentAttack.Stamina * StrikeStaminaScale;
        attackHeld = true;
        SpendStamina(actionStaminaCost);
        Scythe.Manifest();
        switch (CurrentAttack.Kind)
        {
            case StrikeKind.Grind:
                Scythe.Grind(attackDirection, StrikeTime, RecoverTime - StrikeTime);
                break;
            case StrikeKind.Slam:
                // On the ground a hop that lands on the strike frame; in the air he drifts down through the wind-up, then plunges.
                if (IsGrounded)
                {
                    GravityScale = SLAM_GRAVITY_SCALE;
                    ZVelocity = GRAVITY * SLAM_GRAVITY_SCALE * StrikeTime * 0.5f;
                }
                else
                {
                    slamPlungeTimer = Mathf.Max(StrikeTime - SLAM_PLUNGE_TIME, 0.001f);
                    slamPlunge = true;
                }
                Body.Scale = new Vector2(0.86f, 1.16f);
                Scythe.BeginSwing(attackDirection, Mathf.DegToRad(StrikeLook.SlashArcDegrees), swingClockwise, StrikeTime, (RecoverTime - StrikeTime) * 0.6f);
                break;
            default:
                Scythe.BeginSwing(attackDirection, Mathf.DegToRad(StrikeLook.SlashArcDegrees), swingClockwise, StrikeTime, RecoverTime - StrikeTime);
                break;
        }
        Sfx.Play(CurrentAttack.Sound, -2f, (AetheriacActive ? 0.85f : 1f) * (CurrentAttack.Kind == StrikeKind.Slam ? 0.8f : 1f));
    }

    private void UpdateAttack(float delta)
    {
        float t = StateMachine.TimeInState;
        AttackProfile attack = CurrentAttack;
        float activeStart = StrikeTime;
        float activeEnd = RecoverTime;

        // The buzzsaw lunge keeps sliding into the grind; every other strike plants its feet.
        float drag = attack.Kind == StrikeKind.Grind ? 5f : 18f;
        MoveVelocity = Push.Decay(MoveVelocity, drag, delta);
        UpdateAirDescent(delta);

        if (!strikeFeedbackDone && t >= activeStart)
        {
            strikeFeedbackDone = true;
            OnStrikeFrame();
        }
        if (t >= activeStart && t <= activeEnd)
        {
            if (attack.Kind == StrikeKind.Grind)
                UpdateGrind(attack, delta);
            else
                PerformSwingHits(attack);
        }

        if (Consume(BufferedAction.Attack))
            attackQueued = true;

        // Holding Strike through a strike charges its art; until then the strike keeps Kio planted instead of ending.
        if (UpdateHold(t, out bool holding))
        {
            StateMachine.Transition(PlayerState.Charge);
            return;
        }

        // Dash cancels from the strike frame on: defence never waits on the follow-through.
        if (t >= activeStart && Consume(BufferedAction.Dash) && CanDash())
        {
            StateMachine.Transition(PlayerState.Dash);
            return;
        }

        // The last strike of the string keeps its weight: a queued strike after it opens a fresh string once the recovery begins.
        float chainTime = IsLastStrike ? activeEnd : CancelTime;
        if (t >= chainTime)
        {
            if (attackQueued && CanAttack())
            {
                StateMachine.Transition(PlayerState.Attack);
                return;
            }
            if (Consume(BufferedAction.Special) && CanUseSpecial())
            {
                StateMachine.Transition(PlayerState.Special);
                return;
            }
        }

        if (holding)
            return;

        if (t >= activeEnd && MoveInput.Length() > 0.5f && (!IsFinisher || t >= activeEnd + 0.05f))
        {
            StateMachine.Transition(PlayerState.Locomotion);
            return;
        }

        if (t >= AttackDuration)
            StateMachine.Transition(PlayerState.Locomotion);
    }

    private void UpdateAirDescent(float delta)
    {
        airHitTimer -= delta;
        if (IsGrounded)
            return;
        if (slamPlunge)
        {
            slamPlungeTimer -= delta;
            if (slamPlungeTimer <= 0f)
            {
                slamPlunge = false;
                // Over solid floor the plunge is timed to meet it on the strike frame; over a pit there is nothing to meet.
                if (float.IsNegativeInfinity(SupportZ))
                {
                    GravityScale = SLAM_GRAVITY_SCALE;
                    ZVelocity = -SLAM_PLUNGE_SPEED;
                }
                else
                {
                    GravityScale = 0f;
                    ZVelocity = Mathf.Min(SupportZ - Z, 0f) / SLAM_PLUNGE_TIME;
                }
                return;
            }
        }
        else if (IsFinisher)
            return;
        // The jump's climb runs out under normal gravity; the cap only takes over once he is falling.
        if (ZVelocity > 0f)
        {
            GravityScale = 1f;
            return;
        }
        // Gravity hands over to the cap: falling picks up speed as usual up to the limit, and a faster fall is braked to it.
        float limit = airHitTimer > 0f ? AirDescentHit : AirDescentMiss;
        GravityScale = 0f;
        float rate = ZVelocity > -limit ? GRAVITY : AIR_DESCENT_BRAKE;
        ZVelocity = Mathf.MoveToward(ZVelocity, -limit, rate * delta);
    }

    private void ExitAttack()
    {
        GravityScale = 1f;
        comboContinueTimer = attackQueued ? 1f : COMBO_CONTINUE_WINDOW;
        if (IsLastStrike && !attackQueued)
            comboStep = -1;
    }

    private Vector2 ApplyAimAssist(Vector2 aim)
    {
        if (GameSettings.AimAssist <= 0f)
            return aim;
        Enemy best = null;
        float bestScore = float.MaxValue;
        foreach (Entity entity in World.Entities)
        {
            if (entity is not Enemy enemy || enemy.IsDead || !IsWithinVerticalReach(enemy.Z, enemy.HeightSpan))
                continue;
            Vector2 to = enemy.GlobalPosition - GlobalPosition;
            float distance = to.Length();
            if (distance > AIM_ASSIST_RANGE || distance < 1f)
                continue;
            float angle = Mathf.Abs(aim.AngleTo(to));
            if (angle > AIM_ASSIST_ANGLE)
                continue;
            float score = distance * (1f + angle * 1.5f);
            if (score < bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }
        if (best == null)
            return aim;
        Vector2 toTarget = (best.GlobalPosition - GlobalPosition).Normalized();
        return aim.Slerp(toTarget, 0.7f * GameSettings.AimAssist).Normalized();
    }

    private Color SlashColor => AetheriacActive ? new Color(1f, 0.2f, 0.28f) : CurrentArc.TrailColor;

    private Vector2 HandPosition => GlobalPosition + new Vector2(0, -Z - 11f);

    private float SwingArc => Mathf.DegToRad(IsFinisher ? CurrentArc.ManifestArcDegrees : CurrentAttack.ArcDegrees);

    private float SwingRange => CurrentAttack.Range + (IsFinisher ? CurrentArc.ManifestRangeBonus : 0f);

    private StrikeVfx StrikeLook => TelekineticScythe.Strike(comboStep);

    // Combat counts as engaged while any living enemy nearby is hunting Kio; that keeps the Scythe out.
    private bool ComputeInCombat()
    {
        foreach (Entity entity in World.Entities)
            if (entity is Enemy enemy && enemy.IsAggroed && !enemy.IsDead && enemy.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 220f * 220f)
                return true;
        return false;
    }

    private Vector2 GrindCenter => GlobalPosition + attackDirection * GRIND_REACH;

    // The Scythe's sweep carries the motion; the crescent is its wake, traced on the same arc at the blade's own reach.
    // The base wake belongs to the weapon's pattern; the Arc's manifestation is a separate overlay on the interval strike.
    private void OnStrikeFrame()
    {
        Body.Scale = IsFinisher ? new Vector2(1.22f, 0.82f) : new Vector2(1.1f, 0.92f);
        StrikeVfx look = StrikeLook;
        Color wake = new(SlashColor, 0.75f);
        float reach = TelekineticScythe.VISUAL_REACH;
        float linger = RecoverTime - StrikeTime + look.SlashLinger;
        switch (CurrentAttack.Kind)
        {
            case StrikeKind.Grind:
                Vector2 disc = GrindCenter + new Vector2(0, -Z - 8f);
                Vfx.Ring(World.Effects, disc, 4f, reach * look.RingReach, wake, 0.22f, look.RingThickness, 0.9f);
                Vfx.ImpactStar(World.Effects, disc, look.StarSize, Colors.White);
                Vfx.Burst(World.Effects, disc, look.SparkCount * 2, SlashColor, 160f, attackDirection, 80f, 0.24f, 0f, 1.8f, 140f, true);
                return;
            case StrikeKind.Slam:
                Vector2 impact = GlobalPosition + attackDirection * 18f + new Vector2(0, -Z);
                Vfx.Slash(World.Effects, HandPosition, attackDirection.Angle(), Mathf.DegToRad(look.SlashArcDegrees), reach * look.SlashReach, look.SlashThickness, wake, swingClockwise, 0.3f);
                Vfx.Ring(World.Effects, impact, 5f, reach * look.RingReach, new Color(SlashColor, 0.8f), 0.3f, look.RingThickness);
                Vfx.Burst(World.Effects, impact, look.SparkCount, new Color(0.62f, 0.58f, 0.52f), 120f, Vector2.Up, 170f, 0.4f, 320f, 2f, 40f);
                Vfx.ImpactStar(World.Effects, impact + new Vector2(0, -5f), look.StarSize, Colors.White);
                World.Camera.Feedback.AddTrauma(0.18f);
                Sfx.Play("hit_heavy", -4f, 0.75f);
                break;
            default:
                Vfx.Slash(World.Effects, HandPosition, attackDirection.Angle(), Mathf.DegToRad(look.SlashArcDegrees), reach * look.SlashReach, look.SlashThickness, wake, swingClockwise, linger);
                if (look.RingReach > 0f)
                    Vfx.Ring(World.Effects, GlobalPosition + new Vector2(0, -Z), 6f, reach * look.RingReach, new Color(SlashColor, 0.5f), 0.26f, look.RingThickness);
                break;
        }
        if (IsFinisher)
        {
            PlayArcManifestation(look);
            QueueEcho();
        }
    }

    // Traces the base wake again in the Arc's colours without resizing it, so any Arc can rewrite the interval strike's look.
    private void PlayArcManifestation(StrikeVfx look)
    {
        float radius = TelekineticScythe.VISUAL_REACH * look.SlashReach;
        Color arcColor = CurrentArc.TrailColor.Lightened(0.35f);
        Vfx.Slash(World.Effects, HandPosition, attackDirection.Angle(), Mathf.DegToRad(look.SlashArcDegrees), radius - look.SlashThickness * 0.5f, look.SlashThickness * 0.45f, arcColor, swingClockwise, 0.36f);
        Vector2 burstPoint = GlobalPosition + attackDirection * 14f + new Vector2(0, -Z - 8f);
        Vfx.Burst(World.Effects, burstPoint, 8, arcColor, 90f, attackDirection, 60f, 0.35f, -40f, 1.6f, 60f, true);
        Color tint = CurrentArc.SpecialMove == SpecialMoveKind.ScytheThrow ? Colors.White : CurrentArc.TrailColor.Lightened(0.45f);
        Animator.SpawnArcEffect(World.Effects, burstPoint, attackDirection.X < 0f, tint, 0.55f);
        World.Camera.Feedback.Punch(0.02f);
    }

    // The buzzsaw re-strikes everything inside its disc on a fixed tick, so a pinned enemy eats the whole grind.
    private void UpdateGrind(AttackProfile attack, float delta)
    {
        grindTickTimer -= delta;
        if (grindTickTimer > 0f)
            return;
        grindTickTimer = GRIND_TICK * attackSpeedScale;
        hitThisAction.Clear();
        Vector2 center = GrindCenter;
        Vector2 visual = center + new Vector2(0, -Z - 8f);
        StrikeVfx look = StrikeLook;
        float bladeRadius = TelekineticScythe.VISUAL_REACH * look.SlashReach;
        float spin = (float)GD.RandRange(0, Mathf.Tau);
        Vfx.Slash(World.Effects, visual, spin, Mathf.DegToRad(look.SlashArcDegrees), bladeRadius, look.SlashThickness, new Color(SlashColor, 0.8f), swingClockwise, 0.14f);
        Vector2 rim = visual + Vector2.FromAngle(spin) * bladeRadius;
        Vfx.Burst(World.Effects, rim, look.SparkCount, SlashColor, 150f, Vector2.FromAngle(spin).Orthogonal(), 50f, 0.2f, 120f, 1.6f, 120f, true);
        Sfx.Play("swing1", -12f, 1.4f + GD.Randf() * 0.2f);
        foreach (IHittable target in World.CollectPlayerTargets())
        {
            Vector2 to = target.GroundPosition - center;
            if (to.Length() > attack.Range + target.HurtRadius || !IsWithinVerticalReach(target.Z, target.HeightSpan))
                continue;
            hitThisAction.Add(target);
            airHitTimer = AirHitLinger;
            HitInfo hit = BuildHit(target, attack.DamageMultiplier * MomentumMultiplier, CurrentArc.PrimaryDamageType, attackDirection, attack.Knockback, attack.Tenacity, attack.HitStop, false);
            hit.Lift = JuggleLift;
            StrikeThrough(target, DeliverHit(target, hit, attack.Trauma));
        }
    }

    private void PerformSwingHits(AttackProfile attack)
    {
        float range = SwingRange;
        float halfArc = SwingArc * 0.5f;
        float damageMultiplier = attack.DamageMultiplier * MomentumMultiplier * (IsFinisher ? CurrentArc.ManifestDamageMultiplier : 1f);
        float knockback = attack.Knockback * (IsFinisher ? CurrentArc.ManifestKnockbackMultiplier : 1f);
        float tenacity = attack.Tenacity * (IsFinisher ? CurrentArc.ManifestTenacityMultiplier : 1f);

        foreach (IHittable target in World.CollectPlayerTargets())
        {
            if (hitThisAction.Contains(target))
                continue;
            Vector2 to = target.GroundPosition - GlobalPosition;
            float distance = to.Length();
            if (distance > range + target.HurtRadius)
                continue;
            if (distance > target.HurtRadius && Mathf.Abs(attackDirection.AngleTo(to)) > halfArc + 0.15f)
                continue;
            if (!IsWithinVerticalReach(target.Z, target.HeightSpan))
                continue;

            hitThisAction.Add(target);
            airHitTimer = AirHitLinger;
            Vector2 direction = distance > 1f ? to / distance : attackDirection;
            Vector2 pushDirection = direction.Lerp(attackDirection, 0.5f).Normalized();
            HitInfo hit = BuildHit(target, damageMultiplier, CurrentArc.PrimaryDamageType, pushDirection, knockback, tenacity, attack.HitStop, IsFinisher);
            hit.Lift = IsFinisher && CurrentArc.SpecialMove == SpecialMoveKind.ShatterWave ? 140f : IsFinisher ? 0f : JuggleLift;
            hit.IsArcInterval = IsFinisher;
            StrikeThrough(target, DeliverHit(target, hit, attack.Trauma));
        }
    }

    // Driving a strike into a body spends the step's momentum on it.
    private void StrikeThrough(IHittable target, HitResponse response)
    {
        if (Push.Connected(response))
            MoveVelocity *= Push.Retained(this, target);
    }

    public HitInfo BuildHit(IHittable target, float multiplier, DamageType type, Vector2 direction, float knockback, float tenacity, float hitStop, bool finisher)
    {
        float efficiency = CurrentArc.BaseEfficiency;
        bool weakness = false;
        if (target is Enemy enemy && enemy.Weakness == type)
        {
            efficiency = CurrentArc.MatchedEfficiency;
            weakness = true;
        }

        float damage = DamageScaling.StrikeDamage(CurrentArc.ArcId) * multiplier * efficiency;
        bool critical = GD.Randf() < SaveManager.WeaponStats.CriticalChance;
        if (critical)
            damage *= SaveManager.WeaponStats.CriticalMultiplier;
        if (AetheriacActive)
            damage *= LevelingSystem.AetheriacDamage(SaveManager.PlayerStats);
        if (target is Enemy staggered && staggered.Impact.IsStaggered)
            damage *= 1.5f;
        damage *= Amulets.Multiplier(AmuletStat.Damage);
        damage *= (float)GD.RandRange(0.94, 1.06);
        damage = DamageScaling.CapHit(damage, target);

        return new HitInfo
        {
            Damage = Mathf.Round(damage),
            DamageType = type,
            Direction = direction,
            Knockback = knockback,
            TenacityDamage = tenacity * (weakness ? 1.5f : 1f) * LevelingSystem.TenacityMultiplier(SaveManager.PlayerStats) * ArcUpgrades.TenacityMultiplier(ArcLevel),
            HitStop = hitStop,
            Efficiency = efficiency,
            IsCritical = critical,
            IsFinisher = finisher,
            IsWeaknessHit = weakness,
            StaminaCost = actionStaminaCost,
            Source = HitSource.Player,
            Attacker = this,
            ContactPoint = target.GroundPosition + new Vector2(0, -target.Z - target.HeightSpan * 0.5f),
        };
    }

    public HitResponse DeliverHit(IHittable target, HitInfo hit, float trauma)
    {
        bool staggered = target is Enemy { Impact.IsStaggered: true };
        HitResponse response = target.ReceiveHit(hit);
        if (response == HitResponse.Ignored)
            return response;

        // Every hit on a staggered enemy pays back the stamina its action cost, with interest.
        if (staggered)
            RestoreStamina(hit.StaminaCost * STAGGER_STAMINA_RETURN);

        bool living = target is Enemy;
        bool killed = response == HitResponse.Killed;
        if (living)
        {
            RegisterLivingHit(hit.Damage, killed);
            float stop = hit.HitStop + (hit.IsWeaknessHit ? 0.012f : 0f) + (killed ? 0.035f : 0f);
            TimeControl.HitStop(stop);
            World.Camera.Feedback.AddTrauma(trauma + (killed ? 0.15f : 0f));
            if (hit.IsFinisher || killed)
                World.Camera.Feedback.Punch(killed ? 0.05f : 0.035f);
        }
        else
        {
            TimeControl.HitStop(0.018f);
            World.Camera.Feedback.AddTrauma(trauma * 0.5f);
        }

        SpawnHitEffect(hit, staggered);
        Sfx.Play(hit.IsWeaknessHit ? "hit_weak" : hit.IsFinisher ? "hit_heavy" : "hit", hit.IsWeaknessHit ? -4f : -1f);
        return response;
    }

    // Three readable tiers: a plain cut, a punishing hit on a staggered enemy, and the Arc's mark layered over either.
    private void SpawnHitEffect(HitInfo hit, bool staggered)
    {
        Vector2 point = hit.ContactPoint;
        Color sparkColor = hit.IsWeaknessHit ? new Color(1f, 0.85f, 0.3f) : SlashColor;
        if (staggered)
        {
            Color gold = new(1f, 0.86f, 0.4f);
            float cut = hit.Direction.Angle();
            Vfx.Slash(World.Effects, point - Vector2.FromAngle(cut + 0.9f) * 9f, cut + 0.9f, 1.1f, 9f, 3f, gold, true, 0.16f);
            Vfx.Slash(World.Effects, point - Vector2.FromAngle(cut - 0.9f) * 9f, cut - 0.9f, 1.1f, 9f, 3f, gold, false, 0.16f);
            Vfx.ImpactStar(World.Effects, point, 16f, gold);
            Vfx.Burst(World.Effects, point, 10, gold, 190f, hit.Direction, 70f, 0.3f, 120f, 2f, 160f, true);
        }
        else
        {
            Vfx.ImpactStar(World.Effects, point, 12f, Colors.White);
            Vfx.Burst(World.Effects, point, 6, sparkColor, 200f, hit.Direction, 38f, 0.24f, 0f, 1.8f, 220f, true);
        }
        if (!hit.IsArcInterval)
            return;
        Color arcColor = CurrentArc.TrailColor.Lightened(0.3f);
        Vfx.Ring(World.Effects, point, 3f, 16f, arcColor, 0.28f, 2.5f, 0.8f);
        Vfx.ImpactStar(World.Effects, point, 14f, arcColor);
        Vfx.Burst(World.Effects, point, 8, arcColor, 70f, Vector2.Up, 180f, 0.45f, -60f, 1.6f, 40f, true);
    }

    private float SpecialDuration() => CurrentArc.SpecialMove switch
    {
        SpecialMoveKind.ScytheThrow => 0.22f,
        SpecialMoveKind.ShatterWave => 0.48f,
        _ => 0.5f,
    };

    private void EnterSpecial()
    {
        attackDirection = ApplyAimAssist(AimDirection);
        Facing = attackDirection;
        TimeSinceAttackStart = 0f;
        specialImpactDone = false;
        hitThisAction.Clear();
        actionStaminaCost = 0f;
        specialCooldownTimer = GodMode ? 0f : SpecialCooldown;
        MoveVelocity = Vector2.Zero;
        Body.Scale = new Vector2(0.85f, 1.15f);
        Animator.PlayOneShot("Attack_" + PlayerAnimator.Direction8(attackDirection), Mathf.Max(SpecialDuration(), 0.32f));
        Scythe.Manifest();

        switch (CurrentArc.SpecialMove)
        {
            case SpecialMoveKind.ScytheThrow:
                Sfx.Play("throw", -2f);
                scytheThrown = true;
                Scythe.SetThrown(true);
                var projectile = new ScytheProjectile();
                projectile.Setup(this, attackDirection);
                World.Actors.AddChild(projectile);
                break;
            case SpecialMoveKind.ShatterWave:
                Sfx.Play("swing3", -2f, 0.7f);
                Scythe.BeginSwing(attackDirection, Mathf.Pi * 0.8f, true, 0.17f, 0.05f);
                break;
            case SpecialMoveKind.SpearLunge:
                Sfx.Play("dash", -2f, 0.8f);
                invulnerableTimer = Mathf.Max(invulnerableTimer, 0.28f);
                IgnorePits = true;
                spearLungeSpeed = Push.SPEAR_LUNGE_SPEED;
                Scythe.Thrust(attackDirection, 0.42f);
                break;
        }
    }

    private void UpdateSpecial(float delta)
    {
        float t = StateMachine.TimeInState;
        switch (CurrentArc.SpecialMove)
        {
            case SpecialMoveKind.ScytheThrow:
                MoveVelocity = Push.Decay(MoveVelocity, 12f, delta);
                break;
            case SpecialMoveKind.ShatterWave:
                MoveVelocity = Vector2.Zero;
                if (!specialImpactDone && t >= 0.2f)
                {
                    specialImpactDone = true;
                    var wave = new ShatterWave();
                    wave.Setup(this, attackDirection);
                    World.Effects.AddChild(wave);
                    Body.Scale = new Vector2(1.3f, 0.75f);
                    World.Camera.Feedback.AddTrauma(0.3f);
                    Sfx.Play("hit_heavy", -1f, 0.75f);
                }
                break;
            case SpecialMoveKind.SpearLunge:
                UpdateSpearLunge(t, delta);
                break;
        }
        if (t >= SpecialDuration())
            StateMachine.Transition(PlayerState.Locomotion);
    }

    // The lunge bursts out and brakes to a stop. Each body it pierces eats momentum, so it stops sooner and shorter:
    // what is left of the run is speed² / (2 · deceleration), never a fixed distance.
    private void UpdateSpearLunge(float t, float delta)
    {
        if (spearLungeSpeed > 0f)
        {
            spearLungeSpeed = Mathf.MoveToward(spearLungeSpeed, 0f, Push.SPEAR_LUNGE_DECELERATION * delta);
            if ((int)(t * 60f) % 2 == 0)
                Vfx.AfterImage(World.Actors, this, Sprite, Z, new Color(0.75f, 0.55f, 1f, 0.5f));
            foreach (IHittable target in World.CollectPlayerTargets())
            {
                if (hitThisAction.Contains(target) || target.GroundPosition.DistanceTo(GlobalPosition) > 16f + target.HurtRadius || !IsWithinVerticalReach(target.Z, target.HeightSpan))
                    continue;
                hitThisAction.Add(target);
                HitInfo pierce = BuildHit(target, 0.9f, CurrentArc.PrimaryDamageType, attackDirection, 120f, 10f, 0.03f, false);
                if (Push.Connected(DeliverHit(target, pierce, 0.12f)))
                    spearLungeSpeed *= Push.Retained(this, target);
            }
            MoveVelocity = attackDirection * spearLungeSpeed;
            return;
        }

        MoveVelocity = Vector2.Zero;
        if (specialImpactDone)
            return;
        specialImpactDone = true;
        IgnorePits = GodMode;
        Vector2 impactPoint = GlobalPosition + attackDirection * 10f;
        Vfx.Ring(World.Effects, impactPoint + new Vector2(0, -Z), 6f, 44f, CurrentArc.TrailColor, 0.35f, 4f);
        Vfx.Burst(World.Effects, impactPoint + new Vector2(0, -Z), 16, new Color(0.7f, 0.65f, 0.6f), 150f, Vector2.Up, 180f, 0.45f, 300f, 2.2f, 60f);
        World.Camera.Feedback.AddTrauma(0.35f);
        Sfx.Play("hit_heavy", 0f, 0.8f);
        Body.Scale = new Vector2(1.3f, 0.75f);
        foreach (IHittable target in World.CollectPlayerTargets())
        {
            Vector2 to = target.GroundPosition - impactPoint;
            if (to.Length() > 38f + target.HurtRadius || !IsWithinVerticalReach(target.Z, target.HeightSpan))
                continue;
            HitInfo smash = BuildHit(target, CurrentArc.SpecialDamageMultiplier, CurrentArc.SpecialMoveDamageType, to.Length() > 1f ? to.Normalized() : attackDirection, 300f, 35f, 0.07f, true);
            smash.Lift = 160f;
            DeliverHit(target, smash, 0.2f);
        }
    }

    private void ExitSpecial()
    {
        IgnorePits = GodMode;
    }

    public void OnScytheCaught()
    {
        scytheThrown = false;
        Scythe.SetThrown(false);
        Sfx.Play("catch", -3f);
        Body.Scale = new Vector2(1.15f, 0.9f);
        Vfx.ImpactStar(World.Effects, HandPosition, 10f, CurrentArc.TrailColor);
    }

    private void UpdateCombatTimers(float delta)
    {
        TimeSinceAttackStart += delta;
        UpdateEcho(delta);
        if (specialCooldownTimer > 0f)
            specialCooldownTimer -= delta;
        if (comboContinueTimer > 0f && !StateMachine.Is(PlayerState.Attack) && !StateMachine.Is(PlayerState.Charge))
        {
            comboContinueTimer -= delta;
            if (comboContinueTimer <= 0f)
                comboStep = -1;
        }
    }
}
