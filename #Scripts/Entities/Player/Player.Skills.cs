using Godot;

namespace Anomaly;

// The combat arts from the skill tree. Holding Strike through a strike of the string turns it into a charge: Kio holds
// the wind-up while the Scythe gathers, and letting go once it is full unleashes that strike's charged art. The string
// carries on afterwards, so a charged third strike still leads into the fourth.
public partial class Player
{
    private enum ChargedMove
    {
        None,
        RisingReap,
        Whirlwind,
        Cataclysm,
        MeteorSlam,
    }

    private const float CHARGE_HOLD_TIME = 0.26f;
    private const float CHARGE_FULL_TIME = 0.4f;
    private const float CHARGE_MAX_TIME = 1.2f;
    private const float CHARGE_STAMINA = 8f;
    private const float MOMENTUM_PER_STEP = 0.07f;
    private const float JUGGLE_LIFT = 150f;
    private const float SKY_DESCENT_HIT = 5f;
    private const float SKY_DESCENT_MISS = 55f;
    private const float SKY_HIT_LINGER = 0.75f;
    private const float ECHO_DELAY = 0.2f;
    private const float ECHO_RADIUS = 46f;
    private const float RISING_REAP_TIME = 0.32f;
    private const float WHIRLWIND_TIME = 0.66f;
    private const float WHIRLWIND_TICK = 0.11f;
    private const float WHIRLWIND_RADIUS = 42f;
    private const float CATACLYSM_IMPACT = 0.3f;
    private const float CATACLYSM_AFTERSHOCK = 0.2f;
    private const float CATACLYSM_TIME = 0.75f;
    private const float METEOR_SPEED = 600f;
    private const float METEOR_TIMEOUT = 1.2f;

    private ChargedMove chargedMove;
    private bool attackHeld;
    private bool chargeReleased;
    private bool chargeFull;
    private float chargeTimer;
    private float chargeFxTimer;
    private float moveTimer;
    private float moveTickTimer;
    private bool moveImpactDone;
    private bool aftershockDone;
    private float meteorStartZ;
    private Vector2 moveOrigin;
    private float echoTimer = -1f;
    private Vector2 echoPoint;
    private Vector2 echoDirection;

    private static bool Learned(CombatSkill skill) => CombatSkills.Has(skill);

    private float GroundZ => float.IsNegativeInfinity(SupportZ) ? 0f : SupportZ;

    private float AirDescentHit => Learned(CombatSkill.SkyRhythm) ? SKY_DESCENT_HIT : AIR_DESCENT_HIT;
    private float AirDescentMiss => Learned(CombatSkill.SkyRhythm) ? SKY_DESCENT_MISS : AIR_DESCENT_MISS;
    private float AirHitLinger => Learned(CombatSkill.SkyRhythm) ? SKY_HIT_LINGER : AIR_HIT_LINGER;

    private float StrikeStaminaScale => !IsGrounded && Learned(CombatSkill.SkyRhythm) ? 0.5f : 1f;

    // Each strike deeper into the string hits a little harder.
    private float MomentumMultiplier => Learned(CombatSkill.Momentum) ? 1f + MOMENTUM_PER_STEP * Mathf.Max(comboStep, 0) : 1f;

    private float JuggleLift => !IsGrounded && Learned(CombatSkill.Juggle) ? JUGGLE_LIFT : 0f;

    // The art this strike would charge into right now. In the air the Meteor Slam takes over any strike.
    private ChargedMove ChargeableMove()
    {
        if (Stamina <= 0f || scytheThrown)
            return ChargedMove.None;
        if (!IsGrounded)
            return Learned(CombatSkill.MeteorSlam) ? ChargedMove.MeteorSlam : ChargedMove.None;
        return comboStep switch
        {
            0 when Learned(CombatSkill.RisingReap) => ChargedMove.RisingReap,
            2 when Learned(CombatSkill.Whirlwind) => ChargedMove.Whirlwind,
            4 when Learned(CombatSkill.Cataclysm) => ChargedMove.Cataclysm,
            _ => ChargedMove.None,
        };
    }

