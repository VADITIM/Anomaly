using Godot;
using System;

/// <summary>
/// UI component that displays the current player state.
/// </summary>
public partial class StateDisplay : Label
{
    private Label _stateLabel;

    public override void _Ready()
    {
        _stateLabel = this;
    }

    public override void _Process(double delta)
    {
        if (PlayerStateMachine.Instance != null)
        {
            var currentState = PlayerStateMachine.Instance.CurrentState;
            var moveDir = Movement.CurrentMovementDirection;
            
            string stateText = currentState.ToString();
            if (currentState == PlayerState.Moving)
            {
                stateText += $" ({moveDir})";
            }
            else if (currentState == PlayerState.HeavyCharging)
            {
                float charge = PlayerStateMachine.Instance.HeavyChargeProgress;
                stateText += $" ({charge:P0})";
            }
            
            _stateLabel.Text = stateText;
        }
        else
        {
            _stateLabel.Text = "No State Machine (Instance is null)";
            GD.PrintErr("StateDisplay: PlayerStateMachine.Instance is null!");
        }
    }
}


