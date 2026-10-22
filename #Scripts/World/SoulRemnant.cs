using Godot;

namespace Anomaly;

public partial class SoulRemnant : Node2D
{
    private GameWorld world;
    private int souls;
    private float time;
    private float surface;

    public void Setup(GameWorld gameWorld, Vector2 position, int amount)
    {
        world = gameWorld;
        Position = position;
        souls = amount;
        float z = world.Grid.SurfaceZAt(position);
        surface = float.IsNegativeInfinity(z) ? 0f : z;
    }

    public override void _Ready()
    {
        AddChild(LightFactory.Point(new Color(0.45f, 0.85f, 1f), 0.8f, 1f, new Vector2(0, -surface - 8f), 128));
    }

    public override void _Process(double delta)
    {
        time += (float)delta;
        Player player = world.Player;
        if (!player.IsDead && player.GlobalPosition.DistanceTo(GlobalPosition) < 14f && Mathf.Abs(player.Z - surface) < 12f)
        {
            player.AddSouls(souls);
            Sfx.Play("heal", -4f, 1f);
            Vfx.Ring(world.Effects, GlobalPosition + new Vector2(0, -surface), 4f, 34f, new Color(0.5f, 0.9f, 1f), 0.45f, 3f);
            Vfx.Burst(world.Effects, GlobalPosition + new Vector2(0, -surface - 8f), 20, new Color(0.55f, 0.9f, 1f), 90f, Vector2.Up, 180f, 0.6f, -40f, 2f, 30f, true);
            Vfx.Number(world.Overlay, player.GlobalPosition + new Vector2(0, -player.Z - 34f), $"+{souls}", new Color(0.6f, 0.9f, 1f), 10);
            world.ClearSoulRemnant(this);
            return;
        }
        if (GD.Randf() < 0.3f)
            Vfx.Burst(world.Effects, GlobalPosition + new Vector2((float)GD.RandRange(-6, 6), -surface - 4f), 1, new Color(0.5f, 0.85f, 1f), 20f, Vector2.Up, 15f, 0.9f, -25f, 1.5f, 5f, true);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 center = new(0, -surface);
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
        DrawSetTransform(center, 0f, new Vector2(1f, 0.45f));
        DrawCircle(Vector2.Zero, 10f + pulse * 2f, new Color(0.3f, 0.7f, 1f, 0.25f));
        DrawCircle(Vector2.Zero, 5f, new Color(0.7f, 0.95f, 1f, 0.6f));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawColoredPolygon(new[] { center + new Vector2(0, -14f - pulse * 2f), center + new Vector2(3f, -8f), center + new Vector2(0, -3f), center + new Vector2(-3f, -8f) }, new Color(0.75f, 0.95f, 1f, 0.9f));
    }
}
