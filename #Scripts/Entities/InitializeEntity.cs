using Godot;

/// <summary>
/// Static helper class for initializing entity resource bars and related UI elements.
/// Handles discovery and initialization of resource bar controls for entities.
/// </summary>
public static class InitializeEntity
{
    /// <summary>
    /// Initializes resource bars for an entity by finding and setting up the health bar UI.
    /// </summary>
    /// <param name="entity">The entity to initialize resource bars for</param>
    /// <returns>Tuple containing (resourceBarControl, healthBar)</returns>
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

    /// <summary>
    /// Finds the resource bar control for an entity by checking candidates.
    /// </summary>
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

    /// <summary>
    /// Recursively searches for a child control with a specific name.
    /// </summary>
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

    /// <summary>
    /// Finds a TextureProgressBar at a given node path, recursively if needed.
    /// </summary>
    public static TextureProgressBar FindTextureProgressBar(Node node, string nodePath)
    {
        if (node == null)
            return null;

        Node child = node.GetNodeOrNull(nodePath);
        if (child is TextureProgressBar textureProgressBar)
            return textureProgressBar;

        return node.FindChild(nodePath, true, false) as TextureProgressBar;
    }

    /// <summary>
    /// Gets the list of candidate names for resource bar controls for an entity.
    /// </summary>
    private static string[] GetResourceBarCandidates(Entity entity)
    {
        if (entity is Enemy)
            return new[] { "Resource Bar", "Enemy Resource Bar", "Enemy Health Bar" };
        
        if (entity is Prop)
            return new[] { "Resource Bar", "Prop Resource Bar" };
        
        return new[] { "Resource Bar" };
    }

    /// <summary>
    /// Generic helper to get a node by path from an entity.
    /// </summary>
    private static T GetNodeOrNull<T>(Entity entity, string path) where T : Node
    {
        return entity.GetNodeOrNull<T>(path);
    }

    /// <summary>
    /// Generic helper to get a node by path from any node.
    /// </summary>
    private static T GetNodeOrNull<T>(Node node, string path) where T : Node
    {
        return node.GetNodeOrNull<T>(path);
    }
}
