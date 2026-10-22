using Godot;

namespace Anomaly;

public partial class Player
{
    public const float VESSEL_MAXIMUM = 100f;
    private const float VESSEL_HELD_MAXIMUM = 99f;
    public const float CORRUPTION_MAXIMUM = 100f;
    private const float VESSEL_SCALING = 1.3f;
    private const int VESSEL_CONSECUTIVE_CAP = 8;
    private const float CORRUPTION_PER_HIT = 1.4f;
    private const float CORRUPTION_PER_KILL = 7f;
    private const float AETHERIAC_BASE_LIFESTEAL = 0.35f;
    private const float AETHERIAC_LIFESTEAL_GROWTH = 0.05f;
    private const float AETHERIAC_MAX_LIFESTEAL = 1f;
    private const float AETHERIAC_BASE_DRAIN = 2f;
    private const float AETHERIAC_DRAIN_ACCELERATION = 2.4f;
    private const float HEAL_FILL_DURATION = 1.2f;

    public float Stamina { get; private set; }
    public float Vessel { get; private set; }
    // The Health Star holds one heal. A full Vessel pours into an empty star; while the star is full the Vessel caps at 99 and waits.
    public float HealCharge { get; private set; }
    public bool HealReadyNow => HealCharge >= 1f && !healFilling;
    public float Corruption { get; private set; }
    public bool AetheriacActive { get; private set; }
    public int Souls { get; private set; }
    public float Potency => SaveManager.PlayerStats.VesselPotency(BasePotency) * Amulets.Multiplier(AmuletStat.Healing);

    private float staminaRegenCooldown;
    private int consecutiveHits;
    private float aetheriacTime;
    private float heartbeatTimer;
    private bool corruptionFullAnnounced;
    private bool healFilling;
    private bool healConsumed;
    private SpriteSheetEffect healEffect;

    private void InitializeResources()
    {
        Stamina = MaxStamina;
        Souls = SaveManager.Progression.HeldSouls;
        Amulets.GrantDebug();
        string arcId = string.IsNullOrEmpty(SaveManager.Progression.EquippedArc) ? ArcLibrary.DEFAULT_ARC : SaveManager.Progression.EquippedArc;
        CurrentArc = ArcLibrary.Get(arcId);
    }

    public void BroadcastAll()
    {
        PlayerSignals.EmitHealthChanged(Health, MaxHealth);
        PlayerSignals.EmitStaminaChanged(Stamina, MaxStamina);
        PlayerSignals.EmitVesselChanged(Vessel, VESSEL_MAXIMUM, HealCharge);
        PlayerSignals.EmitCorruptionChanged(Corruption, CORRUPTION_MAXIMUM, AetheriacActive);
        PlayerSignals.EmitSoulsChanged(Souls);
        PlayerSignals.EmitArcChanged(CurrentArc);
        EmitResonance();
    }

    private static void EmitResonance()
    {
        PlayerStats stats = SaveManager.PlayerStats;
        PlayerSignals.EmitResonanceChanged(stats.Resonance, stats.Residue, LevelingSystem.ResidueToNext(stats.Resonance), stats.UnspentPoints);
    }

    public void AddResidue(int amount)
    {
        if (amount <= 0 || IsDead)
            return;
        int gained = LevelingSystem.AddResidue(SaveManager.PlayerStats, amount);
        if (gained > 0)
            OnResonanceGained();
        EmitResonance();
    }

    // Level-up lands as one beat: a white freeze on Kio, a ring that cuts outward, and the new maximum filled in.
    private void OnResonanceGained()
    {
        RefreshMaxHealth();
        Stamina = MaxStamina;
        int resonance = SaveManager.PlayerStats.Resonance;
        Vector2 center = GlobalPosition + new Vector2(0, -Z - 12f);
        Color tone = new(0.72f, 0.9f, 1f);
        Flash(0.18f, Colors.White);
        Sfx.Play("level_up", -3f);
        World.PostFx.Flash(tone, 0.22f);
        World.Camera.Feedback.Punch(0.04f);
        Vfx.Ring(World.Effects, center, 4f, 46f, tone, 0.4f, 3f, 0.9f);
        Vfx.Ring(World.Effects, center, 2f, 26f, Colors.White, 0.22f, 2f, 1f);
        Vfx.Burst(World.Effects, center, 22, tone, 120f, Vector2.Up, 180f, 0.7f, -50f, 2f, 60f, true);
        Vfx.Number(World.Overlay, center + new Vector2(0, -22f), $"RESONANCE {resonance}", tone, 9);
        PlayerSignals.EmitResonanceGained(resonance);
        World.Hud.Toast($"Resonance {resonance}.  Attune at a bonfire.");
        World.Speak("voice_first_resonance", "Their residue settles in you. You grow. Is that not what a Hunter is for?");
    }

