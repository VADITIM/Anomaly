using Godot;

/// <summary>
/// Resolves which elevation plane a world position belongs to and keeps entities in sync with it.
/// The world's ground TileMapLayers are the authority: a cell in "E3 Ground" means elevation 3
/// exists at that cell. Walls ("E3 Wall") carry the matching "Elevation 3" physics layer and do
/// the edge blocking through <see cref="ElevationMath.ApplyWallMask"/> — see docs/design.md §3.2.
/// </summary>
public static class ElevationSystem
{
    public const float NoGround = -1f;

    private const string GroundLayerSuffix = " Ground";
    private const string WallLayerSuffix = " Wall";

    // "E{n} Wall" holds the face that stands on plane n and rises to plane n+1 — it is painted on the
    // upper plateau's own cells, so it must draw over that plateau's ground. Draw order interleaves:
    // E0 Ground, E1 Ground, E0 Wall, E2 Ground, E1 Wall, ... Layer z-indices are relative to their
    // "Elevation N" root, which carries the ground band.
    private const int WallZOffset = 3;

    // Far enough ahead to read the cliff the entity is about to cross, short enough not to read the
    // plateau one tile past a ledge it is dropping off. Tiles are 32 px.
    private const float WallProbeDistance = 20f;

    private static readonly TileMapLayer[] GroundLayers = new TileMapLayer[ElevationMath.MaxElevation + 1];
    private static readonly Node2D[] ElevationRoots = new Node2D[ElevationMath.MaxElevation + 1];
    private static Node cachedScene;

    public static void Update(Entity entity)
    {
        if (entity == null || !entity.IsInsideTree())
            return;

        EnsureLayers(entity);

        float surface = GetSurfaceElevation(entity.GlobalPosition, entity.Elevation);

        if (entity.IsJumping)
        {
            if (entity.IsFalling && surface != NoGround && entity.Elevation <= surface)
                entity.LandOnElevation(surface);
        }
        else if (surface != NoGround && surface < entity.GroundElevation)
        {
            entity.BeginFall(surface);
        }
        else if (surface != NoGround && surface > entity.GroundElevation)
        {
            // Ground rose under a standing entity (a ramp tile, or a spawn placed over higher ground).
            entity.SetGroundElevation(surface);
        }

        entity.CollisionMask = ElevationMath.ResolveMask(entity.BaseCollisionMask, entity.Elevation, CanPassWalls(entity));

        SyncElevationParent(entity);
    }

    /// <summary>
    /// Walls only open up for an airborne entity heading somewhere it can actually land. Deciding by
    /// wall layer alone is not enough: the lower half of a two-plane cliff is authored as "E1 Wall",
    /// so a one-plane jumper would clear it and end up inside the cliff. Probing the real surface
    /// ahead — uncapped, so the full plateau height is seen — keeps that wall solid.
    /// </summary>
    private static bool CanPassWalls(Entity entity)
    {
        if (!entity.IsJumping)
            return false;

        Vector2 heading = entity.Velocity.Normalized();
        Vector2 probePosition = entity.GlobalPosition + heading * WallProbeDistance;
        float surfaceAhead = GetSurfaceElevation(probePosition, ElevationMath.MaxElevation);

        if (surfaceAhead == NoGround)
            return true;

        return ElevationMath.CanClear(surfaceAhead, entity.GroundElevation, entity.MaxJumpElevations);
    }

    /// <summary>
    /// Entities live under the "Elevation N" node of the plane they stand on: the plane's z-band puts
    /// it in front of lower planes, and the root's Y-sort orders everything within the plane against
    /// the tiles themselves. Reparenting is deferred — it must not happen mid-physics.
    /// </summary>
    private static void SyncElevationParent(Entity entity)
    {
        int plane = Mathf.FloorToInt(entity.GroundElevation);
        if (plane < 0 || plane > ElevationMath.MaxElevation)
            return;

        Node2D root = ElevationRoots[plane];
        if (root == null || !GodotObject.IsInstanceValid(root) || entity.GetParent() == root)
            return;

        entity.CallDeferred(Node.MethodName.Reparent, root, true);
    }

