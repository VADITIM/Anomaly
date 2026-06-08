using Godot;

public class PlayerInputBehavior : IEntityBehavior
{
    private Entity owner;
    private MovementBehavior movementBehavior;

    public void OnReady(Entity owner)
    {
        this.owner = owner;
        movementBehavior = owner?.GetBehavior<MovementBehavior>();
    }

    public void OnProcess(double delta)
    {
        if (movementBehavior == null)
            movementBehavior = owner?.GetBehavior<MovementBehavior>();

        if (movementBehavior == null)
            return;

        MovementBehavior.MovementDirection newDirection = MovementBehavior.MovementDirection.None;

        if (Input.IsActionPressed(Keybinds.MoveUp)) newDirection |= MovementBehavior.MovementDirection.Up;
        if (Input.IsActionPressed(Keybinds.MoveDown)) newDirection |= MovementBehavior.MovementDirection.Down;
        if (Input.IsActionPressed(Keybinds.MoveLeft)) newDirection |= MovementBehavior.MovementDirection.Left;
        if (Input.IsActionPressed(Keybinds.MoveRight)) newDirection |= MovementBehavior.MovementDirection.Right;

        movementBehavior.CurrentDirection = newDirection;
    }

    public void OnPhysicsProcess(double delta)
    {
    }

    public void OnExitTree()
    {
    }
}
