using Godot;
using System;

public partial class StateDisplay : Label
{
    private Label _stateLabel;
    private Player _player;

    public override void _Ready()
    {
        _stateLabel = this;
        _player = GetTree().Root.FindChild("Player", true, false) as Player;
    }

    public override void _Process(double delta)
    {
        if (_player?.StateMachine != null)
        {
            var currentState = _player.StateMachine.CurrentState;
            var movementBehavior = _player.GetBehavior<MovementBehavior>();
            var moveDir = movementBehavior?.CurrentDirection ?? MovementBehavior.MovementDirection.None;

            string stateText = currentState.ToString();
            if (currentState == PlayerState.Moving)
            {
                stateText += $" ({moveDir})";
            }
            else if (currentState == PlayerState.HeavyCharging)
            {
                float charge = _player.StateMachine.HeavyChargeProgress;
                stateText += $" ({charge:P0})";
            }

            _stateLabel.Text = stateText;
        }
    }
}


