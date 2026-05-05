using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class WeaponInventory : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Export] public Godot.Collections.Array<PackedScene> StartingWeapons = new();

    private readonly List<PackedScene> _weaponScenes = new();

    public IReadOnlyList<PackedScene> WeaponScenes => _weaponScenes;

    public override void _Ready()
    {
        foreach (PackedScene scene in StartingWeapons)
        {
            AddWeapon(scene);
        }
    }

    public bool AddWeapon(PackedScene weaponScene)
    {
        if (weaponScene == null)
            return false;

        string path = weaponScene.ResourcePath;
        bool exists = _weaponScenes.Any(w => w?.ResourcePath == path);
        if (exists)
            return false;

        _weaponScenes.Add(weaponScene);
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool RemoveWeapon(PackedScene weaponScene)
    {
        if (weaponScene == null)
            return false;

        bool removed = _weaponScenes.Remove(weaponScene);
        if (removed)
            EmitSignal(SignalName.InventoryChanged);
        return removed;
    }

    public PackedScene GetWeaponScene(int index)
    {
        if (index < 0 || index >= _weaponScenes.Count)
            return null;
        return _weaponScenes[index];
    }

    public int GetPageCount(int pageSize)
    {
        if (pageSize <= 0)
            return 0;
        return Mathf.CeilToInt((float)_weaponScenes.Count / pageSize);
    }

    public IEnumerable<(PackedScene scene, int index)> GetPageItems(int pageIndex, int pageSize)
    {
        if (pageSize <= 0 || pageIndex < 0)
            yield break;

        int start = pageIndex * pageSize;
        int end = Mathf.Min(start + pageSize, _weaponScenes.Count);
        for (int i = start; i < end; i++)
        {
            yield return (_weaponScenes[i], i);
        }
    }
}
