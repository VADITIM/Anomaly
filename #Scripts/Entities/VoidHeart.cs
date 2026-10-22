using System.Collections.Generic;
using Godot;

namespace Anomaly;

public enum VoidHeartPhase
{
    Dormant,
    Guarded,
    Exposed,
    Ruptured,
}

// The Voice calls it cleansing. Rupturing a Void Heart releases centuries of stored Corruption at once,
// and Kio is the nearest vessel to absorb it.
public partial class VoidHeart : Entity
{
    private const float ACTIVATION_RADIUS = 105f;

    // Each continent's heart calls on what lives around it.
    public static PlacementKind[][] WavesFor(Biome biome) => biome switch
    {
        Biome.Snow => new[]
        {
            new[] { PlacementKind.Rimeslime, PlacementKind.Hound, PlacementKind.Rimeslime },
            new[] { PlacementKind.Rimebound, PlacementKind.Wasp, PlacementKind.Hound, PlacementKind.GloomBat },
        },
        Biome.Undead => new[]
        {
            new[] { PlacementKind.Gnawer, PlacementKind.Gnawer, PlacementKind.Gnawer, PlacementKind.Bonewalker },
            new[] { PlacementKind.Bonewalker, PlacementKind.GloomBat, PlacementKind.BloatedGnawer, PlacementKind.Bonewalker },
        },
        Biome.BrightForest => new[]
        {
            new[] { PlacementKind.Bogslime, PlacementKind.Gnawer, PlacementKind.Gnawer, PlacementKind.Wasp },
            new[] { PlacementKind.Hound, PlacementKind.Bogslime, PlacementKind.Wasp, PlacementKind.Rotling },
        },
        _ => new[]
        {
            new[] { PlacementKind.Rotling, PlacementKind.Rotling, PlacementKind.Hound },
            new[] { PlacementKind.Wasp, PlacementKind.Shadeflit, PlacementKind.Hound, PlacementKind.Rotling },
        },
    };

    public string HeartId = LevelBlueprint.VOID_HEART_ID;
    public PlacementKind[][] Waves = WavesFor(Biome.Verdant);
    public List<Vector2> RiftPositions = new();
    public VoidHeartPhase Phase { get; private set; } = VoidHeartPhase.Dormant;

    private readonly List<Enemy> waveEnemies = new();
    private int waveIndex;
    private float time;
    private float waveDelay;
    private float pulseTimer;
    private PointLight2D light;

    protected override string DefaultStatsPath => "res://#Resources/EntityStats/VoidHeart.tres";
    public override bool CanReceivePlayerHits => !IsDead && Phase != VoidHeartPhase.Ruptured;
    public override float CameraPriority => Phase == VoidHeartPhase.Exposed ? 2.5f : 0f;

    protected override void InitializeEntity()
    {
        Radius = 14f;
        HurtRadius = 16f;
        HeightSpan = 40f;
        Solid = true;
        light = LightFactory.Point(new Color(0.95f, 0.25f, 0.8f), 0.7f, 1.1f, new Vector2(0, -26));
        Body.AddChild(light);
        Body.AddChild(new VoidHeartVisual { Name = "Visual" });
    }

    public float PulseTime => time;
    public bool Shielded => Phase is VoidHeartPhase.Dormant or VoidHeartPhase.Guarded;

    protected override void TickEntity(float delta)
    {
        time += delta;
        MoveVelocity = Vector2.Zero;
        light.Energy = 0.6f + 0.2f * Mathf.Sin(time * 3f);

        switch (Phase)
        {
            case VoidHeartPhase.Dormant:
                Player player = World.Player;
                if (!player.IsDead && player.GlobalPosition.DistanceTo(GlobalPosition) < ACTIVATION_RADIUS && Mathf.Abs(player.Z - Z) < 20f)
                    BeginEncounter();
                break;
            case VoidHeartPhase.Guarded:
                UpdateWaves(delta);
                break;
            case VoidHeartPhase.Exposed:
                pulseTimer -= delta;
                if (pulseTimer <= 0f)
                {
                    pulseTimer = 2.6f;
                    EmitPulse();
                }
                break;
        }
    }

