using Godot;
using System.Collections.Generic;

public abstract partial class Enemy : Entity
{
    private static readonly List<Enemy> ActiveEnemies = new();
    private const float CAMERA_FOCUS_RANGE = 500f;

    public Player Player { get; private set; }
    private WeaponArc WeaponArc => Player?.Weapon?.GetCurrentArc();
    private EnemyStateMachine StateMachine;
    public TenacitySystem TenacitySystem;

    public Texture2D Tenacity_Bar_Normal = ResourceLoader.Load<Texture2D>("uid://brl8bddmdruyt");
    public Texture2D Tenacity_Bar_Active = ResourceLoader.Load<Texture2D>("uid://b2fo3fwaguoep");
    public Texture2D Tenacity_Bar_Cooldown = ResourceLoader.Load<Texture2D>("uid://0s2uvwfy3s50");
    private RichTextLabel testDisplay;
    private TextureProgressBar TenacityBar;
    public AnimatedSprite2D TenacityCooldownCue { get; private set; }

    [Export] public float armor { get; set; } = 0f;
    [Export] public float health { get; set; } = 100f;
    [Export] public float maxHealth { get; set; } = 100f;
    [Export] public float speed { get; set; } = 80f;
    [Export] public float damage { get; set; } = 10f;
    [Export] public float vesselReward { get; set; } = 10f;
    [Export] public float soulReward { get; set; } = 50f;
    protected float cameraPriority = 1f;

    [Export] public float outsideKnockbackForce { get; set; } = 1f;
    [Export] public float tenacity { get => _tenacity; set => _tenacity = Mathf.Clamp(value, 0f, 10f); }
    [Export] public float maxTenacity { get; set; }
    [Export] public int maxStaggers { get; set; }
    private float _tenacity;
    
    [Export] public DamageType damageType { get; set; }
    [Export] public WeaknessType weaknessType { get; set; }
    public enum DamageType { Normal, Corrupted }
    public enum WeaknessType { Piercing, Slashing, Smashing }
    public enum DificultyScaling { Regular, Corrupted }

    [Export] public float DefaultStaggerDuration { get; set; } = .5f;
    [Export] public float DefaultRecoveryDuration { get; set; } = 5f;
    [Export] public float DefaultKnockbackDuration { get; set; } = 0.2f;
    [Export] public float ChaseRange { get; set; } = 500f;
    [Export] public float AttackRange { get; set; } = 50f;
    [Export] public float StopDistance { get; set; } = 20f;

    public float attackDuration;
    private float hitTimer = 0f;
    private const float HIT_WINDOW = 1.5f;
    
    public bool IsStaggered => TenacitySystem?.IsStaggered ?? false;
    public bool IsInStaggerWindow => TenacitySystem?.IsInStaggerWindow ?? false;
    public bool IsRecovering => TenacitySystem?.IsRecovering ?? false;
    public bool IsDead => StateMachine?.IsDead ?? false;
    public int CurrentStaggers => TenacitySystem?.CurrentStaggerCount ?? 0;
    public float CameraPriority => cameraPriority;
    public bool HasCameraFocus => HasBeenHit && !IsDead && IsWithinCameraFocusRange();

    private bool HasBeenHit = false;

    public void MarkCameraFocus() { HasBeenHit = true; }
    private bool IsWithinCameraFocusRange() { if (Player == null) return false; return GlobalPosition.DistanceTo(Player.GlobalPosition) <= CAMERA_FOCUS_RANGE; }

    public override void _Ready()
    {
        base._Ready();
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }

