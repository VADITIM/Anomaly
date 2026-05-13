using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Autoloaded singleton that scans the scene for Node2Ds named "Elevation X"
/// (X = 0..15) and their TileMapLayer children named "EX Ground" / "EX Wall".
///
/// Responsibilities:
///   • Set the Godot Z-index of each Elevation node and its TileMapLayers
///     so they sort correctly in the 2D renderer  (Z = elevation * ELEVATION_HEIGHT).
///   • Provide spatial queries used by Entity / ZAxis:
///       - What elevation is this world-position on (grounded)?
///       - Is there a Wall at elevation N near a world-position?
///       - Is there Ground at elevation N near a world-position?
///       - Can an entity legally jump UP from elevation A → B?
///       - Can an entity legally step DOWN from elevation A → B?
/// </summary>
public partial class ElevationSystem : Node
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static ElevationSystem Instance { get; private set; }

    // ── Constants ────────────────────────────────────────────────────────────
    /// <summary>World-units (pixels) between consecutive elevation levels.</summary>
    public const float ELEVATION_HEIGHT = 32f;

    /// <summary>Maximum tracked elevation index (inclusive, 0-based).</summary>
    public const int MAX_ELEVATION = 15;

    /// <summary>
    /// Tile-search radius (in cells) used when checking adjacency between a
    /// Wall tile and a Ground tile on the neighbouring elevation.
    /// </summary>
    private const int ADJACENCY_RADIUS = 2;

    // ── Data ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Per elevation level, the two TileMapLayers (may be null if absent).
    /// Index = elevation number (0 … MAX_ELEVATION).
    /// </summary>
    private readonly TileMapLayer[] groundLayers = new TileMapLayer[MAX_ELEVATION + 1];
    private readonly TileMapLayer[] wallLayers   = new TileMapLayer[MAX_ELEVATION + 1];

    /// <summary>Cached occupied-cell sets so we never call GetUsedCells() at runtime.</summary>
    private readonly HashSet<Vector2I>[] groundCells = new HashSet<Vector2I>[MAX_ELEVATION + 1];
    private readonly HashSet<Vector2I>[] wallCells   = new HashSet<Vector2I>[MAX_ELEVATION + 1];

    // ═════════════════════════════════════════════════════════════════════════
    //  Godot lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        Instance = this;
        for (int i = 0; i <= MAX_ELEVATION; i++)
        {
            groundCells[i] = new HashSet<Vector2I>();
            wallCells[i]   = new HashSet<Vector2I>();
        }
        ScanElevationNodes();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Scene scanning
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Finds every Node2D whose name is exactly "Elevation X" (X = 0..MAX_ELEVATION),
    /// sets its ZIndex, then looks inside it for TileMapLayers named "EX Ground"
    /// and "EX Wall", caches their cell sets, and sets their ZIndex too.
    ///
    /// Call this once at startup (from _Ready) and again whenever the scene
    /// changes (e.g. after a room transition).
    /// </summary>
    public void ScanElevationNodes()
    {
        // Clear previous data.
        for (int i = 0; i <= MAX_ELEVATION; i++)
        {
            groundLayers[i] = null;
            wallLayers[i]   = null;
            groundCells[i].Clear();
            wallCells[i].Clear();
        }

        Node root = GetTree().CurrentScene ?? GetTree().Root;
        if (root == null)
        {
            GD.PrintErr("[ElevationSystem] No current scene found.");
            return;
        }

        // Walk the whole scene graph looking for "Elevation X" Node2Ds.
        GatherElevationNodes(root);
    }

    private void GatherElevationNodes(Node node)
    {
        // Check if this node matches "Elevation X".
        if (node is Node2D elevNode)
        {
            string nodeName = elevNode.Name.ToString();
            if (nodeName.StartsWith("Elevation "))
            {
                string numPart = nodeName.Substring("Elevation ".Length).Trim();
                if (int.TryParse(numPart, out int elev) && elev >= 0 && elev <= MAX_ELEVATION)
                {
                    RegisterElevationNode(elevNode, elev);
                    // Don't recurse further – children are handled inside RegisterElevationNode.
                    return;
                }
            }
        }

        foreach (Node child in node.GetChildren())
            GatherElevationNodes(child);
    }

    private void RegisterElevationNode(Node2D elevNode, int elev)
    {
        // Set the visual Z-index of the elevation container.
        // Using ZIndex (not ZAsRelative) so it's absolute in the renderer.
        elevNode.ZIndex = elev;          // Godot Z layers; tweak if you use a different scale.
        elevNode.ZAsRelative = false;

        GD.Print($"[ElevationSystem] Elevation {elev} node registered (ZIndex={elevNode.ZIndex}).");

        // Look at direct children for the Ground / Wall TileMapLayers.
        foreach (Node child in elevNode.GetChildren())
        {
            if (child is not TileMapLayer tml)
                continue;

            string tname = tml.Name.ToString();

            if (tname == $"E{elev} Ground")
            {
                tml.ZIndex = elev;
                tml.ZAsRelative = false;
                groundLayers[elev] = tml;
                CacheCells(tml, groundCells[elev]);
                GD.Print($"[ElevationSystem]   ↳ E{elev} Ground → {groundCells[elev].Count} cells");
            }
            else if (tname == $"E{elev} Wall")
            {
                tml.ZIndex = elev;
                tml.ZAsRelative = false;
                wallLayers[elev] = tml;
                CacheCells(tml, wallCells[elev]);
                GD.Print($"[ElevationSystem]   ↳ E{elev} Wall → {wallCells[elev].Count} cells");
            }
        }
    }

    private static void CacheCells(TileMapLayer layer, HashSet<Vector2I> outSet)
    {
        outSet.Clear();
        foreach (Vector2I cell in layer.GetUsedCells())
            outSet.Add(cell);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Public spatial queries
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the highest elevation whose Ground layer covers <paramref name="worldPos"/>.
    /// Returns 0 when no ground is found (treats the world floor as elevation 0).
    /// </summary>
    public int GetGroundedElevation(Vector2 worldPos)
    {
        for (int e = MAX_ELEVATION; e >= 0; e--)
        {
            if (groundLayers[e] != null && IsCellOccupied(groundLayers[e], groundCells[e], worldPos))
                return e;
        }
        return 0;
    }

    /// <summary>World-space floor height (pixels) for <paramref name="elevation"/>.</summary>
    public static float GetElevationFloorZ(int elevation)
        => Mathf.Clamp(elevation, 0, MAX_ELEVATION) * ELEVATION_HEIGHT;

    /// <summary>True if there is a Wall tile at <paramref name="elevation"/> near <paramref name="worldPos"/>.</summary>
    public bool HasWallAt(Vector2 worldPos, int elevation, int radius = 0)
    {
        if (elevation < 0 || elevation > MAX_ELEVATION || wallLayers[elevation] == null)
            return false;
        return radius == 0
            ? IsCellOccupied(wallLayers[elevation], wallCells[elevation], worldPos)
            : HasCellNear(wallLayers[elevation], wallCells[elevation], worldPos, radius);
    }

    /// <summary>True if there is a Ground tile at <paramref name="elevation"/> near <paramref name="worldPos"/>.</summary>
    public bool HasGroundAt(Vector2 worldPos, int elevation, int radius = 0)
    {
        if (elevation < 0 || elevation > MAX_ELEVATION || groundLayers[elevation] == null)
            return false;
        return radius == 0
            ? IsCellOccupied(groundLayers[elevation], groundCells[elevation], worldPos)
            : HasCellNear(groundLayers[elevation], groundCells[elevation], worldPos, radius);
    }

    // ── Transition validation ─────────────────────────────────────────────

    /// <summary>
    /// Checks whether it is geometrically valid for an entity to jump UP
    /// from <paramref name="fromElev"/> to <paramref name="toElev"/>.
    ///
    /// Rule: there must be an E{fromElev} Wall tile AND an E{toElev} Ground tile
    /// within ADJACENCY_RADIUS cells of <paramref name="worldPos"/>.
    /// </summary>
    public bool CanJumpUp(Vector2 worldPos, int fromElev, int toElev)
    {
        if (toElev != fromElev + 1)
            return false;           // Only single-step jumps are supported.
        if (fromElev < 0 || toElev > MAX_ELEVATION)
            return false;

        bool wallPresent   = HasWallAt(worldPos, fromElev, ADJACENCY_RADIUS);
        bool groundPresent = HasGroundAt(worldPos, toElev,  ADJACENCY_RADIUS);

        return wallPresent && groundPresent;
    }

    /// <summary>
    /// Checks whether it is geometrically valid for an entity to step DOWN
    /// from <paramref name="fromElev"/> to <paramref name="toElev"/> (no jump needed).
    ///
    /// Rule: there must be an E{toElev} Wall tile AND an E{toElev} Ground tile
    /// within ADJACENCY_RADIUS cells of <paramref name="worldPos"/>.
    /// (The entity walks off the ledge – the wall of the lower level and its
    /// ground must be adjacent to the current position.)
    /// </summary>
    public bool CanStepDown(Vector2 worldPos, int fromElev, int toElev)
    {
        if (toElev != fromElev - 1)
            return false;
        if (toElev < 0 || fromElev > MAX_ELEVATION)
            return false;

        bool wallPresent   = HasWallAt(worldPos, toElev, ADJACENCY_RADIUS);
        bool groundPresent = HasGroundAt(worldPos, toElev, ADJACENCY_RADIUS);

        return wallPresent && groundPresent;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════════════════════════════════

    private static bool IsCellOccupied(TileMapLayer layer, HashSet<Vector2I> cells, Vector2 worldPos)
    {
        Vector2I cell = WorldToCell(layer, worldPos);
        return cells.Contains(cell);
    }

    private static bool HasCellNear(TileMapLayer layer, HashSet<Vector2I> cells, Vector2 worldPos, int radius)
    {
        Vector2I center = WorldToCell(layer, worldPos);
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
            if (cells.Contains(new Vector2I(center.X + dx, center.Y + dy)))
                return true;
        return false;
    }

    private static Vector2I WorldToCell(TileMapLayer layer, Vector2 worldPos)
    {
        Vector2 local = layer.ToLocal(worldPos);
        return layer.LocalToMap(local);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Back-compat aliases (old method names used elsewhere in the project)
    // ═════════════════════════════════════════════════════════════════════════

    /// <inheritdoc cref="HasWallAt"/>
    public bool HasWallAtPosition(Vector2 worldPos, int elevation, int radius = 0)
        => HasWallAt(worldPos, elevation, radius);

    /// <inheritdoc cref="HasGroundAt"/>
    public bool HasGroundAtPosition(Vector2 worldPos, int elevation, int radius = 0)
        => HasGroundAt(worldPos, elevation, radius);

    /// <inheritdoc cref="GetElevationFloorZ"/>
    public float GetElevationHeightForLevel(int elevation)
        => GetElevationFloorZ(elevation);

    /// <inheritdoc cref="GetGroundedElevation"/>
    public int GetPlayerElevation(Vector2 worldPos)
        => GetGroundedElevation(worldPos);

    /// <summary>
    /// Old helper — kept for call-sites that haven't migrated to CanJumpUp yet.
    /// Checks whether there is a Wall at <paramref name="currentElevation"/> AND
    /// Ground at <paramref name="currentElevation"/>+1 near <paramref name="worldPos"/>.
    /// </summary>
    public bool CanJumpUpFromElevation(Vector2 worldPos, int currentElevation)
        => CanJumpUp(worldPos, currentElevation, currentElevation + 1);
}