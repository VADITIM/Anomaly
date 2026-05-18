using Godot;

public abstract partial class Enemy
{

    private void StatsDisplay()
    {
        testDisplay = GetNodeOrNull<RichTextLabel>("Label")
                      ?? GetNodeOrNull<RichTextLabel>("CanvasLayer/Label")
                      ?? GetTree().CurrentScene?.FindChild("Label", true, false) as RichTextLabel;

        if (testDisplay != null)
            testDisplay.BbcodeEnabled = true;
    }
    public TenacitySystem TenacitySystem;

    [Export] public float DefaultStaggerDuration { get; set; } = .5f;
    [Export] public float DefaultRecoveryDuration { get; set; } = 5f;
    [Export] public float DefaultKnockbackDuration { get; set; } = 0.2f;

    private void InitializeTenacity()
    {
        AnimationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        TenacityCooldownCue = GetNodeOrNull<AnimatedSprite2D>("Tenacity Broken Animation");
        if (TenacityCooldownCue != null)
            TenacityCooldownCue.Visible = false;

        maxTenacity = tenacity;
    }


}