    public void ResetEncounter()
    {
        if (Phase == VoidHeartPhase.Ruptured)
            return;
        Phase = VoidHeartPhase.Dormant;
        waveEnemies.Clear();
        waveIndex = 0;
        Health = MaxHealth;
    }

    private void BeginEncounter()
    {
        Phase = VoidHeartPhase.Guarded;
        waveIndex = 0;
        waveDelay = 1.2f;
        Sfx.Play("void_rupture", -8f, 1.1f);
        World.Camera.Feedback.AddTrauma(0.35f);
        World.Hud.ShowFocusBar(this, "VOID HEART");
        World.Speak("voice_heart_guarded", "It defends itself. Of course it does. Cut through them.");
    }

    private void UpdateWaves(float delta)
    {
        waveEnemies.RemoveAll(enemy => !IsInstanceValid(enemy) || enemy.IsDead);
        if (waveEnemies.Count > 0)
            return;
        waveDelay -= delta;
        if (waveDelay > 0f)
            return;
        if (waveIndex >= Waves.Length)
        {
            Phase = VoidHeartPhase.Exposed;
            pulseTimer = 2f;
            Sfx.Play("tenacity_break", 0f, 0.6f);
            Vfx.Ring(World.Effects, VisualCenter, 10f, 80f, new Color(0.95f, 0.3f, 0.85f), 0.6f, 5f, 0.8f);
            World.Speak("voice_heart_exposed", "Now. Cleanse it.");
            return;
        }
        PlacementKind[] wave = Waves[waveIndex++];
        for (int i = 0; i < wave.Length; i++)
        {
            Vector2 rift = RiftPositions[i % RiftPositions.Count] + new Vector2((i / RiftPositions.Count) * 14f, 0);
            Enemy enemy = World.SpawnFromRift(wave[i], rift, i * 0.25f);
            waveEnemies.Add(enemy);
        }
        waveDelay = 1.4f;
    }

    private void EmitPulse()
    {
        Vfx.Ring(World.Effects, VisualCenter + new Vector2(0, HeightSpan * 0.5f), 10f, 70f, new Color(0.9f, 0.2f, 0.8f, 0.8f), 0.7f, 4f);
        Sfx.Play("heartbeat", -6f, 0.6f);
        Player player = World.Player;
        if (player.GlobalPosition.DistanceTo(GlobalPosition) < 60f && player.IsWithinVerticalReach(Z, HeightSpan))
        {
            Vector2 away = (player.GlobalPosition - GlobalPosition).Normalized();
            player.ReceiveHit(new HitInfo { Damage = 8f, Direction = away, Knockback = 260f, Source = HitSource.Environment, Attacker = this });
        }
    }

    public override HitResponse ReceiveHit(HitInfo hit)
    {
        if (Phase == VoidHeartPhase.Ruptured)
            return HitResponse.Ignored;
        if (Shielded)
        {
            if (Phase == VoidHeartPhase.Dormant)
                BeginEncounter();
            Sfx.Play("catch", -4f, 0.6f);
            Vfx.Ring(World.Effects, VisualCenter, 18f, 26f, new Color(0.7f, 0.5f, 1f, 0.8f), 0.2f, 2f, 1f);
            Vfx.Number(World.Overlay, VisualCenter + new Vector2(0, -24f), "SEALED", new Color(0.8f, 0.6f, 1f), 8);
            return HitResponse.Landed;
        }
        hit.Knockback = 0f;
        HitResponse response = base.ReceiveHit(hit);
        Vfx.Burst(World.Effects, hit.ContactPoint, 8, new Color(0.9f, 0.2f, 0.8f), 130f, hit.Direction, 60f, 0.5f, 0f, 2.2f, 60f, true);
        Vfx.Number(World.Overlay, VisualCenter + new Vector2(0, -20f), Mathf.RoundToInt(hit.Damage).ToString(), Colors.White, 10);
        return response;
    }

    protected override void OnDeath(HitInfo hit)
    {
        Phase = VoidHeartPhase.Ruptured;
        Solid = false;
        World.RuptureVoidHeart(this);
    }
}
