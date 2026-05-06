using Godot;

public partial class Hurtbox : Area2D
{
    [Export] public NodePath OwnerEntityPath { get; set; }

    public Entity OwnerEntity { get; private set; }

    public override void _Ready()
    {
        OwnerEntity = ResolveOwnerEntity();
    }

    private Entity ResolveOwnerEntity()
    {
        if (OwnerEntityPath != null && !OwnerEntityPath.IsEmpty)
            return GetNodeOrNull<Entity>(OwnerEntityPath);

        Node current = GetParent();
        while (current != null)
        {
            if (current is Entity entity)
                return entity;
            current = current.GetParent();
        }

        return null;
    }
}
