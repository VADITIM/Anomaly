using Godot;
using System.Collections.Generic;

public abstract partial class Enemy : CharacterBody2D
{
    private static readonly List<Enemy> ActiveEnemies = new();
    private const float CAMERA_FOCUS_RANGE = 800f;

    public Player Player { get; private set; }
    private Weapon Weapon => Player?.Weapon;
    private EnemyStateMachine StateMachine;
    public TenacitySystem TenacitySystem;
    
    private RichTextLabel testDisplay;
    private ProgressBar _healthBar;
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

    [Export] public float DefaultStaggerDuration { get; set; } = 1.2f;
    [Export] public float DefaultRecoveryDuration { get; set; } = 2.5f;
    [Export] public float DefaultKnockbackDuration { get; set; } = 0.3f;
    [Export] public float ChaseRange { get; set; } = 500f;
    [Export] public float AttackRange { get; set; } = 50f;
    [Export] public float StopDistance { get; set; } = 100f;

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
        if (!ActiveEnemies.Contains(this))
        {
            ActiveEnemies.Add(this);
        }

        InitializeEnemy();
        InitiateHealthBar();
    }

    public override void _ExitTree()
    {
        ActiveEnemies.Remove(this);
    }

    public void InitializeEnemy()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
        testDisplay = GetNode<RichTextLabel>("Label");
        testDisplay.BbcodeEnabled = true;
        TenacityCooldownCue = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        TenacityCooldownCue.Visible = false;
        
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
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;
        
        UpdateHitTimer((float)delta);
        TenacitySystem.Process((float)delta);
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


    private void OnStateChangedHandler(EnemyState oldState, EnemyState newState) {  OnStateChanged(oldState, newState); }
    protected virtual void OnStateChanged(EnemyState oldState, EnemyState newState) { }

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
        _healthBar.Value = health;
        
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
        
        string stateInfo = StateMachine != null ? StateMachine.CurrentState.ToString() : "Unknown";
        
        testDisplay.Text = $"[color=yellow]State:[/color] {stateInfo} \n" +
                           $"\n[color=red]Health:[/color] {health:F0} / {maxHealth:F0} [color=gray]Armor:[/color] {armor}" +
                           $"\n[color=green]Tenacity:[/color] {tenacity:F1} / 10 [color=orange]Staggers:[/color] {CurrentStaggers} / {maxStaggers}" +
                           $"\n[color=cyan]Weakness:[/color] {weaknessType}";
    }

    private void InitiateHealthBar()
    {
        _healthBar = GetNode<ProgressBar>("Health Bar");
        _healthBar.MaxValue = health;
        _healthBar.Value = health;
    }
}
