using Godot;

public abstract partial class Enemy
{
    public TenacitySystem TenacitySystem;

    public AnimatedSprite2D TenacityCooldownCue { get; private set; }
    private RichTextLabel _testDisplay;

    [Export] public float DefaultStaggerDuration  { get; set; } = TenacityDefaults.DefaultStaggerDuration;
    [Export] public float DefaultRecoveryDuration { get; set; } = TenacityDefaults.DefaultRecoveryDuration;
    [Export] public float DefaultKnockbackDuration { get; set; } = TenacityDefaults.DefaultKnockbackDuration;

    private void InitializeTenacity()
    {
        TenacityCooldownCue = GetNodeOrNull<AnimatedSprite2D>("Tenacity Broken Animation");
        if (TenacityCooldownCue != null)
            TenacityCooldownCue.Visible = false;

        AddBehavior(new KnockbackBehavior());

        MaxTenacity = Tenacity;
    }

    private void StatsDisplay()
    {
        _testDisplay = GetNodeOrNull<RichTextLabel>("Label")
                      ?? GetNodeOrNull<RichTextLabel>("CanvasLayer/Label")
                      ?? GetTree().CurrentScene?.FindChild("Label", true, false) as RichTextLabel;

        if (_testDisplay != null)
            _testDisplay.BbcodeEnabled = true;
    }
}