    // Called each frame of a strike. True once Strike has been held long enough for the strike to become a charge.
    private bool UpdateHold(float t, out bool holding)
    {
        attackHeld &= Input.IsActionPressed(StringNames.Of(GameInput.ATTACK));
        holding = attackHeld && ChargeableMove() != ChargedMove.None;
        return holding && t >= Mathf.Max(CHARGE_HOLD_TIME, StrikeTime + 0.05f);
    }

    private void EnterCharge()
    {
        chargedMove = ChargeableMove();
        chargeReleased = false;
        chargeFull = false;
        chargeTimer = 0f;
        chargeFxTimer = 0f;
        MoveVelocity *= 0.3f;
        Animator.Hold("Attack_" + PlayerAnimator.Direction8(attackDirection), 0.02f);
        Scythe.Manifest();
        Scythe.BeginSwing(attackDirection, Mathf.Pi * 0.4f, !swingClockwise, CHARGE_MAX_TIME, 0.2f);
        Sfx.Play("swing1", -10f, 0.6f);
    }

    private void UpdateCharge(float delta)
    {
        if (chargeReleased)
        {
            UpdateChargedMove(delta);
            return;
        }

        chargeTimer += delta;
        MoveVelocity = Push.Decay(MoveVelocity, 14f, delta);
        attackDirection = PlayerAnimator.Snap8(AimDirection);
        Facing = attackDirection;
        Animator.Hold("Attack_" + PlayerAnimator.Direction8(attackDirection), 0.02f);
        // The Meteor Slam hangs Kio in the air while it gathers.
        if (chargedMove == ChargedMove.MeteorSlam)
        {
            GravityScale = 0f;
            ZVelocity = Mathf.MoveToward(ZVelocity, 0f, 2400f * delta);
        }
        UpdateChargeFx(delta);

        if (Consume(BufferedAction.Dash) && CanDash())
        {
            StateMachine.Transition(PlayerState.Dash);
            return;
        }
        bool held = Input.IsActionPressed(StringNames.Of(GameInput.ATTACK));
        if (held && chargeTimer < CHARGE_MAX_TIME)
            return;
        if (!chargeFull)
        {
            StateMachine.Transition(PlayerState.Locomotion);
            return;
        }
        ReleaseCharge();
    }

    // Motes are drawn in toward the blade, faster as the charge fills; a full charge flashes once.
    private void UpdateChargeFx(float delta)
    {
        Color color = SlashColor;
        Vector2 hand = HandPosition;
        float fill = Mathf.Clamp(chargeTimer / CHARGE_FULL_TIME, 0f, 1f);
        Body.Scale = new Vector2(1f + 0.06f * fill, 1f - 0.06f * fill);
        chargeFxTimer -= delta;
        if (chargeFxTimer <= 0f)
        {
            chargeFxTimer = chargeFull ? 0.12f : Mathf.Lerp(0.09f, 0.04f, fill);
            Vfx.Ring(World.Effects, hand, 18f - 6f * fill, 3f, new Color(color, 0.35f + 0.4f * fill), 0.16f, 1.5f, 0.7f);
            Vector2 from = hand + Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * 16f;
            Vfx.Burst(World.Effects, from, 2, color, 60f, hand - from, 20f, 0.18f, 0f, 1.4f, 0f, true);
        }
        if (chargeFull || chargeTimer < CHARGE_FULL_TIME)
            return;
        chargeFull = true;
        Vfx.ImpactStar(World.Effects, hand, 14f, Colors.White);
        Vfx.Ring(World.Effects, hand, 4f, 22f, new Color(color, 0.9f), 0.2f, 2f, 0.7f);
        Sfx.Play("catch", -4f, 1.3f);
        World.Camera.Feedback.Punch(0.012f);
    }

