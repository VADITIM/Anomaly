using Godot;

public partial class Player
{

    public void OnActionPerformed() { staminaRegenerationTimer = staminaRegenCooldown; }
    private void OnAttackStarted(bool isHeavy) { }
    private void OnAttackEnded() { }
    private void OnDodgeStarted(Vector2 direction) { }
    private void OnPlayerDied() { }
    
    public static bool canMove
    {
        get => Player.Instance?.StateMachine?.CanMove ?? true;
        set { if (Player.Instance?.StateMachine != null) Player.Instance.StateMachine.CanMove = value; }
    }
    public static bool canAttack
    {
        get => Player.Instance?.StateMachine?.CanAttack ?? true;
        set { if (Player.Instance?.StateMachine != null) Player.Instance.StateMachine.CanAttack = value; }
    }
    public static bool isPaused
    {
        get => Player.Instance?.StateMachine?.IsPaused ?? false;
        set { if (Player.Instance?.StateMachine != null) Player.Instance.StateMachine.IsPaused = value; }
    }

    protected override State GetCurrentAnimationState() { return StateMachine?.CurrentState ?? State.Idle; }

    protected override string GetCurrentAnimationDirection(bool useDirectionalAnimations, out bool flipH)
    {
        State currentState = StateMachine?.CurrentState ?? State.Idle;

        if (currentState == PlayerState.Dodging)
        {
            Vector2 dodgeVel = Dodge.GetDodgeVelocity();
            return GetDirectionFromVector(dodgeVel, out flipH);
        }

        if (currentState == PlayerState.Staggered || currentState == PlayerState.Knockback || currentState == PlayerState.Dead)
        {
            flipH = false;
            return _lastDamageDirection;
        }

        if (StateMachine != null && StateMachine.IsAirborne)
        {
            flipH = false;
            return "Down";
        }

        if (currentState == PlayerState.Attacking || currentState == PlayerState.HeavyAttacking)
        {
            flipH = _lastFlipH;
            return lastAnimationDirection;
        }

        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        return GetDirectionFromVector(toMouse, out flipH);
    }

    protected override void OnAnimationPlayed(string animationName, State state, string direction, bool flipH)
    {
        lastAnimationDirection = direction;

        if (animationName.StartsWith("Attack") ||
            animationName.StartsWith("attack") ||
            animationName == "Attack_Spin")
        {
            float desiredDuration = Weapon.GetAttackAnimationDuration(direction, state == PlayerState.HeavyAttacking);
            float nativeLength = Mathf.Max(0.1f, (float)AnimationPlayer.GetAnimation(animationName).Length);

            AnimationPlayer.SpeedScale = nativeLength / Mathf.Max(desiredDuration, 0.0001f);
        }

        Weapon.PlayStateAnimation(animationName);

        int playerZ = Sprite.ZIndex;
        bool weaponAbove = direction == "E" || direction == "NE" || direction == "N" || direction == "NW";
        Weapon.SetLayerRelativeToPlayer(playerZ, weaponAbove);
    }






    
    private void PlayWeaponAttackAnimation(bool isHeavy)
    {
        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        string direction = GetDirectionFromVector(toMouse, out _);

        Weapon.PlayAttackAnimation(direction, isHeavy);
    }

    public float GetCurrentAttackAnimationDuration(bool isHeavy)
    {
        if (AnimationPlayer == null)
            return 0.5f;

        Vector2 mousePos = GetGlobalMousePosition();
        Vector2 toMouse = mousePos - GlobalPosition;
        string direction = GetDirectionFromVector(toMouse, out _);

        return Weapon.GetAttackAnimationDuration(direction, isHeavy);
    }

    public override float GetAttackDuration()
    {
        return GetCurrentAttackAnimationDuration(false);
    }


}