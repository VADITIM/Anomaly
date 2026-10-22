using Godot;

namespace Anomaly;

// Rule between sections: fades out from a central diamond, or from the left one when not centred.
[Tool]
public partial class Divider : Control
{
    [Export] public bool Centered = true;
    [Export] public Color Line = UiFactory.FrameLine;

    public Divider()
    {
        CustomMinimumSize = new Vector2(0, 5);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        Rid item = GetCanvasItem();
        float y = Mathf.Floor(Size.Y * 0.5f);
        var clear = new Color(Line, 0f);
        if (Centered)
        {
            float mid = Mathf.Floor(Size.X * 0.5f);
            UiDraw.Gradient(item, new Rect2(0, y, mid, 1), clear, Line);
            UiDraw.Gradient(item, new Rect2(mid, y, Size.X - mid, 1), Line, clear);
            UiDraw.Diamond(item, new Vector2(mid + 0.5f, y + 0.5f), 1.5f, UiFactory.Rend);
        }
        else
        {
            UiDraw.Gradient(item, new Rect2(2, y, Size.X - 2, 1), Line, clear);
            UiDraw.Diamond(item, new Vector2(2.5f, y + 0.5f), 1.5f, UiFactory.Rend);
        }
    }
}
