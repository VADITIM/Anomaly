using Godot;

public class KnockbackBehavior : IEntityBehavior
{
    private Entity _owner;
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private float _knockbackDuration = 0f;

    public void OnReady(Entity owner)
    {
        _owner = owner;
    }

    public void OnProcess(double delta)
    {
    }

    public void OnPhysicsProcess(double delta)
    {
        if (_owner == null || _knockbackDuration <= 0f)
            return;

        _knockbackDuration -= (float)delta;
        _owner.Velocity = _knockbackVelocity;
        _owner.MoveAndSlide();
        _knockbackVelocity = _knockbackVelocity.MoveToward(Vector2.Zero, _owner.KnockbackDecay * (float)delta);

        // Stop exactly at the duration boundary so this never slides in the same
        // frame as MovementBehavior once the state machine has moved on.
        if (_knockbackDuration <= 0f)
        {
            StopKnockback();
            _owner.NotifyKnockbackFinished();
        }
    }

    public void OnExitTree()
    {
    }

    public void ApplyKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (_owner == null)
            return;

        Vector2 direction = (_owner.GlobalPosition - sourcePosition).Normalized();
        ApplyKnockbackFromDirection(direction, force, duration);
    }

    public void ApplyKnockbackFromDirection(Vector2 direction, float force, float duration = 0.1f)
    {
        if (_owner == null || !_owner.CanBeKnockedBack)
            return;

        float effectiveForce = force / Mathf.Max(_owner.Weight, 0.1f);
        _knockbackVelocity = direction.Normalized() * effectiveForce;
        _knockbackDuration = duration;
    }

    public void ApplyAttackPushback(Vector2 direction, float force, float duration = 0.1f)
    {
        ApplyKnockbackFromDirection(direction, force, duration);
    }

    public bool IsKnockbackActive => _knockbackDuration > 0f;

    public void StopKnockback()
    {
        _knockbackVelocity = Vector2.Zero;
        _knockbackDuration = 0f;
    }
}
