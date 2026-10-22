using Godot;

namespace Anomaly;

public partial class PropResourceBar : Node2D
{
    private const string ROOT = "UI/Resources UI/Prop/Health Bar/";

    private Prop owner;
    private float alpha;
    private float revealTimer;
    private Texture2D under;
    private Texture2D progress;
    private Texture2D over;

    public void Setup(Prop prop, float height)
    {
        owner = prop;
        Position = new Vector2(-16, height);
        ZIndex = 5;
        ZAsRelative = true;
    }

    public override void _Ready()
    {
        under = Art.Texture(ROOT + "Prop_Resources_Under.png");
        progress = Art.Texture(ROOT + "Prop_Resources_Progress.png");
        over = Art.Texture(ROOT + "Prop_Resources_Over.png");
        Visible = false;
    }

    public void Reveal() => revealTimer = 2.5f;

    public override void _Process(double delta)
    {
        revealTimer -= (float)delta;
        alpha = Mathf.MoveToward(alpha, revealTimer > 0f ? 1f : 0f, (float)delta * 5f);
        Visible = alpha > 0.01f;
        Modulate = new Color(1f, 1f, 1f, alpha);
        if (Visible)
            QueueRedraw();
    }

    public override void _Draw()
    {
        DrawTexture(under, Vector2.Zero);
        Vector2 size = progress.GetSize();
        float width = Mathf.Round(size.X * owner.HealthRatio);
        if (width > 0f)
            DrawTextureRectRegion(progress, new Rect2(Vector2.Zero, new Vector2(width, size.Y)), new Rect2(0, 0, width, size.Y));
        DrawTexture(over, Vector2.Zero);
    }
}
