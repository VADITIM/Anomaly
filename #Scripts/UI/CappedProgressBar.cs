using Godot;

namespace Anomaly;

// A partially filled nine-patch bar normally hides its right margin section until the fill reaches it, cutting off
// the dark end cap. This redraws the progress texture's tail at the fill's leading edge so the cap is always there.
public partial class CappedProgressBar : TextureProgressBar
{
    public override void _Draw()
    {
        if (!NinePatchStretch || TextureProgress == null || FillMode != (int)FillModeEnum.LeftToRight)
            return;
        double ratio = (Value - MinValue) / (MaxValue - MinValue);
        if (ratio <= 0.0 || ratio >= 1.0)
            return;

        float filled = Size.X * (float)ratio;
        // Once the fill is inside the right margin, the engine draws the cap itself.
        if (filled >= Size.X - StretchMarginRight)
            return;

        Vector2 textureSize = TextureProgress.GetSize();
        float tail = Mathf.Min(Mathf.Min(StretchMarginRight, textureSize.X), Mathf.Max(0f, filled - StretchMarginLeft));
        if (tail <= 0f)
            return;

        var source = new Rect2(textureSize.X - tail, 0f, tail, textureSize.Y);
        var target = new Rect2(filled - tail, 0f, tail, Size.Y);
        DrawTextureRectRegion(TextureProgress, target, source, TintProgress);
    }
}
