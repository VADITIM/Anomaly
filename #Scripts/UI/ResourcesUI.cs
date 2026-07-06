using Godot;

public partial class ResourcesUI : Control
{
    [Export] public SegmentManager SegmentManager;
    private ResourceManager _resourceManager;
    private bool _subscribed;

    public override void _Ready()
    {
        if (SegmentManager == null)
            SegmentManager = GetNodeOrNull<SegmentManager>("Segment Manager") ?? GetParent() as SegmentManager;

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

        SegmentManager?.Refresh();
    }

    private void OnResourceChangedHandler(float current, float max)
    {
        SegmentManager?.Refresh();
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
