using Godot;

namespace Anomaly;

public partial class PostProcessLayer : CanvasLayer
{
    private ShaderMaterial material;
    private float flash;
    private Color flashColor = Colors.White;
    private float aberration;
    private float aetheriac;
    private bool aetheriacActive;
    private ulong lastTicks;

    public override void _Ready()
    {
        Layer = 10;
        ProcessMode = ProcessModeEnum.Always;
        material = new ShaderMaterial { Shader = ShaderLibrary.PostProcess };
        var rect = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rect);
        PlayerSignals.AetheriacStateChanged += OnAetheriacStateChanged;
        lastTicks = Time.GetTicksUsec();
    }

    public override void _ExitTree() => PlayerSignals.AetheriacStateChanged -= OnAetheriacStateChanged;

    private void OnAetheriacStateChanged(bool active) => aetheriacActive = active;

    public void Flash(Color color, float intensity)
    {
        flashColor = color;
        flash = Mathf.Max(flash, intensity * GameSettings.Flashes);
    }

    public void KickAberration(float amount) => aberration = Mathf.Max(aberration, GameSettings.ChromaticAberration ? amount : 0f);

    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksUsec();
        float realDelta = Mathf.Min((now - lastTicks) / 1_000_000f, 0.1f);
        lastTicks = now;
        flash = Mathf.MoveToward(flash, 0f, realDelta * 2.6f);
        aberration = Mathf.MoveToward(aberration, 0f, realDelta * 3f);
        aetheriac = Mathf.MoveToward(aetheriac, aetheriacActive ? 1f : 0f, realDelta * 2.5f);
        material.SetShaderParameter(StringNames.Of("flash"), flash);
        material.SetShaderParameter(StringNames.Of("flash_color"), flashColor);
        material.SetShaderParameter(StringNames.Of("aberration"), aberration + (TimeControl.IsFrozen && GameSettings.ChromaticAberration ? 0.25f : 0f));
        material.SetShaderParameter(StringNames.Of("brightness"), GameSettings.Brightness);
        material.SetShaderParameter(StringNames.Of("grain"), GameSettings.FilmGrain ? 1f : 0f);
        material.SetShaderParameter(StringNames.Of("bloom"), GameSettings.Bloom ? 1f : 0f);
        material.SetShaderParameter(StringNames.Of("aetheriac"), aetheriac);
        material.SetShaderParameter(StringNames.Of("desaturate"), Engine.TimeScale < 0.6 && !TimeControl.IsFrozen ? 0.35f : 0f);
    }
}
