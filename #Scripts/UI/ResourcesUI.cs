using Godot;
using System;

public partial class ResourcesUI : Control
{
    [Export] public SegmentManager SegmentManager;

    public override void _Ready()
    {
        if (SegmentManager == null)
            SegmentManager = GetNodeOrNull<SegmentManager>("Segment Manager") ?? GetParent() as SegmentManager;

        CallDeferred(nameof(DeferredSubscribe));
    }

    private void DeferredSubscribe()
    {
        if (ResourceManager.Instance == null)
            return;

        ResourceManager.Instance.OnHealthChanged += OnHealthChangedHandler;
        ResourceManager.Instance.OnStaminaChanged += OnStaminaChangedHandler;
        ResourceManager.Instance.OnXpChanged += OnXpChangedHandler;

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

    public override void _ExitTree()
    {
        if (ResourceManager.Instance == null)
            return;

        ResourceManager.Instance.OnHealthChanged -= OnHealthChangedHandler;
        ResourceManager.Instance.OnStaminaChanged -= OnStaminaChangedHandler;
        ResourceManager.Instance.OnXpChanged -= OnXpChangedHandler;
    }
}
