using Godot;

public class PlayerInputBehavior : IEntityBehavior
{
    private Entity owner;
    private MovementBehavior movementBehavior;

    // NOTE: opposing keys held together must not cancel out — the most recently
    // pressed key on each axis wins, so W+S / A+D behave like a re-tap.
    private MovementBehavior.MovementDirection _lastVertical;
    private MovementBehavior.MovementDirection _lastHorizontal;

    public bool DodgeJustPressed { get; private set; }
    public bool AttackJustPressed { get; private set; }
    public bool HealJustPressed { get; private set; }
    public bool HeavyPressed { get; private set; }
    public bool HeavyJustReleased { get; private set; }

    public void OnReady(Entity owner)
    {
        this.owner = owner;
        movementBehavior = owner?.GetBehavior<MovementBehavior>();
    }

    public void OnProcess(double delta)
    {
        DodgeJustPressed = Input.IsActionJustPressed(Keybinds.Dodge);
        AttackJustPressed = Input.IsActionJustPressed(Keybinds.Attack);
        HealJustPressed = Input.IsActionJustPressed(Keybinds.Heal);
        HeavyPressed = Input.IsActionPressed(Keybinds.Heavy);
        HeavyJustReleased = Input.IsActionJustReleased(Keybinds.Heavy);

        if (movementBehavior == null)
            movementBehavior = owner?.GetBehavior<MovementBehavior>();

        if (movementBehavior == null)
            return;

        if (Input.IsActionJustPressed(Keybinds.MoveUp)) _lastVertical = MovementBehavior.MovementDirection.Up;
        if (Input.IsActionJustPressed(Keybinds.MoveDown)) _lastVertical = MovementBehavior.MovementDirection.Down;
        if (Input.IsActionJustPressed(Keybinds.MoveLeft)) _lastHorizontal = MovementBehavior.MovementDirection.Left;
        if (Input.IsActionJustPressed(Keybinds.MoveRight)) _lastHorizontal = MovementBehavior.MovementDirection.Right;

        MovementBehavior.MovementDirection newDirection =
            ResolveAxis(Keybinds.MoveUp, MovementBehavior.MovementDirection.Up,
                        Keybinds.MoveDown, MovementBehavior.MovementDirection.Down, _lastVertical)
            | ResolveAxis(Keybinds.MoveLeft, MovementBehavior.MovementDirection.Left,
                          Keybinds.MoveRight, MovementBehavior.MovementDirection.Right, _lastHorizontal);

        movementBehavior.CurrentDirection = newDirection;
    }

    private static MovementBehavior.MovementDirection ResolveAxis(
        string negativeAction, MovementBehavior.MovementDirection negative,
        string positiveAction, MovementBehavior.MovementDirection positive,
        MovementBehavior.MovementDirection lastPressed)
    {
        bool negativeHeld = Input.IsActionPressed(negativeAction);
        bool positiveHeld = Input.IsActionPressed(positiveAction);

        if (negativeHeld && positiveHeld)
            return lastPressed;
        if (negativeHeld)
            return negative;
        if (positiveHeld)
            return positive;

        return MovementBehavior.MovementDirection.None;
    }

    public void OnPhysicsProcess(double delta)
    {
    }

    public void OnExitTree()
    {
    }
}
