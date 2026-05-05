using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerUpgradePanel : Control
{
    [Export] public Player Player;
    [Export] public Control UpgradeContainer;
    [Export] public PackedScene StatUpgradePanelScene;

    private List<PlayerStat> PlayerStats = new List<PlayerStat>();

    public override void _Ready()
    {
        Player ??= GetTree().Root.FindChild("Player", true, false) as Player;
        
        InitializeStats();
        PopulateStatsPanel();
    }

    private PlayerStats ResolvePlayerStats()
    {
        Player ??= GetTree().Root.FindChild("Player", true, false) as Player;
        return Player?.Stats;
    }

    public override void _Process(double delta)
    {
        if (IsStatChanged())
        {
            // Stats are now managed by PlayerStats, which automatically handles clamping
        }
    }

    public bool IsStatChanged()
    {
        foreach (var stat in PlayerStats)
        {
            float currentValue = stat.GetCurrentValue();
            float totalMaxValue = GetTotalMaxValue(stat.Type);
            if (currentValue > totalMaxValue)
            {
                return true;
            }
        }
        return false;
    }

    private void InitializeStats()
    {
        PlayerStats.Clear();

        Player player = Player ?? GetTree().Root.FindChild("Player", true, false) as Player;
        PlayerStats playerStats = player?.Stats;

        if (player == null || playerStats == null)
        {
            GD.PushWarning("PlayerUpgradePanel could not resolve the Player stats.");
            return;
        }

        PlayerStats.Add(new PlayerStat(
            "Endurance",
            new List<(PlayerStatType, Func<float>, Action<float>, float)>
            {
                (PlayerStatType.MaxHealth, () => playerStats.GetCurrentMax("Health"), (val) => playerStats.SetCurrentMax("Health", val), 5f),
                (PlayerStatType.Vessel, () => playerStats.GetCurrentMax("Vessel"), (val) => playerStats.SetCurrentMax("Vessel", val), 5f),
            }
        ));

        PlayerStats.Add(new PlayerStat(
            "Toughness",
            new List<(PlayerStatType, Func<float>, Action<float>, float)>
            {
                (PlayerStatType.Tenacity, () => playerStats.GetCurrentMax("Tenacity"), (val) => playerStats.SetCurrentMax("Tenacity", val), .12f),
                (PlayerStatType.Armor, () => playerStats.GetCurrentMax("Armor"), (val) => playerStats.SetCurrentMax("Armor", val), 2f)
            }
        ));

        PlayerStats.Add(new PlayerStat(
            "Dexterity",
            new List<(PlayerStatType, Func<float>, Action<float>, float)>
            {
                (PlayerStatType.MaxStamina, () => playerStats.GetCurrentMax("Stamina"), (val) => playerStats.SetCurrentMax("Stamina", val), 5f),
                (PlayerStatType.StaminaRegen, () => playerStats.GetCurrentMax("Stamina Regen"), (val) => playerStats.SetCurrentMax("Stamina Regen", val),.6f),
                (PlayerStatType.StaminaRegenCooldown, () => player.STAMINA_REGEN_COOLDOWN, (val) => player.STAMINA_REGEN_COOLDOWN = val, -.007f),
                (PlayerStatType.Speed, () => playerStats.GetCurrentMax("Speed"), (val) => playerStats.SetCurrentMax("Speed", val), .5f)
            }
        ));

        PlayerStats.Add(new PlayerStat("Corruption", PlayerStatType.Corruption, () => playerStats.GetCurrentMax("Corruption"), (val) => playerStats.SetCurrentMax("Corruption", val), 2f));
        PlayerStats.Add(new PlayerStat("Lacerate", PlayerStatType.Lacerate, () => playerStats.GetCurrentMax("Lacerate Multiplier"), (val) => playerStats.SetCurrentMax("Lacerate Multiplier", val), 2f));
        PlayerStats.Add(new PlayerStat("Puncture", PlayerStatType.Puncture, () => playerStats.GetCurrentMax("Puncture Multiplier"), (val) => playerStats.SetCurrentMax("Puncture Multiplier", val), 2f));
        PlayerStats.Add(new PlayerStat("Crush", PlayerStatType.Crush, () => playerStats.GetCurrentMax("Crush Multiplier"), (val) => playerStats.SetCurrentMax("Crush Multiplier", val), 2f));

    }

    private void PopulateStatsPanel()
    {
        foreach (Node child in UpgradeContainer.GetChildren())
        {
            if (child.Name != "Stat")
                child.QueueFree();
        }

        foreach (var stat in PlayerStats)
        {
            Control statPanel = StatUpgradePanelScene.Instantiate<Control>();

            if (statPanel == null)
            {
                continue;
            }

            Label statNameLabel = statPanel.GetNode<Label>("Stat Upgrade Description/Panel/Stat Name");
            Label currentLevelLabel = statPanel.GetNode<Label>("Stat Upgrade Description/Panel/Current Stat Level");
            Label nextLevelLabel = statPanel.GetNode<Label>("Stat Upgrade Description/Panel/Next Stat Level");
            
            Button levelDownBtn = statPanel.GetNode<Button>("Level Buttons/Level Down Button");
            Button levelUpBtn = statPanel.GetNode<Button>("Level Buttons/Level Up Button");

            if (levelUpBtn == null || levelDownBtn == null || statNameLabel == null || currentLevelLabel == null || nextLevelLabel == null)
            {
                statPanel.QueueFree();
                continue;
            }

            statNameLabel.Text = stat.Name;
            UpdateStatLabels(stat, currentLevelLabel, nextLevelLabel);

            levelUpBtn.Pressed += () => {
                stat.LevelUp();
                UpdateStatLabels(stat, currentLevelLabel, nextLevelLabel);
            };

            levelDownBtn.Pressed += () => {
                stat.LevelDown();
                UpdateStatLabels(stat, currentLevelLabel, nextLevelLabel);
            };

            // Ensure proper positioning if StatsContainer is not a layout container
            if (UpgradeContainer is Control)
            {
                float gap = 100f; // Set the gap between items
                statPanel.Position = new Vector2(0, UpgradeContainer.GetChildCount() * (statPanel.Size.Y + gap));
            }

            UpgradeContainer.AddChild(statPanel);
        }
    }

    private void UpdateStatLabels(PlayerStat stat, Label currentLabel, Label nextLabel)
    {
        float current = stat.GetCurrentValue();
        float next = Math.Min(stat.GetNextLevelValue(), GetTotalMaxValue(stat.Type));

        currentLabel.Text = $"{current:F0}";
        nextLabel.Text = $"+{Math.Max(0, next - current):F0}";

        // Refresh the stats in the Player instance
        ResetPlayerStats();
    }

    private void ResetPlayerStats()
    {
        PlayerStats playerStats = ResolvePlayerStats();

        if (playerStats == null)
        {
            return;
        }

        playerStats.SetCurrentMax("Health", Math.Min(playerStats.GetCurrentMax("Health"), playerStats.GetTotalMax("Health")));
        playerStats.SetCurrentMax("Stamina", Math.Min(playerStats.GetCurrentMax("Stamina"), playerStats.GetTotalMax("Stamina")));
        playerStats.SetCurrentMax("Armor", Math.Min(playerStats.GetCurrentMax("Armor"), playerStats.GetTotalMax("Health")));
        playerStats.SetCurrentMax("Stamina Regen", Math.Min(playerStats.GetCurrentMax("Stamina Regen"), playerStats.GetTotalMax("Stamina")));
        playerStats.SetCurrentMax("Speed", Math.Min(playerStats.GetCurrentMax("Speed"), 100));
    }

    private float GetTotalMaxValue(PlayerStatType type)
    {
        PlayerStats playerStats = ResolvePlayerStats();

        if (playerStats == null)
        {
            return float.MaxValue;
        }

        return type switch
        {
            PlayerStatType.MaxHealth => playerStats.GetTotalMax("Health"),
            PlayerStatType.MaxStamina => playerStats.GetTotalMax("Stamina"),
            PlayerStatType.Armor => playerStats.GetTotalMax("Health"), // Adjust if armor has a different max
            PlayerStatType.StaminaRegen => playerStats.GetTotalMax("Stamina"), // Adjust if needed
            PlayerStatType.Speed => 100, // Example max value for speed
            _ => float.MaxValue
        };
    }
}