    private void ReleaseCharge()
    {
        chargeReleased = true;
        moveTimer = 0f;
        moveTickTimer = 0f;
        moveImpactDone = false;
        aftershockDone = false;
        hitThisAction.Clear();
        actionStaminaCost = CHARGE_STAMINA;
        SpendStamina(actionStaminaCost);
        TimeSinceAttackStart = 0f;
        attackDirection = PlayerAnimator.Snap8(ApplyAimAssist(AimDirection));
        Facing = attackDirection;
        string clip = "Attack_" + PlayerAnimator.Direction8(attackDirection);
        switch (chargedMove)
        {
            case ChargedMove.RisingReap:
                Animator.PlayOneShot(clip, RISING_REAP_TIME);
                Scythe.BeginSwing(attackDirection, Mathf.Pi * 1.1f, swingClockwise, 0.04f, 0.18f);
                Launch(JumpVelocity * 0.95f);
                Body.Scale = new Vector2(0.78f, 1.28f);
                MoveVelocity = attackDirection * 40f;
                RisingReapHits();
                Sfx.Play("swing3", -2f, 1.15f);
                break;
            case ChargedMove.Whirlwind:
                Animator.PlayOneShot(clip, WHIRLWIND_TIME);
                Scythe.Grind(attackDirection, 0.02f, WHIRLWIND_TIME);
                Sfx.Play("throw", -2f, 0.9f);
                break;
            case ChargedMove.Cataclysm:
                Animator.PlayOneShot(clip, CATACLYSM_IMPACT / PlayerAnimator.ATTACK_STRIKE_FRACTION);
                Scythe.BeginSwing(attackDirection, Mathf.Pi * 0.9f, swingClockwise, CATACLYSM_IMPACT, 0.2f);
                GravityScale = SLAM_GRAVITY_SCALE;
                ZVelocity = GRAVITY * SLAM_GRAVITY_SCALE * CATACLYSM_IMPACT * 0.5f;
                Body.Scale = new Vector2(0.8f, 1.22f);
                moveOrigin = GlobalPosition + attackDirection * 14f;
                MoveVelocity = attackDirection * 50f;
                Sfx.Play("swing3", -2f, 0.7f);
                break;
            case ChargedMove.MeteorSlam:
                Animator.PlayOneShot(clip, 0.2f);
                Scythe.BeginSwing(attackDirection, Mathf.Pi * 0.8f, swingClockwise, 0.06f, 0.2f);
                meteorStartZ = Z - GroundZ;
                GravityScale = SLAM_GRAVITY_SCALE;
                ZVelocity = -METEOR_SPEED;
                Body.Scale = new Vector2(0.8f, 1.25f);
                Sfx.Play("dash", -2f, 0.7f);
                break;
        }
    }

    private void UpdateChargedMove(float delta)
    {
        moveTimer += delta;
        switch (chargedMove)
        {
            case ChargedMove.RisingReap:
                MoveVelocity = Push.Decay(MoveVelocity, 6f, delta);
                if (moveTimer >= RISING_REAP_TIME)
                    StateMachine.Transition(PlayerState.Locomotion);
                return;
            case ChargedMove.Whirlwind:
                UpdateWhirlwind(delta);
                return;
            case ChargedMove.Cataclysm:
                UpdateCataclysm(delta);
                return;
            case ChargedMove.MeteorSlam:
                UpdateMeteor();
                return;
        }
    }

