using Godot;

public partial class WeaponManager : Node
{
    public Player Player;
    public Weapon CurrentWeapon { get; private set; }
    public Weapon[] EquippedWeapons { get; private set; } = new Weapon[2];

    public override void _Ready()
    {
        Player = GetTree().Root.FindChild("Player", true, false) as Player;
    }

    public void EquipWeapon(Weapon weapon)
    {
        if (weapon == null)
            return;

        CurrentWeapon?.QueueFree();

        CurrentWeapon = weapon;
        Node weaponParent = GetWeaponParent();
        weaponParent.AddChild(CurrentWeapon);
        CurrentWeapon.Owner = Player;
        EquippedWeapons[0] = weapon;
        Player.Weapon = weapon;
    }

    public void ChangeWeapon(Weapon newWeapon)
    {
        CurrentWeapon.QueueFree();
        EquipWeapon(newWeapon);
    }

    public void EquipWeaponToSlot(PackedScene weaponScene, int slotIndex)
    {
        if (weaponScene == null) return;
        if (slotIndex < 0 || slotIndex >= EquippedWeapons.Length) return;

        Weapon oldWeapon = EquippedWeapons[slotIndex];
        oldWeapon?.QueueFree();

        Weapon instance = weaponScene.Instantiate<Weapon>();
        Node weaponParent = GetWeaponParent();
        weaponParent.AddChild(instance);
        instance.Owner = Player;
        EquippedWeapons[slotIndex] = instance;

        if (CurrentWeapon == null || CurrentWeapon == oldWeapon || slotIndex == 0)
        {
            CurrentWeapon = instance;
            Player.Weapon = instance;
        }
    }

    public int GetCurrentSlotIndex()
    {
        if (CurrentWeapon == null)
        {
            for (int i = 0; i < EquippedWeapons.Length; i++)
            {
                if (EquippedWeapons[i] != null)
                    return i;
            }
            return 0;
        }

        for (int i = 0; i < EquippedWeapons.Length; i++)
        {
            if (EquippedWeapons[i] == CurrentWeapon)
                return i;
        }

        return 0;
    }

    public int GetSlotIndexForScene(PackedScene weaponScene)
    {
        if (weaponScene == null)
            return -1;

        string path = weaponScene.ResourcePath;
        for (int i = 0; i < EquippedWeapons.Length; i++)
        {
            Weapon weapon = EquippedWeapons[i];
            if (weapon == null)
                continue;

            if (IsSameWeaponResource(path, weapon))
                return i;
        }

        return -1;
    }

    public bool IsSceneEquipped(PackedScene weaponScene)
    {
        return GetSlotIndexForScene(weaponScene) >= 0;
    }

    private static bool IsSameWeaponResource(string scenePath, Weapon weapon)
    {
        if (weapon == null)
            return false;

        string instancePath = weapon.SceneFilePath;
        return !string.IsNullOrEmpty(scenePath) && scenePath == instancePath;
    }

    private Node GetWeaponParent()
    {
        if (Player?.WeaponSlot != null)
            return Player.WeaponSlot;
        return Player ?? GetParent();
    }
}