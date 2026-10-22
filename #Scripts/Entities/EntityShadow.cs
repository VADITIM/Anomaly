using Godot;

namespace Anomaly;

public partial class EntityShadow : Node2D
{
    private Entity owner;
    private float drawnScale = -1f;

    public override void _Ready()
    {
        owner = GetParent<Entity>();
        ShowBehindParent = true;
    }

    public override void _Process(double delta)
    {
        float surface = owner.SupportZ;
        if (float.IsNegativeInfinity(surface) || owner.IsDead && owner.Z < surface - 1f)
        {
            Visible = false;
            return;
        }
        Visible = true;
        float height = Mathf.Max(0f, owner.Z - surface);
        float scale = Mathf.Clamp(1f - height / 120f, 0.45f, 1f);
        Position = new Vector2(0, -surface);
        if (!Mathf.IsEqualApprox(scale, drawnScale))
        {
            drawnScale = scale;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float radius = (owner.Radius + 2.5f) * drawnScale;
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.42f));
        DrawCircle(Vector2.Zero, radius + 1.5f, new Color(0.02f, 0.0f, 0.05f, 0.18f * drawnScale));
        DrawCircle(Vector2.Zero, radius, new Color(0.02f, 0.0f, 0.05f, 0.38f * drawnScale));
    }
}
