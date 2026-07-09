using Godot;
using System.Collections.Generic;

public enum EnemyDamageType   { Normal, Corrupted }
public enum EnemyWeaknessType { Piercing, Slashing, Smashing }
public enum EnemyDifficultyScaling { Regular, Corrupted }

public abstract partial class Enemy : Entity
{
    private static readonly List<Enemy> ActiveEnemies = new();
    private static bool _showDebugLabels = false;

    public Player Player { get; private set; }
    public new EnemyStateMachine StateMachine => (EnemyStateMachine)base.StateMachine;

    protected override StateMachine CreateStateMachine() => new EnemyStateMachine();

    [Export] public float VesselReward { get; set; } = 10f;
    [Export] public float SoulReward   { get; set; } = 50f;

    [Export] public EnemyDamageType   DamageType   { get; set; }
    [Export] public EnemyWeaknessType WeaknessType { get; set; }

    [Export] public float ChaseRange    { get; set; } = 200f;
    [Export] public float AttackRange   { get; set; } = 50f;

    // Sampled once per lifetime from the world DifficultyLevel — runtime state, never saved.
    public int EnemyLevel { get; private set; } = 1;

    private float _hitTimer = 0f;
    private const float HitWindow = 1.5f;

    private bool _hasBeenHit = false;

    private void UpdateHitTimer(float delta) { if (_hitTimer > 0) _hitTimer -= delta; }
    private bool IsWithinCameraFocusRange() { if (Player == null) return false; return GlobalPosition.DistanceTo(Player.GlobalPosition) <= CameraFocus.CAMERA_FOCUS_RANGE; }
    public void MarkCameraFocus() { _hasBeenHit = true; }

    public bool IsWeakTo(WeaponArc.WeaponAttackType attackType)
    {
        return attackType switch
        {
            WeaponArc.WeaponAttackType.Slashing => WeaknessType == EnemyWeaknessType.Slashing,
            WeaponArc.WeaponAttackType.Piercing => WeaknessType == EnemyWeaknessType.Piercing,
            WeaponArc.WeaponAttackType.Smashing => WeaknessType == EnemyWeaknessType.Smashing,
            _ => false
        };
    }

    public void InitializeEnemy()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;

        SaveManager.EnsureLoaded();
        EnemyLevel = DifficultyScalingSystem.SampleEnemyLevel(SaveManager.Difficulty.DifficultyLevel);
        float statMultiplier = DifficultyScalingSystem.GetStatMultiplier(EnemyLevel);
        Health *= statMultiplier;
        Damage *= statMultiplier;

        var movementBehavior = new MovementBehavior
        {
            GetBaseSpeed = () => Speed
        };
        AddBehavior(movementBehavior);

        StatsDisplay();
        InitializeTenacity();
        AddBehavior(new CommonDamageFlash());
        InitializeEnemyResourceBars();
        InitializeStateMachine();

        UpdateAnimation();
    }

    private void InitializeEnemyResourceBars()
    {
        SetMaxHealth(Health);
        SetHealth(Health);
        AddBehavior(new EnemyResourceBarBehavior());
    }

    public static void ToggleDebugLabels()
    {
        _showDebugLabels = !_showDebugLabels;

        foreach (Enemy enemy in ActiveEnemies)
        {
            if (enemy == null || !GodotObject.IsInstanceValid(enemy))
                continue;

            enemy.UpdateDebugLabelVisibility();
        }
    }

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
        base._ExitTree();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (IsDead)
            return;

        UpdateHitTimer((float)delta);
        TenacitySystem.Process((float)delta);
        DisplayStats();
    }

    public static Enemy GetBestCameraTarget(Vector2 cursorPosition)
    {
        Enemy bestEnemy = null;
        float bestScore = float.MaxValue;

        foreach (Enemy enemy in ActiveEnemies)
        {
            if (enemy == null || !GodotObject.IsInstanceValid(enemy) || enemy.IsDead || !enemy.HasCameraFocus)
                continue;

            float cursorDistance = cursorPosition.DistanceTo(enemy.GlobalPosition);
            float healthRatio = enemy.MaxHealth > 0f ? enemy.Health / enemy.MaxHealth : 1f;
            float score = cursorDistance + (healthRatio * 150f) - (enemy.CameraPriority * 200f);

            if (score < bestScore)
            {
                bestScore = score;
                bestEnemy = enemy;
            }
        }

        return bestEnemy;
    }

    protected virtual void GrantDeathRewards()
    {
        if (Player == null)
            return;

        Player.ResourceManager?.AddCorruption(2f);

        float currentVessel = Player.Stats.GetCurrent(StatType.Vessel);
        float maxVessel = Player.Stats.GetCurrentMax(StatType.Vessel);
        Player.Stats.SetCurrent(StatType.Vessel, Mathf.Min(currentVessel + VesselReward, maxVessel));

        float currentSoul = Player.Stats.GetCurrent(StatType.Soul);
        float maxSoul = Player.Stats.GetCurrentMax(StatType.Soul);
        Player.Stats.SetCurrent(StatType.Soul, Mathf.Min(currentSoul + SoulReward, maxSoul));
    }

    private void DisplayStats()
    {
        if (_testDisplay == null) return;

        UpdateDebugLabelVisibility();

        if (!_testDisplay.Visible)
            return;

        string stateInfo = StateMachine != null ? StateMachine.CurrentState.ToString() : "Unknown";

        _testDisplay.Text = $"[color=yellow]State:[/color] {stateInfo} \n" +
                           $"\n[color=red]Health:[/color] {GetHealth():F0} / {GetMaxHealth():F0} [color=gray]Armor:[/color] {Armor}" +
                           $"\n[color=green]Tenacity:[/color] {Tenacity:F1} / {MaxTenacity:F1} [color=orange]Staggers:[/color] {CurrentStaggers} / {MaxStaggers}" +
                           $"\n[color=cyan]Weakness:[/color] {WeaknessType} [color=purple]Level:[/color] {EnemyLevel}";
    }

    private void UpdateDebugLabelVisibility()
    {
        if (_testDisplay == null)
            return;

        _testDisplay.Visible = _showDebugLabels;
    }
}
