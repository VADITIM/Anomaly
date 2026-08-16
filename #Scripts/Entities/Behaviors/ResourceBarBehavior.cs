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

    // The Enemy outlives its bars — it keeps processing through the death
    // animation before QueueFree. Drop the references so nothing touches a freed
    // node (a freed Godot node is not null, it throws on first access).
    public virtual void DestroyBars()
    {
        ResourceBarControl?.QueueFree();

        ResourceBarControl = null;
        HealthBar = null;
        HealthBarGhost = null;
        healthBarAnimator = null;
    }
}
