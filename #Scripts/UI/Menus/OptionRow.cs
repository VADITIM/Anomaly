using System;
using Godot;

namespace Anomaly;

// One settings line: name on the left, value on the right. Left/right (keys, d-pad, wheel) step the value,
// confirm or click steps it forward. Sliders also draw a fill bar under the value.
public partial class OptionRow : Button
{
    private const float BAR_WIDTH = 40f;

    private Func<string> valueText;
    private Action<int> change;
    private Func<float> fill;
    private Label valueLabel;
    private float punch;

    public static OptionRow Create(string label, Func<string> valueText, Action<int> change, Func<float> fill = null)
    {
        OptionRow row = UiFactory.Style(new OptionRow { Text = label });
        row.valueText = valueText;
        row.change = change;
        row.fill = fill;
        return row;
    }

    public override void _Ready()
    {
        valueLabel = UiFactory.Label("", Art.BodyFont, 6, UiFactory.Accent);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        valueLabel.SetAnchorsPreset(LayoutPreset.RightWide);
        valueLabel.OffsetLeft = -110f;
        valueLabel.OffsetRight = -6f;
        valueLabel.VerticalAlignment = VerticalAlignment.Center;
        valueLabel.PivotOffset = new Vector2(104f, 5f);
        AddChild(valueLabel);
        Pressed += () => Step(1);
        Refresh();
    }

    public void Refresh()
    {
        if (valueLabel == null)
            return;
        valueLabel.Text = valueText();
        QueueRedraw();
    }

    private void Step(int direction)
    {
        change(direction);
        Sfx.Play("ui_move", -8f, direction > 0 ? 1.05f : 0.95f);
        punch = 1f;
        Refresh();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        int direction = 0;
        if (inputEvent.IsActionPressed("ui_left", true))
            direction = -1;
        else if (inputEvent.IsActionPressed("ui_right", true))
            direction = 1;
        else if (inputEvent is InputEventMouseButton { Pressed: true } wheel)
            direction = wheel.ButtonIndex == MouseButton.WheelUp ? 1 : wheel.ButtonIndex == MouseButton.WheelDown ? -1 : 0;
        if (direction == 0)
            return;
        Step(direction);
        AcceptEvent();
    }

    // The value punches on the frame it changes and settles on back.out, so every step answers immediately.
    public override void _Process(double delta)
    {
        if (punch <= 0f || valueLabel == null)
            return;
        punch = Mathf.Max(0f, punch - Motion.Unscaled(delta) / 0.22f);
        valueLabel.Scale = Vector2.One * (1f + 0.25f * (1f - Motion.BackOut(1f - punch)));
    }

    public override void _Draw()
    {
        if (fill == null)
            return;
        float ratio = Mathf.Clamp(fill(), 0f, 1f);
        var origin = new Vector2(Size.X - 6f - BAR_WIDTH, Size.Y - 2f);
        DrawRect(new Rect2(origin, new Vector2(BAR_WIDTH, 1f)), new Color(UiFactory.FrameLine, 0.35f));
        DrawRect(new Rect2(origin, new Vector2(BAR_WIDTH * ratio, 1f)), UiFactory.Rend);
        DrawRect(new Rect2(origin + new Vector2(Mathf.Round(BAR_WIDTH * ratio) - 1f, -1f), new Vector2(1f, 3f)), UiFactory.Accent);
    }
}
