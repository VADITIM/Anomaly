using Godot;

public abstract partial class Enemy
{
    public TenacitySystem TenacitySystem;
    public TenacityBehavior TenacityBehavior { get; private set; }

    [Export] public float DefaultStaggerDuration { get; set; } = TenacityDefaults.DefaultStaggerDuration;
    [Export] public float DefaultRecoveryDuration { get; set; } = TenacityDefaults.DefaultRecoveryDuration;
    [Export] public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;

    private void InitializeTenacity()
    {
        InitializeEntity.InitializeNodes(this);
        TenacityCooldownCue = GetNodeOrNull<AnimatedSprite2D>("Tenacity Broken Animation");
        if (TenacityCooldownCue != null)
            TenacityCooldownCue.Visible = false;

        TenacityBehavior = new TenacityBehavior
        {
            DefaultStaggerDuration = DefaultStaggerDuration,
            DefaultRecoveryDuration = DefaultRecoveryDuration,
            DefaultKnockbackDuration = DefaultKnockbackDuration,
            GetCurrentTenacity = () => tenacity,
            SetCurrentTenacity = value => tenacity = value,
            GetMaxTenacity = () => maxTenacity,
            SetMaxTenacity = value => maxTenacity = value
        };
        AddBehavior(TenacityBehavior);

        var knockbackBehavior = new KnockbackBehavior
        {
            CanBeKnockedBack = canBeKnockbacked,
            Weight = weight,
            KnockbackDecay = knockbackDecay
        };
        AddBehavior(knockbackBehavior);

        maxTenacity = tenacity;
    }

    private void StatsDisplay()
    {
        testDisplay = GetNodeOrNull<RichTextLabel>("Label")
                      ?? GetNodeOrNull<RichTextLabel>("CanvasLayer/Label")
                      ?? GetTree().CurrentScene?.FindChild("Label", true, false) as RichTextLabel;

        if (testDisplay != null)
            testDisplay.BbcodeEnabled = true;
    }

}