    private void RisingReapHits()
    {
        Vector2 hand = HandPosition;
        Vfx.Slash(World.Effects, hand, attackDirection.Angle(), Mathf.Pi * 1.1f, TelekineticScythe.VISUAL_REACH * 1.1f, 5f, new Color(SlashColor, 0.85f), swingClockwise, 0.26f);
        Vfx.Burst(World.Effects, hand, 14, SlashColor, 170f, Vector2.Up, 40f, 0.4f, 200f, 1.8f, 60f, true);
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z), 8, new Color(0.75f, 0.72f, 0.6f, 0.7f), 60f, Vector2.Up, 160f, 0.3f, 0f, 1.5f, 80f);
        foreach (IHittable target in World.CollectPlayerTargets())
        {
            Vector2 to = target.GroundPosition - GlobalPosition;
            float distance = to.Length();
            if (distance > 40f + target.HurtRadius || !IsWithinVerticalReach(target.Z, target.HeightSpan))
                continue;
            if (distance > target.HurtRadius && Mathf.Abs(attackDirection.AngleTo(to)) > Mathf.Pi * 0.6f)
                continue;
            HitInfo hit = BuildHit(target, 1.3f * MomentumMultiplier, CurrentArc.PrimaryDamageType, attackDirection, 40f, 18f, 0.05f, false);
            hit.Lift = JumpVelocity;
            airHitTimer = AirHitLinger;
            DeliverHit(target, hit, 0.18f);
        }
    }

    private void UpdateWhirlwind(float delta)
    {
        Vector2 steer = MoveInput.Length() > 0.2f ? MoveInput.Normalized() : attackDirection;
        MoveVelocity = MoveVelocity.MoveToward(steer * 70f, 600f * delta);
        moveTickTimer -= delta;
        if (moveTickTimer <= 0f)
        {
            moveTickTimer = WHIRLWIND_TICK;
            Vector2 center = GlobalPosition + new Vector2(0, -Z - 8f);
            float spin = (float)GD.RandRange(0, Mathf.Tau);
            Vfx.Slash(World.Effects, center, spin, Mathf.Tau * 0.9f, WHIRLWIND_RADIUS * 0.8f, 4f, new Color(SlashColor, 0.75f), true, 0.16f);
            Vfx.Ring(World.Effects, GlobalPosition + new Vector2(0, -Z), 8f, WHIRLWIND_RADIUS, new Color(SlashColor, 0.4f), 0.2f, 2f);
            Vfx.Burst(World.Effects, center, 6, SlashColor, 150f, Vector2.FromAngle(spin), 180f, 0.22f, 0f, 1.6f, 120f, true);
            Sfx.Play("swing2", -8f, 1.2f + GD.Randf() * 0.2f);
            Body.Scale = new Vector2(1.08f, 0.94f);
            foreach (IHittable target in World.CollectPlayerTargets())
            {
                Vector2 to = target.GroundPosition - GlobalPosition;
                if (to.Length() > WHIRLWIND_RADIUS + target.HurtRadius || !IsWithinVerticalReach(target.Z, target.HeightSpan))
                    continue;
                // The cyclone drags foes around it rather than flinging them out of reach.
                Vector2 pull = (to.Orthogonal() * (swingClockwise ? 1f : -1f) - to * 0.4f).Normalized();
                HitInfo hit = BuildHit(target, 0.6f * MomentumMultiplier, CurrentArc.PrimaryDamageType, pull, 60f, 7f, 0.02f, false);
                DeliverHit(target, hit, 0.06f);
            }
        }
        if (moveTimer >= WHIRLWIND_TIME)
            StateMachine.Transition(PlayerState.Locomotion);
    }

    private void UpdateCataclysm(float delta)
    {
        MoveVelocity = Push.Decay(MoveVelocity, 10f, delta);
        if (!moveImpactDone && moveTimer >= CATACLYSM_IMPACT)
        {
            moveImpactDone = true;
            moveOrigin = GlobalPosition + attackDirection * 14f;
            Body.Scale = new Vector2(1.35f, 0.72f);
            Blast(moveOrigin, 68f, 2.4f, 380f, 45f, 170f, 0.08f, 0.4f, 3);
            Sfx.Play("hit_heavy", 0f, 0.6f);
        }
        if (moveImpactDone && !aftershockDone && moveTimer >= CATACLYSM_IMPACT + CATACLYSM_AFTERSHOCK)
        {
            aftershockDone = true;
            Blast(moveOrigin, 100f, 0.8f, 220f, 15f, 0f, 0.03f, 0.2f, 1);
            Sfx.Play("hit_heavy", -4f, 0.9f);
        }
        if (moveTimer >= CATACLYSM_TIME)
            StateMachine.Transition(PlayerState.Locomotion);
    }

    private void UpdateMeteor()
    {
        if (!IsGrounded)
        {
            if ((int)(moveTimer * 60f) % 2 == 0)
                Vfx.AfterImage(World.Actors, this, Sprite, Z, new Color(SlashColor, 0.45f));
            if (moveTimer >= METEOR_TIMEOUT)
                StateMachine.Transition(PlayerState.Locomotion);
            return;
        }
        if (!moveImpactDone)
        {
            moveImpactDone = true;
            moveTimer = 0f;
            float height = Mathf.Max(meteorStartZ, 0f);
            float radius = Mathf.Min(40f + height * 0.4f, 96f);
            Body.Scale = new Vector2(1.4f, 0.7f);
            Blast(GlobalPosition, radius, 1.5f + Mathf.Min(height * 0.01f, 1.2f), 320f, 30f, 120f, 0.07f, 0.3f + Mathf.Min(height * 0.002f, 0.2f), 2);
            Sfx.Play("hit_heavy", 0f, 0.7f);
        }
        MoveVelocity = Vector2.Zero;
        if (moveTimer >= 0.22f)
            StateMachine.Transition(PlayerState.Locomotion);
    }

    // A ground impact: rings out from the point and hits everything inside, pushing it away from the center.
    private void Blast(Vector2 point, float radius, float multiplier, float knockback, float tenacity, float lift, float hitStop, float trauma, int rings)
    {
        Vector2 visual = point + new Vector2(0, -GroundZ);
        for (int i = 0; i < rings; i++)
            Vfx.Ring(World.Effects, visual, 6f + i * 4f, radius * (1f - i * 0.18f), new Color(SlashColor, 0.85f - i * 0.2f), 0.3f + i * 0.08f, 4f - i);
        Vfx.Burst(World.Effects, visual, 10 + rings * 6, new Color(0.62f, 0.58f, 0.52f), 140f + radius, Vector2.Up, 180f, 0.45f, 320f, 2.2f, 50f);
        Vfx.ImpactStar(World.Effects, visual + new Vector2(0, -5f), 18f + rings * 3f, Colors.White);
        World.Camera.Feedback.AddTrauma(trauma);
        World.Camera.Feedback.Punch(0.03f);
        foreach (IHittable target in World.CollectPlayerTargets())
        {
            Vector2 to = target.GroundPosition - point;
            if (to.Length() > radius + target.HurtRadius || !IsWithinVerticalReach(target.Z, target.HeightSpan))
                continue;
            Vector2 direction = to.Length() > 1f ? to.Normalized() : attackDirection;
            HitInfo hit = BuildHit(target, multiplier * MomentumMultiplier, CurrentArc.PrimaryDamageType, direction, knockback, tenacity, hitStop, true);
            hit.Lift = lift;
            DeliverHit(target, hit, trauma * 0.3f);
        }
    }

    private void ExitCharge()
    {
        GravityScale = 1f;
        // The string carries on past a charged strike, as if it had been a plain one.
        comboContinueTimer = COMBO_CONTINUE_WINDOW;
        if (IsLastStrike)
            comboStep = -1;
    }

    // The Echo replays the finisher's impact a beat later, where it landed.
    private void QueueEcho()
    {
        if (!Learned(CombatSkill.Echo))
            return;
        echoTimer = ECHO_DELAY;
        echoPoint = GlobalPosition + attackDirection * 18f;
        echoDirection = attackDirection;
    }

    private void UpdateEcho(float delta)
    {
        if (echoTimer < 0f)
            return;
        echoTimer -= delta;
        if (echoTimer > 0f)
            return;
        echoTimer = -1f;
        Color ghost = new(SlashColor.Lightened(0.4f), 0.6f);
        Vector2 visual = echoPoint + new Vector2(0, -GroundZ - 8f);
        Vfx.Slash(World.Effects, visual - echoDirection * 14f, echoDirection.Angle(), Mathf.Pi * 0.8f, TelekineticScythe.VISUAL_REACH, 4f, ghost, false, 0.25f);
        Vfx.Ring(World.Effects, visual, 4f, ECHO_RADIUS, ghost, 0.28f, 3f);
        Vfx.ImpactStar(World.Effects, visual, 12f, ghost);
        Sfx.Play("swing3", -6f, 1.3f);
        foreach (IHittable target in World.CollectPlayerTargets())
        {
            Vector2 to = target.GroundPosition - echoPoint;
            if (to.Length() > ECHO_RADIUS + target.HurtRadius)
                continue;
            HitInfo hit = BuildHit(target, 0.7f, CurrentArc.PrimaryDamageType, to.Length() > 1f ? to.Normalized() : echoDirection, 160f, 12f, 0.03f, false);
            DeliverHit(target, hit, 0.1f);
        }
    }
}
