using Godot;

namespace Anomaly;

public partial class SpriteSheetEffect : Sprite2D
{
    public int FrameCount = 1;
    public float FramesPerSecond = 30f;
    public bool Reverse;
    private float time;

    public override void _Ready()
    {
        Hframes = FrameCount;
        Frame = Reverse ? FrameCount - 1 : 0;
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        int frame = (int)(time * FramesPerSecond);
        if (frame >= FrameCount)
        {
            QueueFree();
            return;
        }
        Frame = Reverse ? FrameCount - 1 - frame : frame;
    }
}
