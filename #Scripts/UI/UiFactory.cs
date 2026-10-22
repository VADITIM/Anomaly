using Godot;

namespace Anomaly;

public static class UiFactory
{
    // Bone text on void ink, gilt for what can be acted on, Rend red for the mark of selection. No cold blues: the
    // only blue left on screen belongs to souls.
    public static readonly Color TextColor = new(0.9f, 0.86f, 0.78f);
    public static readonly Color DimText = new(0.6f, 0.55f, 0.55f);
    public static readonly Color Accent = new(0.93f, 0.75f, 0.47f);
    public static readonly Color Rend = new(0.82f, 0.17f, 0.25f);
    public static readonly Color VoiceColor = new(0.84f, 0.76f, 0.98f);
    public static readonly Color Outline = new(0.03f, 0.01f, 0.04f);
    public static readonly Color Ink = new(0.04f, 0.024f, 0.05f, 0.94f);
    public static readonly Color FrameLine = new(0.52f, 0.41f, 0.35f, 0.75f);
    public static readonly Color Scrim = new(0.02f, 0.0f, 0.03f, 0.6f);

    // Fonts are imported as MSDF (no hinting, no subpixel offsets) so the square pixel-font glyphs stay crisp at any
    // UI scale or project stretch. MSDF has to be sampled
    // with linear filtering; the project default (nearest, for pixel art) would break the glyph edges apart.
    public const CanvasItem.TextureFilterEnum TEXT_FILTER = CanvasItem.TextureFilterEnum.Linear;

    public static LabelSettings Settings(Font font, int size, Color color, int outline = 2)
    {
        return new LabelSettings { Font = font, FontSize = Art.UiPixelSize(font, size), FontColor = color, OutlineSize = outline, OutlineColor = Outline };
    }

    public static Label Label(string text, Font font, int size, Color color, int outline = 2)
    {
        return new Label
        {
            Text = text,
            LabelSettings = Settings(font, size, color, outline),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = TEXT_FILTER,
        };
    }

    public static TextureProgressBar Bar(string under, string progress, string over, Vector2 position, Vector2 size, int margin)
    {
        var bar = new CappedProgressBar
        {
            TextureUnder = under != null ? Art.Texture(under) : null,
            TextureProgress = progress != null ? Art.Texture(progress) : null,
            TextureOver = over != null ? Art.Texture(over) : null,
            NinePatchStretch = true,
            StretchMarginLeft = margin,
            StretchMarginRight = margin,
            StretchMarginTop = 1,
            StretchMarginBottom = 1,
            MinValue = 0,
            MaxValue = 1,
            Step = 0.0001,
            Value = 1,
            Position = position,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        return bar;
    }

    public static FrameStyle Frame(float alpha = 1f, bool ornaments = true)
    {
        return new FrameStyle { Fill = new Color(Ink, Ink.A * alpha), Ornaments = ornaments };
    }

    public static Divider Divider(bool centered = true) => new() { Centered = centered };

    public static Button MenuButton(string text) => Style(new Button { Text = text });

    public static T Style<T>(T button) where T : Button
    {
        button.Alignment = HorizontalAlignment.Left;
        button.FocusMode = Control.FocusModeEnum.All;
        button.CustomMinimumSize = new Vector2(0, 11);
        button.TextureFilter = TEXT_FILTER;
        button.AddThemeFontOverride("font", Art.BodyFont);
        button.AddThemeFontSizeOverride("font_size", Art.UiPixelSize(Art.BodyFont, 6));
        button.AddThemeColorOverride("font_color", DimText);
        button.AddThemeColorOverride("font_hover_color", TextColor);
        button.AddThemeColorOverride("font_focus_color", new Color(1f, 0.95f, 0.88f));
        button.AddThemeColorOverride("font_hover_pressed_color", Accent);
        button.AddThemeColorOverride("font_pressed_color", Accent);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.36f, 0.32f, 0.33f));
        button.AddThemeColorOverride("font_outline_color", Outline);
        button.AddThemeConstantOverride("outline_size", 2);
        // Focus follows the mouse, so hover draws nothing of its own; the mark is the focus style.
        var empty = new StyleBoxEmpty { ContentMarginLeft = MarkStyle.TEXT_INSET, ContentMarginRight = 6f };
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", empty);
        button.AddThemeStyleboxOverride("disabled", empty);
        button.AddThemeStyleboxOverride("pressed", new MarkStyle { Strength = 0.8f });
        button.AddThemeStyleboxOverride("hover_pressed", new MarkStyle { Strength = 0.8f });
        button.AddThemeStyleboxOverride("focus", new MarkStyle());
        button.FocusEntered += () => Sfx.Play("ui_move", -8f);
        return button;
    }
}
