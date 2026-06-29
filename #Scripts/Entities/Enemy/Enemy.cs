using Godot;
using System.Collections.Generic;

public enum EnemyDamageType   { Normal, Corrupted }
public enum EnemyWeaknessType { Piercing, Slashing, Smashing }
public enum EnemyDifficultyScaling { Regular, Corrupted }

public abstract partial class Enemy : Entity
{
    private static readonly List<Enemy> ActiveEnemies = new();
    private static bool ShowDebugLabels = false;

    public Player Player { get; private set; }
    private WeaponArc WeaponArc => Player?.Weapon?.GetCurrentArc();

    [Export] public float VesselReward { get; set; } = 10f;
    [Export] public float SoulReward   { get; set; } = 50f;

    [Export] public EnemyDamageType   DamageType   { get; set; }
    [Export] public EnemyWeaknessType WeaknessType { get; set; }

    [Export] public float ChaseRange    { get; set; } = 200f;
    [Export] public float AttackRange   { get; set; } = 50f;
    [Export] public float StopDistance  { get; set; } = 20f;

    private float hitTimer = 0f;
    private const float HIT_WINDOW = 1.5f;

    private bool HasBeenHit = false;

    private void UpdateHitTimer(float delta) { if (hitTimer > 0) hitTimer -= delta; }
    private bool IsWithinCameraFocusRange() { if (Player == null) return false; return GlobalPosition.DistanceTo(Player.GlobalPosition) <= CameraFocus.CAMERA_FOCUS_RANGE; }
    public void MarkCameraFocus() { HasBeenHit = true; }

    public void InitializeEnemy()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;

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
        ShowDebugLabels = !ShowDebugLabels;

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

    protected virtual void OnDeath()
    {
        Player.Instance?.ResourceManager?.AddCorruption(2f);

        float currentVessel = Player.Instance.Stats.GetCurrent("Vessel");
        float maxVessel = Player.Instance.Stats.GetCurrentMax("Vessel");
        Player.Instance.Stats.SetCurrent("Vessel", Mathf.Min(currentVessel + VesselReward, maxVessel));

        float currentSoul = Player.Instance.Stats.GetCurrent("Soul");
        float maxSoul = Player.Instance.Stats.GetCurrentMax("Soul");
        Player.Instance.Stats.SetCurrent("Soul", Mathf.Min(currentSoul + SoulReward, maxSoul));
    }

    private void DisplayStats()
    {
        if (testDisplay == null) return;

        UpdateDebugLabelVisibility();

        if (!testDisplay.Visible)
            return;

        string stateInfo = StateMachine != null ? StateMachine.CurrentState.ToString() : "Unknown";

        testDisplay.Text = $"[color=yellow]State:[/color] {stateInfo} \n" +
                           $"\n[color=red]Health:[/color] {GetHealth():F0} / {GetMaxHealth():F0} [color=gray]Armor:[/color] {Armor}" +
                           $"\n[color=green]Tenacity:[/color] {Tenacity:F1} / 10 [color=orange]Staggers:[/color] {CurrentStaggers} / {MaxStaggers}" +
                           $"\n[color=cyan]Weakness:[/color] {WeaknessType}";
    }

    private void UpdateDebugLabelVisibility()
    {
        if (testDisplay == null)
            return;

        testDisplay.Visible = ShowDebugLabels;
    }
}
