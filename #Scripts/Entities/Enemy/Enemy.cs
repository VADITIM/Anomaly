using Godot;
using System.Collections.Generic;

public abstract partial class Enemy : Entity
{
    private static readonly List<Enemy> ActiveEnemies = new();
    private const float CAMERA_FOCUS_RANGE = 800f;

    public Player Player { get; private set; }
    private Weapon Weapon => Player?.Weapon;
    private EnemyStateMachine StateMachine;
    public TenacitySystem TenacitySystem;
    private AnimationPlayer AnimationPlayer;
    private Control ResourceBarControl;
    private StyleBox TenacityBarNormalFill;
    private StyleBox TenacityBarKnockbackFill;
    
    private RichTextLabel testDisplay;
    private ProgressBar HealthBar;
    private ProgressBar TenacityBar;
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
    private float _hitTimer = 0f;
    private const float HIT_WINDOW = 1.5f;
    
    public bool IsStaggered => TenacitySystem?.IsStaggered ?? false;
    public bool IsInStaggerWindow => TenacitySystem?.IsInStaggerWindow ?? false;
    public bool IsRecovering => TenacitySystem?.IsRecovering ?? false;
    public bool IsDead => StateMachine?.IsDead ?? false;
    public int CurrentStaggers => TenacitySystem?.CurrentStaggerCount ?? 0;
    public float CameraPriority => cameraPriority;
    public bool HasCameraFocus => _hasBeenHit && !IsDead && IsWithinCameraFocusRange();

    private bool _hasBeenHit = false;

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

    public void InitializeEnemy()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
        testDisplay = GetNodeOrNull<RichTextLabel>("Label")
                      ?? GetNodeOrNull<RichTextLabel>("CanvasLayer/Label")
                      ?? GetTree().CurrentScene?.FindChild("Label", true, false) as RichTextLabel;

        if (testDisplay != null)
            testDisplay.BbcodeEnabled = true;

        AnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        TenacityCooldownCue = GetNodeOrNull<AnimatedSprite2D>("Tenacity Broken Animation");
        TenacityCooldownCue.Visible = false;
        InitiateResourceBars();
        
        maxTenacity = tenacity;
        maxHealth = health;
        
        StateMachine = GetNodeOrNull<EnemyStateMachine>("EnemyStateMachine");
        if (StateMachine == null)
        {
            StateMachine = new EnemyStateMachine();
            StateMachine.Name = "EnemyStateMachine";
            AddChild(StateMachine);
        }
        
        StateMachine.SetMaxStaggers(maxStaggers);
        StateMachine.Target = Player;
        
        StateMachine.OnDied += OnDeathHandler;
        StateMachine.OnStateChanged += OnStateChangedHandler;
        
        TenacitySystem = new TenacitySystem(this, this, StateMachine);

        PlayEnemyAnimation(GetCurrentEnemyAnimation());
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (IsDead || TenacitySystem == null || StateMachine == null)
            return;
        