    private void UpdateResources(float delta)
    {
        if (staminaRegenCooldown > 0f)
            staminaRegenCooldown -= delta;
        else if (Stamina < MaxStamina && !StateMachine.Is(PlayerState.Dash))
            Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegenRate * delta);

        if (AetheriacActive)
            UpdateAetheriacState(delta);
        if (healFilling)
            UpdateHealFill(delta);
        UpdateGodMode();

        PlayerSignals.EmitHealthChanged(Health, MaxHealth);
        PlayerSignals.EmitStaminaChanged(Stamina, MaxStamina);
        PlayerSignals.EmitCorruptionChanged(Corruption, CORRUPTION_MAXIMUM, AetheriacActive);
        PlayerSignals.EmitSpecialReadinessChanged(SpecialReadiness);
    }

    // Emptying the bar winds Kio: regen waits longer, so spending the last point is a real commitment.
    private void SpendStamina(float amount)
    {
        if (GodMode)
            return;
        Stamina = Mathf.Max(0f, Stamina - amount);
        staminaRegenCooldown = Stamina <= 0f ? ExhaustedRegenDelay : StaminaRegenDelay;
    }

    private void RestoreStamina(float amount) => Stamina = Mathf.Min(MaxStamina, Stamina + amount);

    private void RegisterLivingHit(float damageDealt, bool killed)
    {
        if (AetheriacActive)
        {
            RestoreHealth(damageDealt * AetheriacLifesteal);
            Vfx.Burst(World.Effects, GlobalPosition + new Vector2(0, -Z - 12f), 3, new Color(1f, 0.2f, 0.3f), 40f, Vector2.Up, 120f, 0.35f, 0f, 1.6f, 30f, true);
        }
        else
        {
            consecutiveHits = Mathf.Min(consecutiveHits + 1, VESSEL_CONSECUTIVE_CAP);
            AddVessel(consecutiveHits * VESSEL_SCALING);
            AddCorruption((CORRUPTION_PER_HIT + (killed ? CORRUPTION_PER_KILL : 0f)) * LevelingSystem.CorruptionMultiplier(SaveManager.PlayerStats));
        }
    }

    // A full Vessel pours into an empty star and restarts from 0. While the star holds a heal the Vessel stops one
    // short of full; the next hit after the heal is spent tips it over and pours again.
    public void AddVessel(float amount)
    {
        Vessel += amount;
        if (Vessel >= VESSEL_MAXIMUM && CanPourVessel())
        {
            Vessel = 0f;
            healFilling = true;
        }
        Vessel = Mathf.Min(Vessel, VESSEL_HELD_MAXIMUM);
        EmitVessel();
    }

    private bool CanPourVessel() => HealCharge <= 0f && !healFilling && !StateMachine.Is(PlayerState.Heal);

    private void UpdateHealFill(float delta)
    {
        HealCharge = Mathf.Min(1f, HealCharge + delta / HEAL_FILL_DURATION);
        if (HealCharge >= 1f)
        {
            healFilling = false;
            Sfx.Play("mark_ready", -4f);
            PlayerSignals.EmitHealReady();
            World.Speak("voice_first_mark", "Their residue is yours. Take it. It will keep you standing.");
        }
        EmitVessel();
    }

    private void SetHealCharge(float charge)
    {
        HealCharge = charge;
        EmitVessel();
    }

    private void EmitVessel() => PlayerSignals.EmitVesselChanged(Vessel, VESSEL_MAXIMUM, HealCharge);

    private void ConsumeHeal()
    {
        healConsumed = true;
        HealCharge = 0f;
        float amount = Potency;
        RestoreHealth(amount);
        Sfx.Play("heal", -3f);
        Flash(0.25f, new Color(0.8f, 0.6f, 1f));
        Vfx.Number(World.Overlay, GlobalPosition + new Vector2(0, -Z - 30f), $"+{Mathf.RoundToInt(amount)}", new Color(0.78f, 0.62f, 1f), 9);
        EmitVessel();
    }

    public void AddCorruption(float amount)
    {
        if (AetheriacActive)
            return;
        Corruption = Mathf.Min(CORRUPTION_MAXIMUM, Corruption + amount);
        if (Corruption >= CORRUPTION_MAXIMUM && !corruptionFullAnnounced)
        {
            corruptionFullAnnounced = true;
            Sfx.Play("heartbeat", -2f);
            World.Speak("voice_corruption", "You feel it rising, don't you? Do not fear it. Let it move through you. [R]");
        }
    }

    private void ToggleAetheriacState()
    {
        if (IsDead)
            return;
        if (AetheriacActive)
        {
            ExitAetheriacState();
            return;
        }
        if (Corruption < CORRUPTION_MAXIMUM)
        {
            Sfx.Play("ui_move", -8f, 0.5f);
            return;
        }
        AetheriacActive = true;
        aetheriacTime = 0f;
        heartbeatTimer = 0f;
        Sfx.Play("aetheriac_on", 0f);
        TimeControl.HitStop(0.12f, 0.05f);
        World.Camera.Feedback.AddTrauma(0.5f);
        World.Camera.Feedback.Punch(0.08f);
        World.PostFx.Flash(new Color(1f, 0.1f, 0.2f), 0.6f);
        Vfx.Sheet(World.Effects, GlobalPosition + new Vector2(0, -Z - 6f), "rage", 9, 18f, 1.5f);
        PlayerSignals.EmitAetheriacStateChanged(true);
    }

    private void ExitAetheriacState()
    {
        AetheriacActive = false;
        Corruption = 0f;
        corruptionFullAnnounced = false;
        Sfx.Play("aetheriac_off", -2f);
        Vfx.Sheet(World.Effects, GlobalPosition + new Vector2(0, -Z - 6f), "rage", 9, 18f, 1.5f, true);
        PlayerSignals.EmitAetheriacStateChanged(false);
    }

    // Lifesteal climbs with every second held, racing the drain: staying longer pays more and costs more.
    private float AetheriacLifesteal => Mathf.Min(AETHERIAC_MAX_LIFESTEAL, AETHERIAC_BASE_LIFESTEAL + LevelingSystem.AetheriacLifesteal(SaveManager.PlayerStats) + AETHERIAC_LIFESTEAL_GROWTH * aetheriacTime);

    // The state has no timer. The health bar is the only clock: Kio either releases it or the drain ends Kio.
    // The drain is not damage: it bypasses hit resolution entirely and can end Kio by itself.
    private void UpdateAetheriacState(float delta)
    {
        aetheriacTime += delta;
        float drain = (AETHERIAC_BASE_DRAIN + AETHERIAC_DRAIN_ACCELERATION * aetheriacTime) * LevelingSystem.AetheriacDrainScale(SaveManager.PlayerStats) * delta;
        Health = Mathf.Max(0f, Health - drain);

        heartbeatTimer -= delta;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = Mathf.Max(0.35f, 0.95f - aetheriacTime * 0.05f);
            Sfx.Play("heartbeat", -5f + Mathf.Min(5f, aetheriacTime * 0.4f));
        }
        if (MoveVelocity.Length() > 40f && GD.Randf() < 0.25f)
            Vfx.AfterImage(World.Actors, this, Sprite, Z, new Color(1f, 0.1f, 0.2f, 0.35f), 0.3f);

        if (Health <= 0f)
        {
            ExitAetheriacState();
            Die();
        }
    }

    public void AddSouls(int amount)
    {
        if (GodMode)
        {
            bankedSouls += amount;
            return;
        }
        Souls += amount;
        PlayerSignals.EmitSoulsChanged(Souls);
    }

    public bool SpendSouls(int amount)
    {
        if (Souls < amount)
            return false;
        if (GodMode)
            return true;
        Souls -= amount;
        PlayerSignals.EmitSoulsChanged(Souls);
        return true;
    }

    public void EquipArc(SoulWeaponArc arc)
    {
        CurrentArc = arc;
        SaveManager.Progression.EquippedArc = arc.ArcId;
        PlayerSignals.EmitArcChanged(arc);
    }

    public void RefreshMaxHealth()
    {
        float previous = MaxHealth;
        MaxHealth = ComputeMaxHealth();
        Health += MaxHealth - previous;
    }

    public void RestoreAtRest()
    {
        Health = MaxHealth;
        Stamina = MaxStamina;
        if (AetheriacActive)
            ExitAetheriacState();
        BroadcastAll();
    }
}
