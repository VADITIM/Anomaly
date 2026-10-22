using Godot;

namespace Anomaly;

// Gameplay framing and UI layout are authored against a fixed 480x270 design canvas. Every view derives its
// scale from the real visible rect, so project window/viewport settings never change what the player sees.
public static class ViewScale
{
    public static readonly Vector2 DESIGN_SIZE = new(480f, 270f);

    public static float Factor(Viewport viewport)
    {
        Vector2 visible = viewport.GetVisibleRect().Size;
        return Mathf.Max(0.01f, Mathf.Min(visible.X / DESIGN_SIZE.X, visible.Y / DESIGN_SIZE.Y));
    }

    public static void Fit(CanvasLayer layer)
    {
        Viewport viewport = layer.GetViewport();
        void apply()
        {
            float factor = Factor(viewport);
            Vector2 visible = viewport.GetVisibleRect().Size;
            layer.Scale = new Vector2(factor, factor);
            layer.Offset = (visible - DESIGN_SIZE * factor) * 0.5f;
        }
        apply();
        viewport.SizeChanged += apply;
        // The viewport outlives the scene (a reload rebuilds every layer), so a freed layer must stop listening.
        layer.TreeExiting += () => viewport.SizeChanged -= apply;
    }
}
