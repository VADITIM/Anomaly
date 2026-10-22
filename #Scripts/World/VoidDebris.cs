using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Torn-off scraps of Verdanya drifting in the Void beneath the islands. Each fragment sits at its own depth:
// deeper ones are smaller, darker and follow the camera more, which gives the abyss parallax without a second camera.
public partial class VoidDebris : Node2D
{
    private const int FRAGMENT_COUNT = 54;
    private const int Z_INDEX = -90;

    private readonly List<VoidFragment> fragments = new();

    public void Build(WorldGrid grid, Rect2 bounds)
    {
        ZIndex = Z_INDEX;
        var random = new RandomNumberGenerator { Seed = 7331 };
        var pending = new List<VoidFragment>();
        int attempts = 0;
        while (pending.Count < FRAGMENT_COUNT && attempts++ < 4000)
        {
            Vector2 anchor = new(random.RandfRange(bounds.Position.X, bounds.End.X), random.RandfRange(bounds.Position.Y, bounds.End.Y));
            Vector2I tile = WorldGrid.ToTile(anchor);
            if (!grid.IsPit(tile.X, tile.Y) || NearGround(grid, tile, 1))
                continue;
            var fragment = new VoidFragment();
            fragment.Setup(random, anchor);
            pending.Add(fragment);
        }
        pending.Sort((a, b) => b.Depth.CompareTo(a.Depth));
        foreach (VoidFragment fragment in pending)
        {
            fragments.Add(fragment);
            AddChild(fragment);
        }
    }

    private static bool NearGround(WorldGrid grid, Vector2I tile, int radius)
    {
        for (int dy = -radius; dy <= radius + 1; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (!grid.IsPit(tile.X + dx, tile.Y + dy))
                    return true;
        return false;
    }

    public override void _Process(double delta)
    {
        Camera2D camera = GetViewport().GetCamera2D();
        if (camera == null)
            return;
        Vector2 center = camera.GetScreenCenterPosition();
        float time = Time.GetTicksMsec() * 0.001f;
        foreach (VoidFragment fragment in fragments)
            fragment.Follow(center, time);
    }
}
