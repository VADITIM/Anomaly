using Godot;

namespace Anomaly;

public partial class DamageNumber : Node2D
{
    private static readonly System.Collections.Generic.Dictionary<string, LabelSettings> settingsCache = new();

    public string Text = "0";
    public Color Color = Colors.White;
    public int FontSize = 10;
    public float Lifetime = 0.75f;

    public override void _Ready()
    {
        var label = new Label
        {
            Text = Text,
            LabelSettings = Settings(Color, FontSize),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(80, 20),
            Position = new Vector2(-40, -10),
            TextureFilter = UiFactory.TEXT_FILTER,
        };
        AddChild(label);

        Scale = Vector2.One * 1.8f;
        float drift = (float)GD.RandRange(-10.0, 10.0);
        Tween tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", Position + new Vector2(drift, -18f), Lifetime).SetTrans(Tween.TransitionType.Quart).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "modulate:a", 0f, Lifetime * 0.4f).SetDelay(Lifetime * 0.6f);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }

    private static LabelSettings Settings(Color color, int size)
    {
        string key = $"{color.ToHtml()}_{size}";
        if (!settingsCache.TryGetValue(key, out LabelSettings settings))
        {
            settings = new LabelSettings
            {
                Font = Art.NumberFont,
                FontSize = Art.PixelSize(Art.NumberFont, size),
                FontColor = color,
                OutlineSize = 3,
                OutlineColor = new Color(0.04f, 0.01f, 0.07f),
            };
            settingsCache[key] = settings;
        }
        return settings;
    }
}
