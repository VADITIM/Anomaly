using Godot;

public partial class DamageNumber : Label
{
    private const int LARGE_FONT_SIZE = 64;
    private static readonly FontFile DamageFont = ResourceLoader.Load<FontFile>("res://#Assets/UI/Fonts/TangoSans_Italic.ttf");

    public void Begin(string text, Color color, Vector2 spawnPosition, bool isWeakness)
    {
        Text = text;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 1000;

        if (DamageFont != null)
        {
            AddThemeFontOverride("font", DamageFont);
            AddThemeFontSizeOverride("font_size", LARGE_FONT_SIZE);
        }

        AddThemeColorOverride("font_color", color);

        TweenAnimations.DamageNumberPopup(this, spawnPosition, isWeakness);
    }
}