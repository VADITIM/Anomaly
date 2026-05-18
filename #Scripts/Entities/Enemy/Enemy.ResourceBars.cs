using Godot;

public abstract partial class Enemy
{
    private TextureProgressBar TenacityBar;
    public Texture2D Tenacity_Bar_Normal = ResourceLoader.Load<Texture2D>("uid://brl8bddmdruyt");
    public Texture2D Tenacity_Bar_Active = ResourceLoader.Load<Texture2D>("uid://b2fo3fwaguoep");
    public Texture2D Tenacity_Bar_Cooldown = ResourceLoader.Load<Texture2D>("uid://0s2uvwfy3s50");
    public AnimatedSprite2D TenacityCooldownCue { get; private set; }

    private RichTextLabel testDisplay;


    public override void InitializeBars()
    {
        SetHealth(health);
        SetMaxHealth(health);

        base.InitializeBars();
        TenacityBar = InitializeEntity.FindTextureProgressBar(ResourceBarControl, "Tenacity Bar");
    }

    protected override void UpdateResourceBars()
    {
        base.UpdateResourceBars();

        if (HealthBar != null)
        {
            float healthMax = Mathf.Max(GetMaxHealth(), 1f);
            HealthBar.MaxValue = healthMax;
            HealthBar.Value = Mathf.Clamp(GetHealth(), 0f, healthMax);
        }

        if (TenacityBar is TextureProgressBar texTenacityBar)
        {
            float tenacityMax = Mathf.Max(maxTenacity, 1f);
            texTenacityBar.MaxValue = tenacityMax;
            
            float clampedTenacity = Mathf.Clamp(tenacity, 0f, tenacityMax);
            texTenacityBar.Value = tenacityMax - clampedTenacity;

            UpdateTenacityTexture(texTenacityBar);
        }
    }

    private void UpdateTenacityTexture(TextureProgressBar bar)
    {
        bool isInKnockbackWindow = TenacitySystem?.IsKnockbackActive ?? false;
        bool isInStaggerWindow = TenacitySystem?.IsInStaggerWindow ?? false;
        bool isInRecovery = TenacitySystem?.IsInRecoveryCooldown ?? false;

        if (isInStaggerWindow || isInKnockbackWindow)
        {
            if (Tenacity_Bar_Active != null)
                bar.TextureProgress = Tenacity_Bar_Active;
        }
        else if (isInRecovery)
        {
            if (Tenacity_Bar_Cooldown != null)
                bar.TextureProgress = Tenacity_Bar_Cooldown;
        }
        else
        {
            if (Tenacity_Bar_Normal != null)
                bar.TextureProgress = Tenacity_Bar_Normal;
        }
    }
}