using Godot;

public class EnemyResourceBarBehavior : ResourceBarBehavior
{
    private readonly Texture2D tenacityBarNormal   = ResourceLoader.Load<Texture2D>("uid://brl8bddmdruyt");
    private readonly Texture2D tenacityBarActive   = ResourceLoader.Load<Texture2D>("uid://b2fo3fwaguoep");
    private readonly Texture2D tenacityBarCooldown = ResourceLoader.Load<Texture2D>("uid://0s2uvwfy3s50");

    private Enemy enemy;
    private TextureProgressBar tenacityBar;

    public override void OnReady(Entity owner)
    {
        base.OnReady(owner);
        enemy = owner as Enemy;
        tenacityBar = InitializeEntity.FindTextureProgressBar(ResourceBarControl, "Tenacity Bar");
    }

    public override void OnPhysicsProcess(double delta)
    {
        base.OnPhysicsProcess(delta);
        UpdateTenacityBar();
    }

    public override void OnHealthChanged(float health, float maxHealth)
    {
        base.OnHealthChanged(health, maxHealth);

        if (HealthBar != null)
        {
            float healthMax = Mathf.Max(maxHealth, 1f);
            HealthBar.MaxValue = healthMax;
            HealthBar.Value = Mathf.Clamp(health, 0f, healthMax);
        }
    }

    private void UpdateTenacityBar()
    {
        if (tenacityBar == null || enemy == null)
            return;

        float tenacityMax = Mathf.Max(enemy.MaxTenacity, 1f);
        tenacityBar.MaxValue = tenacityMax;

        float clampedTenacity = Mathf.Clamp(enemy.Tenacity, 0f, tenacityMax);
        tenacityBar.Value = tenacityMax - clampedTenacity;

        UpdateTenacityTexture(tenacityBar);
    }

    private void UpdateTenacityTexture(TextureProgressBar bar)
    {
        bool isInKnockbackWindow = enemy.TenacitySystem?.IsKnockbackActive ?? false;
        bool isInStaggerWindow   = enemy.TenacitySystem?.IsInStaggerWindow ?? false;
        bool isInRecovery        = enemy.TenacitySystem?.IsInRecoveryCooldown ?? false;

        if (isInStaggerWindow || isInKnockbackWindow)
        {
            if (tenacityBarActive != null)
                bar.TextureProgress = tenacityBarActive;
        }
        else if (isInRecovery)
        {
            if (tenacityBarCooldown != null)
                bar.TextureProgress = tenacityBarCooldown;
        }
        else
        {
            if (tenacityBarNormal != null)
                bar.TextureProgress = tenacityBarNormal;
        }
    }
}
