using Godot;

namespace Anomaly;

// A lone Rend diamond, centred on its position: end caps and markers.
[Tool]
public partial class DiamondMark : Control
{
    [Export] public float Radius = 2f;
    [Export] public Color Fill = UiFactory.Rend;

    public DiamondMark() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => UiDraw.Diamond(GetCanvasItem(), Vector2.Zero, Radius, Fill);
}
