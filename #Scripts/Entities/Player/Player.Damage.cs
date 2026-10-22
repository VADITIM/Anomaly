using Godot;

namespace Anomaly;

public partial class Player
{
    private const float PERFECT_DODGE_WINDOW = 0.16f;
    private const float HURT_INVULNERABILITY = 0.6f;
    private const float VOID_FALL_DAMAGE = 12f;

    public override HitResponse ReceiveHit(HitInfo hit)
    {
        if (IsDead || StateMachine.Is(PlayerState.Falling))
            return HitResponse.Ignored;
        if (GodMode)
            return HitResponse.Dodged;

        if (invulnerableTimer > 0f)
        {
            if (StateMachine.Is(PlayerState.Dash) && perfectDodgeAvailable && StateMachine.TimeInState <= PERFECT_DODGE_WINDOW)
            {
                PerformPerfectDodge();
                return HitResponse.PerfectDodged;
            }
            return HitResponse.Dodged;
        }

        consecutiveHits = 0;
        float taken = Mathf.Round(hit.Damage * Amulets.Multiplier(AmuletStat.DamageTaken));
        Health = Mathf.Max(0f, Health - taken);
        if (!AetheriacActive)
            Impact.Resolve(hit);
        invulnerableTimer = HURT_INVULNERABILITY;

        Flash(0.16f, new Color(1f, 0.25f, 0.3f));
        TimeControl.HitStop(0.06f, 0.02f);
        World.Camera.Feedback.AddTrauma(0.45f);
        World.PostFx.Flash(new Color(0.9f, 0.05f, 0.1f), 0.3f);
        World.PostFx.KickAberration(1f);
        Sfx.Play("hurt", 0f);
        Vector2 center = GlobalPosition + new Vector2(0, -Z - 12f);
        Vfx.Burst(World.Effects, center, 10, new Color(0.75f, 0.08f, 0.15f), 140f, hit.Direction, 50f, 0.4f, 280f, 2f, 40f);
        Vfx.Number(World.Overlay, center + new Vector2(0, -14f), Mathf.RoundToInt(taken).ToString(), new Color(1f, 0.3f, 0.35f), 10);
        PlayerSignals.EmitDamaged();

        if (Health <= 0f)
        {
            Die();
            return HitResponse.Killed;
        }
        if (!AetheriacActive && !StateMachine.Is(PlayerState.Hurt))
            StateMachine.Transition(PlayerState.Hurt);
        return HitResponse.Landed;
    }

    private void PerformPerfectDodge()
    {
        perfectDodgeAvailable = false;
        invulnerableTimer = Mathf.Max(invulnerableTimer, 0.45f);
        Stamina = Mathf.Min(MaxStamina, Stamina + DashStaminaCost);
        TimeControl.SlowMotion(0.22f, 0.5f, 0.04f);
        Sfx.Play("perfect_dodge", 0f);
        World.PostFx.Flash(new Color(0.5f, 0.95f, 1f), 0.25f);
        World.Camera.Feedback.Punch(0.05f);
        Vector2 center = GlobalPosition + new Vector2(0, -Z - 10f);
        Vfx.Sheet(World.Effects, center, "phase", 9, 30f);
        Vfx.Number(World.Overlay, center + new Vector2(0, -18f), "PHASE", new Color(0.55f, 0.95f, 1f), 8);
        AddCorruption(5f);
    }

    protected override void OnFellIntoVoid()
    {
        if (!IsDead)
            StateMachine.Transition(PlayerState.Falling);
    }

    private void EnterFalling()
    {
        stateTimer = 0f;
        MoveVelocity = Vector2.Zero;
        Sfx.Play("fall", -2f);
    }

    private void UpdateFalling(float delta)
    {
        stateTimer += delta;
        if (stateTimer < 0.55f)
            return;
        PlaceAt(lastSafePosition);
        Health = Mathf.Max(0f, Health - VOID_FALL_DAMAGE);
        Vfx.Number(World.Overlay, GlobalPosition + new Vector2(0, -Z - 30f), Mathf.RoundToInt(VOID_FALL_DAMAGE).ToString(), new Color(1f, 0.3f, 0.35f), 10);
        World.PostFx.Flash(new Color(0.3f, 0.05f, 0.4f), 0.4f);
        consecutiveHits = 0;
        if (Health <= 0f)
        {
            Die();
            return;
        }
        invulnerableTimer = 1f;
        StateMachine.Transition(PlayerState.Locomotion);
    }

    private void Die()
    {
        if (IsDead || GodMode)
            return;
        IsDead = true;
        if (AetheriacActive)
            ExitAetheriacState();
        StateMachine.Transition(PlayerState.Dead);
    }

    private void EnterDead()
    {
        stateTimer = 0f;
        MoveVelocity = Vector2.Zero;
        Solid = false;
        Sfx.Play("death", 0f);
        TimeControl.SlowMotion(1.2f, 0.3f);
        World.Camera.Feedback.AddTrauma(0.6f);
        World.PostFx.Flash(new Color(0.6f, 0f, 0.08f), 0.5f);
        Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z - 12f), 24, new Color(0.6f, 0.05f, 0.12f), 160f, Vector2.Up, 180f, 0.8f, 200f, 2.5f, 40f);
        if (Souls > 0)
            World.DropSoulRemnant(lastSafePosition, Souls);
        Souls = 0;
        PlayerSignals.EmitSoulsChanged(Souls);
        PlayerSignals.EmitDied();
    }

    private void UpdateDead(float delta)
    {
        stateTimer += delta;
        Body.Scale = Body.Scale.Lerp(new Vector2(1.3f, 0.2f), 1f - Mathf.Exp(-4f * delta));
        Animator.SetAlpha(Mathf.Clamp(1.4f - stateTimer, 0f, 1f));
    }

    public void Respawn(Vector2 position)
    {
        IsDead = false;
        Solid = true;
        PlaceAt(position);
        Animator.SetAlpha(1f);
        Body.Scale = Vector2.One;
        consecutiveHits = 0;
        Vessel = 0f;
        scytheThrown = false;
        Scythe.Withdraw();
        invulnerableTimer = 1.5f;
        Health = MaxHealth;
        Stamina = MaxStamina;
        StateMachine.Transition(PlayerState.Locomotion);
        BroadcastAll();
        PlayerSignals.EmitRespawned();
    }
}
