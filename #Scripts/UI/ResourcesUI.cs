using Godot;

public partial class ResourcesUI : Control
{
    [Export] public PlayerResourceBars Bars;
    private ResourceManager _resourceManager;
    private bool _subscribed;

    public override void _Ready()
    {
        if (Bars == null)
            Bars = GetNodeOrNull<PlayerResourceBars>("Segment Manager") ?? GetParent() as PlayerResourceBars;

        CallDeferred(nameof(DeferredSubscribe));
    }

    private void DeferredSubscribe()
    {
        var player = GetTree().Root.FindChild("Player", true, false) as Player;
        _resourceManager = player?.ResourceManager;

        if (_resourceManager == null)
        {
            CallDeferred(nameof(DeferredSubscribe));
            return;
        }

        if (_subscribed)
            return;

        _resourceManager.OnHealthChanged += OnResourceChangedHandler;
        _resourceManager.OnStaminaChanged += OnResourceChangedHandler;
        _resourceManager.OnCorruptionChanged += OnResourceChangedHandler;
        _resourceManager.OnVesselChanged += OnResourceChangedHandler;
        _resourceManager.OnHealthSChanged += OnResourceChangedHandler;
        _resourceManager.OnStaminaSChanged += OnResourceChangedHandler;
        _subscribed = true;

        Bars?.Refresh();
    }

    private void OnResourceChangedHandler(float current, float max)
    {
        Bars?.Refresh();
    }

    public override void _ExitTree()
    {
        if (_resourceManager == null || !_subscribed)
            return;

        _resourceManager.OnHealthChanged -= OnResourceChangedHandler;
        _resourceManager.OnStaminaChanged -= OnResourceChangedHandler;
        _resourceManager.OnCorruptionChanged -= OnResourceChangedHandler;
        _resourceManager.OnVesselChanged -= OnResourceChangedHandler;
        _resourceManager.OnHealthSChanged -= OnResourceChangedHandler;
        _resourceManager.OnStaminaSChanged -= OnResourceChangedHandler;
        _subscribed = false;
        _resourceManager = null;
    }
}
