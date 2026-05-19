using Godot;

public partial class Player
{
    public void InitializePlayer()
    {
        Node firstChild = Weapon.GetChildCount() > 0 ? Weapon.GetChild(0) : null;
        if (firstChild is Weapon weaponNode)
        {
            Weapon = weaponNode;
        }
        else if (firstChild is WeaponArc arcNode)
        {
            PackedScene weaponScene = GD.Load<PackedScene>("res://#Scenes/Entities/Weapon.tscn");
            Weapon weaponContainer = weaponScene.Instantiate<Weapon>();
            Weapon.RemoveChild(arcNode);
            weaponContainer.AddChild(arcNode);
            weaponContainer.SlotArc(arcNode);
            Weapon.AddChild(weaponContainer);
            Weapon = weaponContainer;
        }
        else
        {
            PackedScene weaponScene = GD.Load<PackedScene>("res://#Scenes/Entities/Weapon.tscn");
            Weapon weaponContainer = weaponScene.Instantiate<Weapon>();
            Weapon.AddChild(weaponContainer);
            Weapon = weaponContainer;
        }
    }
}