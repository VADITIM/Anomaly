using Godot;

public partial class Player
{
        protected override bool CanTakeDamage(float damage, Vector2 sourcePosition)
    {
        return !(GetBehavior<DodgeBehavior>()?.IsIFrameActive ?? false);
    }

    public override void TakeDamage(float damage, Vector2 sourcePosition, WeaponArc weapon = null)
    {
        if (StateMachine != null && StateMachine.IsDead)
            return;

        base.TakeDamage(damage, sourcePosition, weapon);
    }

    protected override float ApplyDamageModifiers(float damage, Vector2 sourcePosition)
    {
        float armor = Stats?.GetCurrentMax("Armor") ?? 0f;
        return damage * (1f - armor / 100f);
    }


    protected override void OnDamageTaken(float damage, Vector2 sourcePosition, float previousHealth, float newHealth)
    {
        _lastDamageDirection = GetDirectionFromVector(sourcePosition - GlobalPosition, out _);
        ResourceManager?.ResetConsecutiveHits();

        if (newHealth <= 0f)
            return;

        Vector2 knockbackDir = (GlobalPosition - sourcePosition).Normalized();
        GetBehavior<KnockbackBehavior>()?.ApplyKnockbackFromDirection(knockbackDir, 200f, 0.2f);
    }
    protected override void OnDeath(Vector2 sourcePosition)
    {
        StateMachine.RequestDeath();
    }

}