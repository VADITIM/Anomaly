using Godot;

/// <summary>
/// Manages weapon arc slotting into the single player weapon.
/// Handles arc instantiation, slotting, and unslotting.
/// </summary>
public partial class WeaponManager : Node
{
    public Player Player { get; private set; }

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
    }

    /// <summary>
    /// Slots a weapon arc into the player's weapon.
    /// </summary>
    public void SlotWeaponArc(WeaponArc arc)
    {
        if (arc == null || Player?.Weapon == null)
            return;

        Player.Weapon.SlotArc(arc);
    }

    /// <summary>
    /// Instantiates an arc from a scene and slots it into the weapon.
    /// </summary>
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

    /// <summary>
    /// Unslots the current arc from the weapon.
    /// </summary>
    public void UnslotWeaponArc()
    {
        if (Player?.Weapon != null)
        {
            Player.Weapon.UnslotArc();
        }
    }

    /// <summary>
    /// Gets the currently slotted arc.
    /// </summary>
    public WeaponArc GetCurrentArc()
    {
        return Player?.Weapon?.GetCurrentArc();
    }

    /// <summary>
    /// Checks if the given scene is the currently equipped arc.
    /// </summary>
    public bool IsSceneEquipped(PackedScene arcScene)
    {
        if (arcScene == null)
            return false;

        WeaponArc currentArc = GetCurrentArc();
        if (currentArc == null)
            return false;

        return IsSameArcResource(arcScene.ResourcePath, currentArc);
    }

    /// <summary>
    /// Gets a dummy index for UI compatibility. Always returns 0 for single-arc system.
    /// </summary>
    public int GetCurrentSlotIndex()
    {
        return 0;
    }

    /// <summary>
    /// Equipment an arc to a slot. For single-arc system, slotIndex is ignored.
    /// </summary>
    public void EquipWeaponToSlot(PackedScene arcScene, int slotIndex)
    {
        SlotWeaponArcFromScene(arcScene);
    }

    private Node GetWeaponParent()
    {
        if (Player?.WeaponSlot != null)
            return Player.WeaponSlot;
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
