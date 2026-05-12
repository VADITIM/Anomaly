using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class WeaponArcInventory : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Export] public Godot.Collections.Array<PackedScene> StartingWeaponArc = new();

    private readonly List<PackedScene> WeaponArcScenes = new();

    public IReadOnlyList<PackedScene> WeaponScenes => WeaponArcScenes;

    public override void _Ready()
    {
        foreach (PackedScene scene in StartingWeaponArc)
        {
            AddWeapon(scene);
        }
    }

    public bool AddWeapon(PackedScene WeaponArc)
    {
        if (WeaponArc == null)
            return false;

        string path = WeaponArc.ResourcePath;
        bool exists = WeaponArcScenes.Any(w => w?.ResourcePath == path);
        if (exists)
            return false;

        WeaponArcScenes.Add(WeaponArc);
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool RemoveWeapon(PackedScene WeaponArc)
    {
        if (WeaponArc == null)
            return false;

        bool removed = WeaponArcScenes.Remove(WeaponArc);
        if (removed)
            EmitSignal(SignalName.InventoryChanged);
        return removed;
    }

    public PackedScene GetWeaponArcScene(int index)
    {
        if (index < 0 || index >= WeaponArcScenes.Count)
            return null;
        return WeaponArcScenes[index];
    }

    public int GetPageCount(int pageSize)
    {
        if (pageSize <= 0)
            return 0;
        return Mathf.CeilToInt((float)WeaponArcScenes.Count / pageSize);
    }

    public IEnumerable<(PackedScene scene, int index)> GetPageItems(int pageIndex, int pageSize)
    {
        if (pageSize <= 0 || pageIndex < 0)
            yield break;

        int start = pageIndex * pageSize;
        int end = Mathf.Min(start + pageSize, WeaponArcScenes.Count);
        for (int i = start; i < end; i++)
        {
            yield return (WeaponArcScenes[i], i);
        }
    }
}
