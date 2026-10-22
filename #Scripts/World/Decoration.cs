using Godot;

namespace Anomaly;

public partial class Decoration : Node2D
{
    private Sprite2D sprite;
    private GameWorld world;
    private Rect2 canopyRect;
    private bool fadesWhenOccluding;
    private float swayPhase;
    private bool sways;

    public void Setup(GameWorld gameWorld, SpriteRegion region, Vector2 groundPosition, float elevation, bool fadeWhenOccluding, bool sway, Color tint)
    {
        world = gameWorld;
        Position = groundPosition;
        sprite = Art.CreateSprite(region);
        sprite.Position = new Vector2(0, -elevation);
        sprite.Modulate = tint;
        AddChild(sprite);
        fadesWhenOccluding = fadeWhenOccluding;
        sways = sway;
        swayPhase = groundPosition.X * 0.05f;
        canopyRect = new Rect2(groundPosition + sprite.Position - region.Origin * region.Scale, region.ScaledSize);
        canopyRect = canopyRect.GrowSide(Side.Bottom, -12f);
        SetProcess(fadeWhenOccluding || sway);
    }

    public override void _Process(double delta)
    {
        if (sways)
        {
            swayPhase += (float)delta;
            sprite.Skew = Mathf.Sin(swayPhase * 1.3f) * 0.025f;
        }
        if (!fadesWhenOccluding)
            return;
        Player player = world.Player;
        Vector2 playerVisual = player.GlobalPosition + new Vector2(0, -player.Z - 12f);
        bool behind = player.GlobalPosition.Y < GlobalPosition.Y && canopyRect.HasPoint(playerVisual);
        float target = behind ? 0.42f : 1f;
        sprite.SelfModulate = new Color(1f, 1f, 1f, Mathf.MoveToward(sprite.SelfModulate.A, target, (float)delta * 4f));
    }
}
