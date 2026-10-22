using Godot;

namespace Anomaly;

// One column of a broken enemy bar: thrown along the killing blow, spun, dropped, then cut away on power2.in.
public partial class EnemyBarShard : Node2D
{
    private const float GRAVITY = 520f;
    private const float LEAVE_TIME = 0.12f;
    private const float FLASH_TIME = 0.04f;

    private BarFrame frame;
    private float x0;
    private float x1;
    private Vector2 velocity;
    private float spin;
    private float lifetime;
    private float age;
    private Vector2 baseScale;
    private Transform2D spawnTransform;
    private ShaderMaterial material;

    public void Setup(BarFrame snapshot, float left, float right, Transform2D barTransform, Vector2 direction, float spread)
    {
        frame = snapshot;
        x0 = left;
        x1 = right;
        float center = (left + right) * 0.5f;
        spawnTransform = barTransform * Transform2D.Identity.Translated(new Vector2(center, 4.5f));
        float outward = (center - EnemyResourceBar.WIDTH * 0.5f) / (EnemyResourceBar.WIDTH * 0.5f);
        velocity = direction * (float)GD.RandRange(40.0, 110.0) * spread
            + new Vector2(outward * 70f + (float)GD.RandRange(-25.0, 25.0), (float)GD.RandRange(-150.0, -70.0)) * spread;
        spin = (float)GD.RandRange(-14.0, 14.0) * spread;
        lifetime = (float)GD.RandRange(0.5, 0.75);
        ZIndex = 6;
    }

    public override void _Ready()
    {
        GlobalTransform = spawnTransform;
        baseScale = Scale;
        material = new ShaderMaterial { Shader = ShaderLibrary.Entity };
        material.SetShaderParameter(StringNames.Of("flash"), 1f);
        Material = material;
    }

    public override void _Process(double delta)
    {
        float dt = Motion.Unscaled(delta);
        age += dt;
        velocity.Y += GRAVITY * dt;
        Position += velocity * dt;
        Rotation += spin * dt;
        material.SetShaderParameter(StringNames.Of("flash"), age < FLASH_TIME ? 1f : 0f);
        float leave = (age - (lifetime - LEAVE_TIME)) / LEAVE_TIME;
        if (leave > 0f)
            Scale = baseScale * (1f - Motion.Power2In(leave));
        if (age >= lifetime)
            QueueFree();
    }

    public override void _Draw()
    {
        float center = (x0 + x1) * 0.5f;
        BarTextures.Shared.Draw(this, frame, x0, x1, new Vector2(-center, -4.5f));
    }
}
