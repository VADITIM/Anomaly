using Godot;

namespace Anomaly;

public partial class TimeControlDriver : Node
{
    private ulong lastTicks;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        lastTicks = Time.GetTicksUsec();
    }

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        float realDelta = Mathf.Min((now - lastTicks) / 1_000_000f, 0.1f);
        lastTicks = now;
        TimeControl.Update(realDelta);
    }

    public override void _ExitTree() => TimeControl.Reset();
}
