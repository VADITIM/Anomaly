using Godot;

namespace Anomaly;

// A Source in the forge's Sources column. Dragging it onto the Scythe's source slot equips it; pressing it does the same
// for keyboard and gamepad. Locked Sources show as silhouettes and cannot be picked up.
public partial class ArcCard : Button
{
    private const float DRAG_ICON_SIZE = 22f;

    public string ArcId;
    public bool Locked;
    public Texture2D Art;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Locked || Art == null)
            return default;
        float size = DRAG_ICON_SIZE * ViewScale.Factor(GetViewport());
        var icon = new TextureRect
        {
            Texture = Art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Size = new Vector2(size, size),
            Position = new Vector2(-size * 0.5f, -size * 0.5f),
            Modulate = new Color(1f, 1f, 1f, 0.9f),
        };
        var holder = new Control();
        holder.AddChild(icon);
        SetDragPreview(holder);
        return ArcId;
    }
}
