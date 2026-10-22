using Godot;

namespace Anomaly;

public static class ShaderLibrary
{
    private static Shader entity;
    private static Shader voidShader;
    private static Shader postProcess;
    private static Shader atmosphere;
    private static Shader edgeGlow;
    private static Shader clouds;
    private static Shader rift;
    private static Shader voidFloor;

    public static Shader Entity => entity ??= GD.Load<Shader>("res://#Shaders/entity.gdshader");
    public static Shader Void => voidShader ??= GD.Load<Shader>("res://#Shaders/void.gdshader");
    public static Shader PostProcess => postProcess ??= GD.Load<Shader>("res://#Shaders/post_process.gdshader");
    public static Shader Atmosphere => atmosphere ??= GD.Load<Shader>("res://#Shaders/atmosphere.gdshader");
    public static Shader EdgeGlow => edgeGlow ??= GD.Load<Shader>("res://#Shaders/edge_glow.gdshader");
    public static Shader Clouds => clouds ??= GD.Load<Shader>("res://#Shaders/clouds.gdshader");
    public static Shader Rift => rift ??= GD.Load<Shader>("res://#Shaders/rift.gdshader");
    public static Shader VoidFloor => voidFloor ??= GD.Load<Shader>("res://#Shaders/void_floor.gdshader");
}
