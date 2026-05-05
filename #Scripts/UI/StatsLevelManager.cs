using Godot;
using System;

public partial class StatsLevelManager : Control
{
    public Player Player;
    [Export] public Control Stat; 
    [Export] public Label StatNameLabel;
    public Label StatCurrentLevelLabel;
    public Label StatAddedLevelsLabel;
}  
