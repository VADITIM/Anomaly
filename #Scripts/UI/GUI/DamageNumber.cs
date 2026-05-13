using Godot;

public partial class DamageNumber : Label
{
    // Large font size for crispness - scaled down via Scale property
    private const int LARGE_FONT_SIZE = 64;
    private static readonly FontFile DamageFont = ResourceLoader.Load<FontFile>("res://#Assets/UI/Fonts/TangoSans_Italic.ttf");

    public void Begin(string text, Color color, Vector2 spawnPosition, bool isWeakness)
    {
        Text = text;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 1000;

        // Set font with large size for crispness
        if (DamageFont != null)
        {
            AddThemeFontOverride("font", DamageFont);
            AddThemeFontSizeOverride("font_size", LARGE_FONT_SIZE);
        }

        AddThemeColorOverride("font_color", color);

        // Use the modular tween animation
        TweenAnimations.DamageNumberPopup(this, spawnPosition, isWeakness);
    }
}