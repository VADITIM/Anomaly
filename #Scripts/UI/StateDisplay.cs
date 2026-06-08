using Godot;
using System;

public partial class StateDisplay : Label
{
    private Label _stateLabel;

    public override void _Ready()
    {
        _stateLabel = this;
    }

    public override void _Process(double delta)
    {
        if (Player.Instance?.StateMachine != null)
        {
            var currentState = Player.Instance.StateMachine.CurrentState;
            var movementBehavior = Player.Instance.GetBehavior<MovementBehavior>();
            var moveDir = movementBehavior?.CurrentDirection ?? MovementBehavior.MovementDirection.None;
            
            string stateText = currentState.ToString();
            if (currentState == PlayerState.Moving)
            {
                stateText += $" ({moveDir})";
            }
            else if (currentState == PlayerState.HeavyCharging)
            {
                float charge = Player.Instance.StateMachine.HeavyChargeProgress;
                stateText += $" ({charge:P0})";
            }
            
            _stateLabel.Text = stateText;
        }
    }
}


