using Godot;

public static class InitializeEntity
{
    public static (Control, TextureProgressBar) InitializeResourceBars(Entity entity)
    {
        Control resourceBarControl = FindResourceBarControl(entity);
        TextureProgressBar healthBar = GetNodeOrNull<TextureProgressBar>(entity, "Health Bar")
                        ?? FindTextureProgressBar(resourceBarControl, "Health Bar")
                        ?? FindTextureProgressBar(entity, "Resource Bar/Health Bar")
                        ?? FindTextureProgressBar(entity, "Enemy Resource Bar/Health Bar")
                        ?? FindTextureProgressBar(entity, "Prop Resource Bar/Health Bar");

        return (resourceBarControl, healthBar);
    }

    private static Control FindResourceBarControl(Entity entity)
    {
        foreach (string candidate in GetResourceBarCandidates(entity))
        {
            Control control = GetNodeOrNull<Control>(entity, candidate) ?? FindChildControl(entity, candidate);
            if (control != null)
                return control;
        }

        return null;
    }

    private static Control FindChildControl(Node node, string nodeName)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Control control && child.Name == nodeName)
                return control;

            Control nested = FindChildControl(child, nodeName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public static TextureProgressBar FindTextureProgressBar(Node node, string nodePath)
    {
        if (node == null)
            return null;

        Node child = node.GetNodeOrNull(nodePath);
        if (child is TextureProgressBar textureProgressBar)
            return textureProgressBar;

        return node.FindChild(nodePath, true, false) as TextureProgressBar;
    }

    private static string[] GetResourceBarCandidates(Entity entity)
    {
        if (entity is Enemy)
            return new[] { "Resource Bar", "Enemy Resource Bar", "Enemy Health Bar" };
        
        if (entity is Prop)
            return new[] { "Resource Bar", "Prop Resource Bar" };
        
        return new[] { "Resource Bar" };
    }

    private static T GetNodeOrNull<T>(Entity entity, string path) where T : Node
    {
        return entity.GetNodeOrNull<T>(path);
    }

    private static T GetNodeOrNull<T>(Node node, string path) where T : Node
    {
        return node.GetNodeOrNull<T>(path);
    }
}
