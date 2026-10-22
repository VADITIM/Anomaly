using Godot;

namespace Anomaly;

public static class NodeResolver
{
    public static T FindOnOwner<T>(Node requester) where T : Node
    {
        Node owner = requester.Owner ?? requester.GetParent();
        return owner == null ? null : FindInSubtree<T>(owner);
    }

    public static T FindOnRoot<T>(Node requester) where T : Node
    {
        Node root = requester.GetTree()?.CurrentScene ?? TopLevelAncestor(requester);
        if (root is T typedRoot)
            return typedRoot;
        return root == null ? null : FindInSubtree<T>(root);
    }

    public static T LogMissingAndCreate<T>(Node requester) where T : Node, new()
    {
        GD.PushError($"{requester.Name}: missing {typeof(T).Name}, creating a fallback instance.");
        var created = new T();
        (requester.Owner ?? requester).AddChild(created);
        return created;
    }

    public static T Resolve<T>(Node requester) where T : Node, new()
    {
        return FindOnOwner<T>(requester) ?? FindOnRoot<T>(requester) ?? LogMissingAndCreate<T>(requester);
    }

    private static Node TopLevelAncestor(Node node)
    {
        Node current = node;
        while (current.GetParent() != null && current.GetParent() is not Window)
            current = current.GetParent();
        return current;
    }

    private static T FindInSubtree<T>(Node node) where T : Node
    {
        if (node is T match)
            return match;
        foreach (Node child in node.GetChildren())
        {
            T found = FindInSubtree<T>(child);
            if (found != null)
                return found;
        }
        return null;
    }
}
