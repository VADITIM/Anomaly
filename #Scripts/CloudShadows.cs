using Godot;

public partial class CloudShadows : ColorRect
{
    [Export] public Camera2D TargetCamera;
    private ShaderMaterial _shaderMat;

    public override void _Ready()
    {
        _shaderMat = Material as ShaderMaterial;
    }

    public override void _Process(double delta)
    {
        if (_shaderMat != null && TargetCamera != null)
        {
            // Update the shader with camera position
            _shaderMat.SetShaderParameter("world_offset", TargetCamera.GlobalPosition);
        }
    }
}