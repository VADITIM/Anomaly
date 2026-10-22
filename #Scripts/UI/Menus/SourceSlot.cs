using System;
using Godot;

namespace Anomaly;

// Drop target on the Scythe: takes an ArcCard's id. It lights up while a droppable Source is being dragged.
public partial class SourceSlot : PanelContainer
{
    private StyleBox resting;
    private StyleBox lit;

    public Func<string, bool> Accepts;
    public event Action<string> Dropped;

    public void Style(Color tint)
    {
        resting = Plate(new Color(tint, 0.45f));
        lit = Plate(UiFactory.Accent);
        AddThemeStyleboxOverride("panel", resting);
    }

    private static FrameStyle Plate(Color line) => new FrameStyle { Fill = new Color(0.02f, 0.01f, 0.03f, 0.85f), Line = line, Ornaments = false }.SetPadding(3f, 2f);

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.String && Accepts?.Invoke(data.AsString()) == true;

    public override void _DropData(Vector2 atPosition, Variant data) => Dropped?.Invoke(data.AsString());

    public override void _Notification(int what)
    {
        if (what == NotificationDragBegin && GetViewport().GuiGetDragData().VariantType == Variant.Type.String)
            AddThemeStyleboxOverride("panel", lit);
        else if (what == NotificationDragEnd)
            AddThemeStyleboxOverride("panel", resting);
    }
}
