using Godot;
using System;

public partial class ResourcesUI : Control
{
    [Export] public SegmentManager SegmentManager;
    private bool _subscribed;

    public override void _Ready()
    {
        if (SegmentManager == null)
            SegmentManager = GetNodeOrNull<SegmentManager>("Segment Manager") ?? GetParent() as SegmentManager;

        CallDeferred(nameof(DeferredSubscribe));
    }

    private void DeferredSubscribe()
    {
        if (ResourceManager.Instance == null)
        {
            CallDeferred(nameof(DeferredSubscribe));
            return;
        }

        if (_subscribed)
            return;

        ResourceManager.Instance.OnHealthChanged += OnHealthChangedHandler;
        ResourceManager.Instance.OnStaminaChanged += OnStaminaChangedHandler;
        ResourceManager.Instance.OnXpChanged += OnXpChangedHandler;
        ResourceManager.Instance.OnCorruptionChanged += OnResourceChangedHandler;
        ResourceManager.Instance.OnVesselChanged += OnResourceChangedHandler;
        ResourceManager.Instance.OnHealthSChanged += OnResourceChangedHandler;
        ResourceManager.Instance.OnStaminaSChanged += OnResourceChangedHandler;
        _subscribed = true;

        // initial sync
        SegmentManager?.Refresh();
    }

    private void OnHealthChangedHandler(float current, float max)
    {
        SegmentManager?.Refresh();
    }

    private void OnStaminaChangedHandler(float current, float max)
    {
        SegmentManager?.Refresh();
    }

    private void OnXpChangedHandler(float current, float max)
    {
        SegmentManager?.Refresh();
    }

    private void OnResourceChangedHandler(float current, float max)
    {
        SegmentManager?.Refresh();
    }

    public override void _ExitTree()
    {
        if (ResourceManager.Instance == null || !_subscribed)
            return;

        ResourceManager.Instance.OnHealthChanged -= OnHealthChangedHandler;
        ResourceManager.Instance.OnStaminaChanged -= OnStaminaChangedHandler;
        ResourceManager.Instance.OnXpChanged -= OnXpChangedHandler;
        ResourceManager.Instance.OnCorruptionChanged -= OnResourceChangedHandler;
        ResourceManager.Instance.OnVesselChanged -= OnResourceChangedHandler;
        ResourceManager.Instance.OnHealthSChanged -= OnResourceChangedHandler;
        ResourceManager.Instance.OnStaminaSChanged -= OnResourceChangedHandler;
        _subscribed = false;
    }
}
