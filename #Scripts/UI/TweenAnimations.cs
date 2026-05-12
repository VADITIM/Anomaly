using Godot;
public static class TweenAnimations
{

    public static void DamageNumberPopup(Label damageLabel, Vector2 spawnPosition, bool isWeakness)
    {
        damageLabel.Scale = Vector2.One * 0.1f;
        damageLabel.GlobalPosition = spawnPosition;
        damageLabel.Modulate = new Color(1f, 1f, 1f, 1f);

        Vector2 drift = new Vector2(RandomRange(-5f, 5f), RandomRange(-5f, 5f));
        Vector2 popupPosition = spawnPosition + drift + new Vector2(0f, RandomRange(-36f, -24f));
        Vector2 fallPosition = popupPosition + new Vector2(RandomRange(-8f, 8f), RandomRange(20f, 30f));

        Tween tween = damageLabel.CreateTween();
        
        tween.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        Vector2 targetScale = isWeakness ? Vector2.One * .2f : Vector2.One * .15f;
        tween.TweenProperty(damageLabel, "scale", targetScale, 0.15f);
        tween.TweenProperty(damageLabel, "global_position", popupPosition, 0.15f).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
        
        tween.TweenCallback(Callable.From(() => { })).SetDelay(0.1f);
        
        tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(damageLabel, "global_position", fallPosition, 0.3f);
        tween.Parallel().TweenProperty(damageLabel, "modulate:a", 0f, 0.3f);
        
        tween.TweenCallback(Callable.From(damageLabel.QueueFree));
    }

    private static float RandomRange(float min, float max)
    {
        return min + (GD.Randf() * (max - min));
    }
}
