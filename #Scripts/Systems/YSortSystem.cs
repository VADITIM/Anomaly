using Godot;

public static class YSortSystem
{
    public static void Update(Entity entity)
    {
        if (entity == null)
            return;

        int sortIndex = (int)entity.GlobalPosition.Y;
        entity.ZIndex = sortIndex;
        ApplyToVisualChildren(entity, sortIndex);
    }

    private static void ApplyToVisualChildren(Node node, int sortIndex)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Sprite2D sprite)
                sprite.ZIndex = sortIndex;
            else if (child is AnimatedSprite2D animatedSprite)
                animatedSprite.ZIndex = sortIndex;

            ApplyToVisualChildren(child, sortIndex);
        }
    }
}
