using Godot;

namespace Anomaly;

public partial class DeathOverlay : CanvasLayer
{
    private Control root;
    private ColorRect fade;
    private Label title;
    private Label subtitle;

    public override void _Ready()
    {
        Layer = 25;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        fade = new ColorRect { Color = new Color(0.03f, 0.0f, 0.02f, 0.78f), MouseFilter = Control.MouseFilterEnum.Ignore };
        fade.Position = new Vector2(-2000, -2000);
        fade.Size = new Vector2(4480, 4270);
        root.AddChild(fade);
        root.AddChild(new Band
        {
            Position = new Vector2(0, 108),
            Size = new Vector2(480, 54),
            Fade = 0.35f,
            Fill = new Color(0.36f, 0.02f, 0.07f, 0.55f),
            Edge = new Color(UiFactory.Rend, 0.8f),
        });

        title = UiFactory.Label("SEVERED", Art.TitleFont, 36, new Color(0.96f, 0.86f, 0.82f), 4);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Position = new Vector2(0, 112);
        title.Size = new Vector2(480, 34);
        root.AddChild(title);
        root.AddChild(new Divider { Position = new Vector2(180, 145), Size = new Vector2(120, 5), Line = new Color(UiFactory.Rend, 0.9f) });

        subtitle = UiFactory.Label("The bond does not break. Return to what you dropped.", Art.BodyFont, 6, new Color(0.88f, 0.72f, 0.7f));
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.Position = new Vector2(0, 150);
        subtitle.Size = new Vector2(480, 8);
        root.AddChild(subtitle);
    }

    public void ShowDeath()
    {
        Tween tween = root.CreateTween();
        tween.TweenInterval(0.6f);
        tween.TweenProperty(root, "modulate:a", 1f, 0.8f);
        title.Scale = Vector2.One;
        title.PivotOffset = new Vector2(240, 17);
        title.CreateTween().TweenProperty(title, "scale", Vector2.One * 1.08f, 3f);
    }

    public void HideDeath()
    {
        root.CreateTween().TweenProperty(root, "modulate:a", 0f, 0.6f);
    }

    public void FadeThrough(float duration)
    {
        Tween tween = root.CreateTween();
        title.Visible = false;
        subtitle.Visible = false;
        tween.TweenProperty(root, "modulate:a", 1f, duration * 0.4f);
        tween.TweenInterval(duration * 0.2f);
        tween.TweenProperty(root, "modulate:a", 0f, duration * 0.4f);
        tween.TweenCallback(Callable.From(() =>
        {
            title.Visible = true;
            subtitle.Visible = true;
        }));
    }
}