        InitializeEnemy();
    }

    public override void _ExitTree()
    {
        ActiveEnemies.Remove(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (IsDead)
            return;
        
        UpdateHitTimer((float)delta);
        TenacitySystem.Process((float)delta);
        UpdateResourceBars();
        DisplayStats();
    }

    public static Enemy GetBestCameraTarget(Vector2 cursorPosition)
    {
        Enemy bestEnemy = null;
        float bestScore = float.MaxValue;

        foreach (Enemy enemy in ActiveEnemies)
        {
            if (enemy == null || !GodotObject.IsInstanceValid(enemy) || enemy.IsDead || !enemy.HasCameraFocus)
            {
                continue;
            }

            float cursorDistance = cursorPosition.DistanceTo(enemy.GlobalPosition);
            float healthRatio = enemy.maxHealth > 0f ? enemy.health / enemy.maxHealth : 1f;
            float score = cursorDistance + (healthRatio * 150f) - (enemy.CameraPriority * 200f);

            if (score < bestScore)
            {
                bestScore = score;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    protected virtual void OnDeath()
    {
        float currentVessel = Player.Instance.Stats.GetCurrent("Vessel");
        float maxVessel = Player.Instance.Stats.GetCurrentMax("Vessel");
        Player.Instance.Stats.SetCurrent("Vessel", Mathf.Min(currentVessel + vesselReward, maxVessel));
        
        float currentSoul = Player.Instance.Stats.GetCurrent("Soul");
        float maxSoul = Player.Instance.Stats.GetCurrentMax("Soul");
        Player.Instance.Stats.SetCurrent("Soul", Mathf.Min(currentSoul + soulReward, maxSoul));
    }


    private void UpdateHitTimer(float delta) { if (hitTimer > 0) hitTimer -= delta; }

    private void DisplayStats()
    {
        if (testDisplay == null) return;

        bool shouldShowStats = HasCameraFocus;
        testDisplay.Visible = shouldShowStats;
        if (!shouldShowStats)
            return;
        
        string stateInfo = StateMachine != null ? StateMachine.CurrentState.ToString() : "Unknown";
        
        testDisplay.Text = $"[color=yellow]State:[/color] {stateInfo} \n" +
                           $"\n[color=red]Health:[/color] {GetHealth():F0} / {GetMaxHealth():F0} [color=gray]Armor:[/color] {armor}" +
                           $"\n[color=green]Tenacity:[/color] {tenacity:F1} / 10 [color=orange]Staggers:[/color] {CurrentStaggers} / {maxStaggers}" +
                           $"\n[color=cyan]Weakness:[/color] {weaknessType}";
    }



protected override void UpdateResourceBars()
{
    base.UpdateResourceBars();

    if (HealthBar != null)
    {
        float healthMax = Mathf.Max(GetMaxHealth(), 1f);
        HealthBar.MaxValue = healthMax;
        HealthBar.Value = Mathf.Clamp(GetHealth(), 0f, healthMax);
    }

    // Ensure we cast TenacityBar to TextureProgressBar to access TextureProgress
    if (TenacityBar is TextureProgressBar texTenacityBar)
    {
        float tenacityMax = Mathf.Max(maxTenacity, 1f);
        texTenacityBar.MaxValue = tenacityMax;
        
        float clampedTenacity = Mathf.Clamp(tenacity, 0f, tenacityMax);
        texTenacityBar.Value = tenacityMax - clampedTenacity;

        // Handle the texture swap
        UpdateTenacityTexture(texTenacityBar);
    }
}

private void UpdateTenacityTexture(TextureProgressBar bar)
{
    bool isInKnockbackWindow = TenacitySystem?.IsKnockbackActive ?? false;
    bool isInStaggerWindow = TenacitySystem?.IsInStaggerWindow ?? false;
    bool isInRecovery = TenacitySystem?.IsInRecoveryCooldown ?? false;

    // Show active texture during the stagger window or while knockback is active,
    // show cooldown during recovery, otherwise show normal (progress) texture.
    if (isInStaggerWindow || isInKnockbackWindow)
    {
        if (Tenacity_Bar_Active != null)
            bar.TextureProgress = Tenacity_Bar_Active;
    }
    else if (isInRecovery)
    {
        if (Tenacity_Bar_Cooldown != null)
            bar.TextureProgress = Tenacity_Bar_Cooldown;
    }
    else
    {
        if (Tenacity_Bar_Normal != null)
            bar.TextureProgress = Tenacity_Bar_Normal;
    }
}
}