    /// <summary>Highest plane with ground at this position that is not above <paramref name="ceiling"/>.</summary>
    public static float GetSurfaceElevation(Vector2 globalPosition, float ceiling)
    {
        for (int elevation = ElevationMath.MaxElevation; elevation >= 0; elevation--)
        {
            if (elevation > ceiling)
                continue;

            TileMapLayer layer = GroundLayers[elevation];
            if (layer == null || !GodotObject.IsInstanceValid(layer))
                continue;

            Vector2I cell = layer.LocalToMap(layer.ToLocal(globalPosition));
            if (layer.GetCellSourceId(cell) != -1)
                return elevation;
        }

        return NoGround;
    }

    private static void EnsureLayers(Node context)
    {
        Node scene = context.GetTree()?.CurrentScene;
        if (scene == null || (scene == cachedScene && GodotObject.IsInstanceValid(scene)))
            return;

        cachedScene = scene;
        System.Array.Clear(GroundLayers, 0, GroundLayers.Length);
        System.Array.Clear(ElevationRoots, 0, ElevationRoots.Length);
        CollectLayers(scene);

        if (GroundLayers[0] == null)
            GD.PushError($"ElevationSystem: no 'E0{GroundLayerSuffix}' TileMapLayer in '{scene.Name}'. Every world needs one ground layer per elevation.");
    }

    private static void CollectLayers(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is TileMapLayer layer)
            {
                if (TryParseElevation(layer.Name, GroundLayerSuffix, out int groundElevation))
                {
                    GroundLayers[groundElevation] = layer;
                    layer.ZIndex = 0;
                    RegisterElevationRoot(layer.GetParent() as Node2D, groundElevation);
                }
                else if (TryParseElevation(layer.Name, WallLayerSuffix, out int wallElevation))
                {
                    layer.ZIndex = WallZOffset;
                    SeparateWallCollisionLayer(layer, wallElevation);
                }
            }

            CollectLayers(child);
        }
    }

    /// <summary>
    /// Puts the plane on its own z-band so scene tree order between the "Elevation N" nodes stops
    /// mattering — a higher plane always draws over a lower one, and each plane's wall draws over the
    /// plateau it supports.
    /// </summary>
    // NOTE: Y-sort is deliberately NOT enabled here. Turning it on makes Godot sort tiles
    // individually, which requires a y_sort_origin authored per tile in the TileSet; without that
    // every tile sorts against every entity and the world falls apart.
    private static void RegisterElevationRoot(Node2D root, int elevation)
    {
        if (root == null)
            return;

        ElevationRoots[elevation] = root;
        root.ZIndex = GroundZIndex(elevation);
    }

    // Grounds step by 2 so each wall can slot between its own plateau and the next one up.
    private static int GroundZIndex(int elevation) => (elevation * 2) - 1;

    /// <summary>
    /// Every wall TileMapLayer shares one authored TileSet, so all elevations would collide on the
    /// same physics layer. Duplicating it per layer at runtime puts each wall on its own physics
    /// layer — the same shared-subresource duplication rule as per-instance materials.
    /// </summary>
    // "E{n} Wall" is what a body standing on plane n runs into, and what holds up plane n+1, so it
    // carries the "Elevation n+1" layer: it blocks anyone below it and anyone standing on the
    // plateau's edge, and opens only for a jump that reaches n+1.
    private static void SeparateWallCollisionLayer(TileMapLayer layer, int elevation)
    {
        int blockedPlane = elevation + 1;
        if (layer.TileSet == null || layer.TileSet.GetPhysicsLayersCount() == 0 || blockedPlane > ElevationMath.MaxElevation)
            return;

        TileSet perElevation = (TileSet)layer.TileSet.Duplicate();
        perElevation.SetPhysicsLayerCollisionLayer(0, ElevationMath.ElevationBit(blockedPlane));
        layer.TileSet = perElevation;
    }

    private static bool TryParseElevation(string layerName, string suffix, out int elevation)
    {
        elevation = 0;

        if (!layerName.StartsWith("E") || !layerName.EndsWith(suffix))
            return false;

        string digits = layerName.Substring(1, layerName.Length - 1 - suffix.Length);
        return int.TryParse(digits, out elevation) && elevation >= 0 && elevation <= ElevationMath.MaxElevation;
    }
}
