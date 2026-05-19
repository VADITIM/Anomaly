using Godot;

public partial class WeaponManager : Node
{
    public Player Player { get; private set; }

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
    }

    public void SlotWeaponArc(WeaponArc arc)
    {
        if (arc == null || Player?.Weapon == null)
            return;

        Player.Weapon.SlotArc(arc);
    }

    public WeaponArc SlotWeaponArcFromScene(PackedScene arcScene)
    {
        if (arcScene == null || Player?.Weapon == null)
            return null;

        WeaponArc arc = arcScene.Instantiate<WeaponArc>();
        if (arc == null)
            return null;

        Node weaponParent = GetWeaponParent();
        weaponParent.AddChild(arc);
        arc.Owner = Player;

        Player.Weapon.SlotArc(arc);
        return arc;
    }

    public void UnslotWeaponArc()
    {
        if (Player?.Weapon != null)
        {
            Player.Weapon.UnslotArc();
        }
    }

    public WeaponArc GetCurrentArc()
    {
        return Player?.Weapon?.GetCurrentArc();
    }

    public bool IsSceneEquipped(PackedScene arcScene)
    {
        if (arcScene == null)
            return false;

        WeaponArc currentArc = GetCurrentArc();
        if (currentArc == null)
            return false;

        return IsSameArcResource(arcScene.ResourcePath, currentArc);
    }

    public int GetCurrentSlotIndex()
    {
        return 0;
    }

    public void EquipWeaponToSlot(PackedScene arcScene, int slotIndex)
    {
        SlotWeaponArcFromScene(arcScene);
    }

    private Node GetWeaponParent()
    {
        if (Player?.Weapon != null)
            return Player.Weapon;
        return Player ?? GetParent();
    }

    private static bool IsSameArcResource(string scenePath, WeaponArc arc)
    {
        if (arc == null)
            return false;

        string instancePath = arc.SceneFilePath;
        return !string.IsNullOrEmpty(scenePath) && scenePath == instancePath;
    }
}
