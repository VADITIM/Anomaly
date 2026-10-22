using Godot;

namespace Anomaly;

// Rows of stat name, current value and what the next level adds, shared by the bonfire and Maldor's forge. The current
// value sits on the left, the gain on the right.
public static class StatTable
{
    public const string MAXED = "MAX";
    private const float VALUE_WIDTH = 24f;

    public static GridContainer Create()
    {
        var grid = new GridContainer { Columns = 3, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 3);
        return grid;
    }

    public static void Row(GridContainer grid, string name, string current, string gain, Color gainColor, bool dim = false)
    {
        Label nameLabel = UiFactory.Label(name, Art.BodyFont, 6, UiFactory.DimText);
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(nameLabel);
        Label currentLabel = UiFactory.Label(current, Art.BodyFont, 6, dim ? UiFactory.DimText : UiFactory.TextColor);
        currentLabel.CustomMinimumSize = new Vector2(VALUE_WIDTH, 0);
        currentLabel.HorizontalAlignment = HorizontalAlignment.Right;
        grid.AddChild(currentLabel);
        Label gainLabel = UiFactory.Label(gain, Art.BodyFont, 6, dim || gain == MAXED ? UiFactory.DimText : gainColor);
        gainLabel.CustomMinimumSize = new Vector2(VALUE_WIDTH, 0);
        gainLabel.HorizontalAlignment = HorizontalAlignment.Right;
        grid.AddChild(gainLabel);
    }

    public static string Gain(float delta, string format = "0.0", string unit = "") =>
        (delta < 0f ? "-" : "+") + Mathf.Abs(delta).ToString(format) + unit;
}
