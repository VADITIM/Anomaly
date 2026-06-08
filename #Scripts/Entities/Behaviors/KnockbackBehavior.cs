using Godot;

public class KnockbackBehavior : IEntityBehavior
{
    public bool CanBeKnockedBack { get; set; } = true;
    public float Weight { get; set; } = 1f;
    public float KnockbackDecay { get; set; } = 2000f;

    private Entity owner;
    private Vector2 knockbackVelocity = Vector2.Zero;
    private float knockbackDuration = 0f;

    public void OnReady(Entity owner)
    {
        this.owner = owner;
    }

    public void OnProcess(double delta)
    {
    }

    public void OnPhysicsProcess(double delta)
    {
        if (!CanBeKnockedBack || owner == null)
            return;

        if (knockbackDuration > 0f || knockbackVelocity.Length() > 0.1f)
        {
            knockbackDuration -= (float)delta;
            owner.Velocity = knockbackVelocity;
            owner.MoveAndSlide();
            knockbackVelocity = knockbackVelocity.MoveToward(Vector2.Zero, KnockbackDecay * (float)delta);

            if (knockbackDuration <= 0f && knockbackVelocity.Length() < 1f)
            {
                knockbackVelocity = Vector2.Zero;
                knockbackDuration = 0f;
                    owner.NotifyKnockbackFinished();
            }
        }
    }

    public void OnExitTree()
    {
    }

    public void ApplyKnockback(Vector2 sourcePosition, float force, float duration = 0.1f)
    {
        if (!CanBeKnockedBack || owner == null)
            return;

        Vector2 direction = (owner.GlobalPosition - sourcePosition).Normalized();
        ApplyKnockbackFromDirection(direction, force, duration);
    }

    public void ApplyKnockbackFromDirection(Vector2 direction, float force, float duration = 0.1f)
    {
        if (!CanBeKnockedBack || owner == null)
            return;

        float effectiveForce = force / Mathf.Max(Weight, 0.1f);
        knockbackVelocity = direction.Normalized() * effectiveForce;
        knockbackDuration = duration;
    }

    public void ApplyAttackPushback(Vector2 direction, float force, float duration = 0.1f)
    {
        ApplyKnockbackFromDirection(direction, force, duration);
    }

    public void StopKnockback()
    {
        knockbackVelocity = Vector2.Zero;
        knockbackDuration = 0f;
    }
}