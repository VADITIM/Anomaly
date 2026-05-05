using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class UpgradeBackground : Panel
{
    Player Player;
    PlayerStats playerStats;
    public string[] statName;
    public string[] hiddenStatName;
    
    private Dictionary<string, Label> statLabels;
    private Dictionary<string, (Button increaseBtn, Button decreaseBtn)> statButtons;
    
    [Export] public HBoxContainer StatContainer; // Assign the HBoxContainer in the inspector

    public override void _Ready()
    {
        Player ??= GetTree().Root.FindChild("Player", true, false) as Player;
        playerStats = new PlayerStats();

        statName = new string[]
        {
            "Corruption",
            "Vessel",

            "Health",

            "Stamina",
            "Stamina Regen",

            "Speed",
            "Armor",
            "Tenacity",
            "Soul",
        };

        hiddenStatName = new string[]
        {
            "Lacerate Multiplier",
            "Puncture Multiplier",
            "Crush Multiplier",
        };
        
        // Instantiate stat labels in the VBoxContainer
        PopulateStatLabels();
    }
    
    private void PopulateStatLabels()
    {
        statLabels = new Dictionary<string, Label>();
        statButtons = new Dictionary<string, (Button, Button)>();
        
        // Clear existing children from StatContainer
        foreach (Node child in StatContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Create left VBoxContainer for stat labels
        VBoxContainer statsVBox = new VBoxContainer();
        statsVBox.Name = "StatsVBox";
        StatContainer.AddChild(statsVBox);
        
        // Create right VBoxContainer for buttons
        VBoxContainer buttonsVBox = new VBoxContainer();
        buttonsVBox.Name = "ButtonsVBox";
        StatContainer.AddChild(buttonsVBox);
        
        // Create labels and buttons for normal stats
        foreach (var stat in statName)
        {
            // Create label
            Label label = new Label();
            label.Name = stat.Replace(" ", "");
            label.AddThemeFontSizeOverride("font_size", 80);
            statsVBox.AddChild(label);
            statLabels[stat] = label;
            
            // Create HBoxContainer for increase/decrease buttons
            HBoxContainer buttonRow = new HBoxContainer();
            buttonRow.Name = stat.Replace(" ", "") + "Buttons";
            
            // Decrease button
            Button decreaseBtn = new Button();
            decreaseBtn.Name = stat.Replace(" ", "") + "DecreaseBtn";
            decreaseBtn.Text = "-";
            decreaseBtn.AddThemeFontSizeOverride("font_size", 60);
            decreaseBtn.CustomMinimumSize = new Vector2(80, 80);
            decreaseBtn.Pressed += () => OnDecreasePressed(stat);
            buttonRow.AddChild(decreaseBtn);
            
            // Increase button
            Button increaseBtn = new Button();
            increaseBtn.Name = stat.Replace(" ", "") + "IncreaseBtn";
            increaseBtn.Text = "+";
            increaseBtn.AddThemeFontSizeOverride("font_size", 60);
            increaseBtn.CustomMinimumSize = new Vector2(80, 80);
            increaseBtn.Pressed += () => OnIncreasePressed(stat);
            buttonRow.AddChild(increaseBtn);
            
            buttonsVBox.AddChild(buttonRow);
            statButtons[stat] = (increaseBtn, decreaseBtn);
        }
    }
    
    private void OnIncreasePressed(string statName)
    {
        if (playerStats.CanIncrease(statName))
        {
            playerStats.IncreaseStat(statName);
            GD.Print($"Increased {statName} to {playerStats.GetCurrentMax(statName)}");
        }
        else
        {
            GD.Print($"Cannot increase {statName} - already at maximum");
        }
    }
    
    private void OnDecreasePressed(string statName)
    {
        if (playerStats.CanDecrease(statName))
        {
            playerStats.DecreaseStat(statName);
            GD.Print($"Decreased {statName} to {playerStats.GetCurrentMax(statName)}");
        }
        else
        {
            GD.Print($"Cannot decrease {statName} - no levels added yet");
        }
    }
    
    public override void _Process(double delta)
    {
        // Update normal stats
        foreach (var stat in statName)
        {
            if (statLabels.TryGetValue(stat, out Label label))
            {
                float currentMax = playerStats.GetCurrentMax(stat);
                float totalMax = playerStats.GetTotalMax(stat);
                label.Text = $"{stat}: {(int)currentMax}/{(int)totalMax}";
                
                // Update button states
                if (statButtons.TryGetValue(stat, out var buttons))
                {
                    buttons.increaseBtn.Disabled = !playerStats.CanIncrease(stat);
                    buttons.decreaseBtn.Disabled = !playerStats.CanDecrease(stat);
                }
            }
        }
        
        // Update hidden stats
        foreach (var stat in hiddenStatName)
        {
            if (statLabels.TryGetValue(stat, out Label label))
            {
                float currentMax = playerStats.GetCurrentMax(stat);
                float totalMax = playerStats.GetTotalMax(stat);
                label.Text = $"{stat}: {(int)currentMax}/{(int)totalMax}";
                
                // Update button states
                if (statButtons.TryGetValue(stat, out var buttons))
                {
                    buttons.increaseBtn.Disabled = !playerStats.CanIncrease(stat);
                    buttons.decreaseBtn.Disabled = !playerStats.CanDecrease(stat);
                }
            }
        }
    }
}
