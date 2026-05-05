using Godot;
using System;

public partial class Gui : Control
{
    private const float TOTAL_MAX_HEALTH = 500f;
    private const float TOTAL_MAX_STAMINA = 500f;
    private const float TOTAL_MAX_CORRUPTION = 100f;
    public static float dodgeAnimationSpeed;

    private AnimatedSprite2D _animatedSprite;
    private Player Player;
    private PlayerState _lastState = (PlayerState)(-1); 
    private string _lastDirection = "";

    private enum FacingDirection
    {
        Up,
        UpRight,
        DownRight,
        Down,
        DownLeft,
        UpLeft
    }

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
        if (Player != null)
        {
            _animatedSprite = Player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        }

        // Compute dodge duration as frameCount / fps with safe fallback
        if (_animatedSprite?.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation("DodgeDown"))
        {
            var frames = _animatedSprite.SpriteFrames.GetFrameCount("DodgeDown");
            var fps = _animatedSprite.SpriteFrames.GetAnimationSpeed("DodgeDown");
            if (fps > 0 && frames > 0)
                dodgeAnimationSpeed = frames / (float)fps;
            else
                dodgeAnimationSpeed = 0.25f; // fallback
        }
        else
        {
            dodgeAnimationSpeed = 0.25f; // fallback until animations are ready
        }
    }

    public override void _Process(double delta)
    {
        if (Player == null)
        {
            Player = GetTree().Root.FindChild("Player", true, false) as Player;
            _animatedSprite = Player.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        }

        var currentState = Player.StateMachine.CurrentState;
        
        string direction;
        bool flipH;
        
        if (currentState == PlayerState.Dodging)
        {
            Vector2 dodgeDir = Dodge.GetDodgeVelocity().Normalized();
            if (dodgeDir == Vector2.Zero)
            {
                dodgeDir = Movement.GetMovementVector();
            }
            (direction, flipH) = GetDirectionFromVector(dodgeDir);
        }
        else
        {
            Vector2 mousePos = Player.GetGlobalMousePosition();
            Vector2 playerPos = Player.GlobalPosition;
            Vector2 dirVector = (mousePos - playerPos).Normalized();
            (direction, flipH) = GetDirectionFromVector(dirVector);
        }

        if (currentState != _lastState || direction != _lastDirection)
        {
            PlayAnimationForState(currentState, direction, flipH);
            _lastState = currentState;
            _lastDirection = direction;
        }
        
        _animatedSprite.FlipH = flipH;
    }

    private (string direction, bool flipH) GetDirectionFromVector(Vector2 dirVector)
    {
        if (dirVector == Vector2.Zero)
            return ("Down", false);

        float angle = Mathf.RadToDeg(dirVector.Angle());

        // Normalize
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;

        bool flipH = false;
        if (angle > 90 || angle < -90)
        {
            flipH = true;
            angle = angle > 0 ? 180 - angle : -180 - angle; 
        }

        string direction;
        if (angle <= -60)
            direction = "Up";
        else if (angle < -15)
            direction = "UpRight";
        else if (angle <= 15)
            direction = angle < 0 ? "UpRight" : "DownRight"; 
        else if (angle < 60)
            direction = "DownRight";
        else
            direction = "Down";

        return (direction, flipH);
    }

    private void PlayAnimationForState(PlayerState state, string direction, bool flipH)
    {
        string animationName = state switch
        {
            PlayerState.Idle => $"Idle{direction}",
            PlayerState.Moving => $"Move{direction}",
            PlayerState.Dodging => $"Dodge{direction}",
            PlayerState.Attacking => $"Idle{direction}", 
            PlayerState.HeavyCharging => $"Idle{direction}",
            PlayerState.HeavyAttacking => $"Idle{direction}",
            PlayerState.Healing => $"Idle{direction}",
            PlayerState.Staggered => $"Idle{direction}",
            PlayerState.Knockback => $"Idle{direction}",
            PlayerState.Dead => $"IdleDown",
            _ => $"Idle{direction}"
        };

        _animatedSprite.FlipH = flipH;
        
        if (_animatedSprite.SpriteFrames.HasAnimation(animationName))
            _animatedSprite.Play(animationName);
        else
            _animatedSprite.Play("IdleDown");
    }
}
