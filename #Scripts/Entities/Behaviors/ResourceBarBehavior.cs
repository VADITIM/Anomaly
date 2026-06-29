using Godot;

public class ResourceBarBehavior : IEntityBehavior
{
    protected Entity Owner;
    protected Control ResourceBarControl;
    protected TextureProgressBar HealthBar;
    protected TextureProgressBar HealthBarGhost;

    private HealthBarAnimator healthBarAnimator;

    public virtual void OnReady(Entity owner)
    {
        Owner = owner;

        (ResourceBarControl, HealthBar) = InitializeEntity.InitializeResourceBars(owner);

        if (ResourceBarControl != null)
            HealthBarGhost = ResourceBarControl.GetNodeOrNull<TextureProgressBar>("Health Bar Ghost")
                          ?? ResourceBarControl.GetNodeOrNull<TextureProgressBar>("HealthBarGhost");

        healthBarAnimator = new HealthBarAnimator();
        healthBarAnimator.Initialize(ResourceBarControl, HealthBar, HealthBarGhost, owner.CurrentHealth);

        OnHealthChanged(owner.CurrentHealth, owner.CurrentMaxHealth);
    }

    public virtual void OnProcess(double delta)
    {
        healthBarAnimator?.Update((float)delta);
    }

    public virtual void OnPhysicsProcess(double delta) { }

    public virtual void OnExitTree() { }

    public virtual void OnHealthChanged(float health, float maxHealth)
    {
        healthBarAnimator?.OnHealthChanged(health, maxHealth);
    }

    public void DestroyBars()
    {
        ResourceBarControl?.QueueFree();
    }
}
