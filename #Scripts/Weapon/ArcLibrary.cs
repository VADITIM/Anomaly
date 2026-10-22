using System.Collections.Generic;
using Godot;

namespace Anomaly;

public static class ArcLibrary
{
    public const string DEFAULT_ARC = "soul_scythe_arc";
    public static readonly string[] ALL_ARCS = { "soul_scythe_arc", "soul_hammer_arc", "soul_spear_arc" };

    private static readonly Dictionary<string, SoulWeaponArc> cache = new();

    public static SoulWeaponArc Get(string arcId)
    {
        if (cache.TryGetValue(arcId, out SoulWeaponArc arc))
            return arc;
        string path = $"res://#Resources/Arcs/{arcId}.tres";
        arc = ResourceLoader.Exists(path) ? GD.Load<SoulWeaponArc>(path) : GD.Load<SoulWeaponArc>($"res://#Resources/Arcs/{DEFAULT_ARC}.tres");
        cache[arcId] = arc;
        return arc;
    }
}