public enum PlayerStatType
{
    MaxHealth,
    Vessel,
    MaxStamina,
    Speed,
    Armor,
    Tenacity,
    StaminaRegenCooldown,
    StaminaRegen,
    Corruption,
    Lacerate,
    Puncture,
    Crush
}

public class PlayerStat
{
    public string Name { get; private set; }
    public PlayerStatType Type { get; private set; }
    public int Level { get; private set; }
    public float IncrementPerLevel { get; private set; }

    private Func<float> _getter;
    private Action<float> _setter;

    // Constructor for single stat
    public PlayerStat(string name, PlayerStatType type, Func<float> getter, Action<float> setter, float incrementPerLevel)
    {
        Name = name;
        Type = type;
        Level = 0;
        IncrementPerLevel = incrementPerLevel;
        _getter = getter;
        _setter = setter;
    }

    // Constructor for multiple stats
    public PlayerStat(string name, List<(PlayerStatType type, Func<float> getter, Action<float> setter, float incrementPerLevel)> stats)
    {
        Name = name;
        Level = 0;
        IncrementPerLevel = 0;

        foreach (var stat in stats)
        {
            IncrementPerLevel += stat.incrementPerLevel;
            _getter += stat.getter;
            _setter += stat.setter;
        }
    }

    public void LevelUp()
    {
        Level++;
        _setter(_getter() + IncrementPerLevel);
    }

    public void LevelDown()
    {
        if (Level > 0)
        {
            Level--;
            _setter(_getter() - IncrementPerLevel);
        }
    }

    public float GetCurrentValue() => _getter();
    public float GetNextLevelValue() => _getter() + IncrementPerLevel;
}
