using Godot;

public abstract partial class Enemy
{
    public TenacitySystem TenacitySystem;
    public TenacityBehavior TenacityBehavior { get; private set; }

    public AnimatedSprite2D TenacityCooldownCue { get; private set; }
    private RichTextLabel testDisplay;

    [Export] public float DefaultStaggerDuration  { get; set; } = TenacityDefaults.DefaultStaggerDuration;
    [Export] public float DefaultRecoveryDuration { get; set; } = TenacityDefaults.DefaultRecoveryDuration;
    [Export] public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;

    private void InitializeTenacity()
    {
        TenacityCooldownCue = GetNodeOrNull<AnimatedSprite2D>("Tenacity Broken Animation");
        if (TenacityCooldownCue != null)
            TenacityCooldownCue.Visible = false;

        TenacityBehavior = new TenacityBehavior
        {
            DefaultStaggerDuration  = DefaultStaggerDuration,
            DefaultRecoveryDuration = DefaultRecoveryDuration,
            DefaultKnockbackDuration = DefaultKnockbackDuration,
            GetCurrentTenacity = () => Tenacity,
            SetCurrentTenacity = value => Tenacity = value,
            GetMaxTenacity     = () => MaxTenacity,
            SetMaxTenacity     = value => MaxTenacity = value
        };
        AddBehavior(TenacityBehavior);

        var knockbackBehavior = new KnockbackBehavior
        {
            CanBeKnockedBack = CanBeKnockedBack,
            Weight           = Weight,
            KnockbackDecay   = KnockbackDecay
        };
        AddBehavior(knockbackBehavior);

        MaxTenacity = Tenacity;
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