        UpdateHitTimer((float)delta);
        TenacitySystem.Process((float)delta);
        UpdateResourceBars();
        DisplayStats();
    }

    public void MarkCameraFocus()
    {
        _hasBeenHit = true;
    }

    private bool IsWithinCameraFocusRange()
    {
        if (Player == null) return false;
        return GlobalPosition.DistanceTo(Player.GlobalPosition) <= CAMERA_FOCUS_RANGE;
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


    private void OnStateChangedHandler(EnemyState oldState, EnemyState newState)
    {
        OnStateChanged(oldState, newState);
        PlayEnemyAnimation(GetCurrentEnemyAnimation());
    }
    protected virtual void OnStateChanged(EnemyState oldState, EnemyState newState) { }

    private string GetCurrentEnemyAnimation()
    {
        if (StateMachine == null)
            return null;

        if (StateMachine.IsDead)
            return "Die_Down";

        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Move_Down"))
            return "Move_Down";

        return null;
    }

    private void PlayEnemyAnimation(string animationName)
    {
        if (AnimationPlayer == null || string.IsNullOrEmpty(animationName) || !AnimationPlayer.HasAnimation(animationName))
            return;

        if (AnimationPlayer.CurrentAnimation != animationName)
            AnimationPlayer.Play(animationName);
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

    private void OnDeathHandler()
    {
        OnDeath();

        if (AnimationPlayer != null && AnimationPlayer.HasAnimation("Die_Down"))
        {
            AnimationPlayer.Play("Die_Down");

            float animationLength = AnimationPlayer.GetAnimation("Die_Down").Length;
            SceneTreeTimer timer = GetTree().CreateTimer((double)Mathf.Max(animationLength, 0.1f));
            timer.Timeout += QueueFree;
            return;
        }

        QueueFree();
    }

    private void UpdateHitTimer(float delta)
    {
        if (_hitTimer > 0)
        {
            _hitTimer -= delta;
        }
    }

    public void TakeDamage(Weapon weapon, Vector2 playerPosition)
    {
        if (IsDead) return;

        MarkCameraFocus();
        
        Camera camera = GetViewport().GetCamera2D() as Camera;
        camera?.ShakeCamera(.5f);
        
        float calculatedDamage = weapon.ApplyDamage(this);
        health -= calculatedDamage;
        UpdateResourceBars();
        
        StateMachine.NotifyDamageTaken(calculatedDamage);

        if (health <= 0)
        {
            StateMachine.RequestDeath();
            return;
        }

        _hitTimer = HIT_WINDOW;
        
        bool staggerTriggered = TenacitySystem.ProcessTenacitySystem(playerPosition, weapon);
        
        if (!staggerTriggered)
        {
            if (IsInStaggerWindow)
            {
                ApplyStaggeredHitKnockback(playerPosition, weapon);
            }
            else
            {
                ApplySubtleKnockback(playerPosition, weapon);
            }
        }
        
        if (staggerTriggered)
        {
            camera?.ShakeCamera(5f);
            TenacityCooldownCue.Visible = true;
        }

        UpdateResourceBars();
    }

    private void ApplySubtleKnockback(Vector2 playerPosition, Weapon weapon)
    {
        float subtleForce = 30f;
        
        float weaknessMultiplier = (weaknessType.ToString() == weapon.attackType.ToString()) 
            ? outsideKnockbackForce : 1f;
        
        Vector2 knockbackDirection = (GlobalPosition - playerPosition).Normalized();
        
        TenacitySystem.RequestKnockback(knockbackDirection, subtleForce * weaknessMultiplier, 0.1f);
    }
    
    private void ApplyStaggeredHitKnockback(Vector2 playerPosition, Weapon weapon)
    {
        float staggeredForce = weapon.knockback * 0.5f;
        
        float weaknessMultiplier = (weaknessType.ToString() == weapon.attackType.ToString()) 
            ? outsideKnockbackForce * 1.5f : 1f;
        
        Vector2 knockbackDirection = (GlobalPosition - playerPosition).Normalized();
        
        TenacitySystem.RequestKnockback(knockbackDirection, staggeredForce * weaknessMultiplier * 10f, 0.2f);
    }

    private void DisplayStats()
    {
        if (testDisplay == null) return;

        bool shouldShowStats = HasCameraFocus;
        testDisplay.Visible = shouldShowStats;
        if (!shouldShowStats)
            return;
        
        string stateInfo = StateMachine != null ? StateMachine.CurrentState.ToString() : "Unknown";
        
        testDisplay.Text = $"[color=yellow]State:[/color] {stateInfo} \n" +
                           $"\n[color=red]Health:[/color] {health:F0} / {maxHealth:F0} [color=gray]Armor:[/color] {armor}" +
                           $"\n[color=green]Tenacity:[/color] {tenacity:F1} / 10 [color=orange]Staggers:[/color] {CurrentStaggers} / {maxStaggers}" +
                           $"\n[color=cyan]Weakness:[/color] {weaknessType}";
    }

    private void InitiateResourceBars()
    {
        ResourceBarControl = GetNodeOrNull<Control>("Enemy Resource Bar")
                             ?? GetNodeOrNull<Control>("Enemy Health Bar")
                             ?? FindChildControl(this, "Enemy Resource Bar")
                             ?? FindChildControl(this, "Enemy Health Bar");

        HealthBar = FindProgressBar(ResourceBarControl, "Health Bar")
                    ?? FindProgressBar(this, "Enemy Resource Bar/Health Bar")
                    ?? FindProgressBar(this, "Enemy Health Bar");

        TenacityBar = FindProgressBar(ResourceBarControl, "Tenacity Bar")
                      ?? FindProgressBar(this, "Enemy Resource Bar/Tenacity Bar");

        if (HealthBar == null && TenacityBar == null)
            return;

        UpdateResourceBars();
    }

    private void UpdateResourceBars()
    {
        if (HealthBar != null)
        {
            float healthMax = Mathf.Max(maxHealth, 1f);
            HealthBar.MaxValue = healthMax;
            HealthBar.Value = Mathf.Clamp(health, 0f, healthMax);
        }

        if (TenacityBar != null)
        {
            float tenacityMax = Mathf.Max(maxTenacity, 1f);
            TenacityBar.MaxValue = tenacityMax;
            float clampedTenacity = Mathf.Clamp(tenacity, 0f, tenacityMax);
            TenacityBar.Value = tenacityMax - clampedTenacity;
            UpdateTenacityBarFillStyle();
        }
    }

    private void UpdateTenacityBarFillStyle()
    {
        if (TenacityBar == null)
            return;

        bool canBeKnockbacked = TenacitySystem?.CanBeStaggered() ?? false;

        if (canBeKnockbacked)
        {
            EnsureTenacityBarStyles();
            if (TenacityBarKnockbackFill != null)
                TenacityBar.AddThemeStyleboxOverride("fill", TenacityBarKnockbackFill);
        }
        else
        {
            EnsureTenacityBarStyles();
            if (TenacityBarNormalFill != null)
                TenacityBar.AddThemeStyleboxOverride("fill", TenacityBarNormalFill);
        }
    }

    private void EnsureTenacityBarStyles()
    {
        if (TenacityBarNormalFill != null && TenacityBarKnockbackFill != null)
            return;

        StyleBox originalFill = TenacityBar?.GetThemeStylebox("fill");
        StyleBoxFlat originalFlat = originalFill as StyleBoxFlat;

        TenacityBarNormalFill = originalFlat != null
            ? originalFlat.Duplicate() as StyleBox
            : new StyleBoxFlat();

        TenacityBarKnockbackFill = originalFlat != null
            ? originalFlat.Duplicate() as StyleBox
            : new StyleBoxFlat();

        if (TenacityBarKnockbackFill is StyleBoxFlat knockbackFlat)
            knockbackFlat.BgColor = Colors.White;
    }

    private Control FindChildControl(Node node, string nodeName)
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

    private ProgressBar FindProgressBar(Node node, string nodePath)
    {
        if (node == null)
            return null;

        ProgressBar direct = node.GetNodeOrNull<ProgressBar>(nodePath);
        if (direct != null)
            return direct;

        foreach (Node child in node.GetChildren())
        {
            if (child is ProgressBar bar && bar.Name == nodePath)
                return bar;

            ProgressBar nested = FindProgressBar(child, nodePath);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
