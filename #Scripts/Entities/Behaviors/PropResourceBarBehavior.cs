using Godot;

public class PropResourceBarBehavior : ResourceBarBehavior
{
    private const float VisibleSeconds = 8f;
    private const float FadeSeconds = 0.5f;

    private Tween fadeTween;

    public override void OnReady(Entity owner)
    {
        base.OnReady(owner);
        Hide();
    }

    public void ShowForHit()
    {
        if (ResourceBarControl == null)
            return;

        fadeTween?.Kill();
        ResourceBarControl.Visible = true;
        ResourceBarControl.Modulate = WithAlpha(1f);

        fadeTween = Owner.CreateTween();
        fadeTween.TweenInterval(VisibleSeconds);
        fadeTween.TweenProperty(ResourceBarControl, "modulate:a", 0f, FadeSeconds);
        fadeTween.TweenCallback(Callable.From(Hide));
    }

    private void Hide()
    {
        if (ResourceBarControl == null)
            return;

        ResourceBarControl.Visible = false;
        ResourceBarControl.Modulate = WithAlpha(0f);
    }

    private Color WithAlpha(float alpha)
    {
        Color current = ResourceBarControl.Modulate;
        return new Color(current.R, current.G, current.B, alpha);
    }
}
