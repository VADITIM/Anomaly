using System.Collections.Generic;
using Godot;

namespace Anomaly;

// The world is cut into authored sections (see LevelBlueprint.Section): a room, a long road, a crossing. Each one is its
// own node under Actors/<region>/<section> holding its ground, raised terrain and scenery. Kio's section is live together
// with every neighbour it touches, so walking across a seam always lands in ground that is already there; anything else
// the camera can see (a scouting pan, a long view) is live too. The rest sit hidden and asleep.
// Every tile that draws anything belongs to exactly one section: the one whose area holds it, or the nearest one for the
// walls and rims just outside every area.
// Waking and sleeping never happen in one go. A section starts loading while Kio is still a few tiles short of the
// sections next to it, the work is spread over frames under a small time budget, and a section Kio left lingers a few
// seconds before it sleeps, so walking back and forth over a seam costs nothing. Sleeping hides a section on the
// rendering server only: Godot redraws every canvas item that becomes visible, so toggling Visible would replay the
// whole floor and every terrain row in the frame a section wakes. Hidden this way, their drawing is kept and waking is
// close to free.
public sealed partial class WorldSections : Node
{
    // Generous enough for a tree canopy, a raised terrain top or a light whose anchor sits just off screen.
    private const float ACTIVE_MARGIN = 160f;
    private const float REFRESH_INTERVAL = 0.1f;
    // Kio this close to a section loads that section's neighbours too, ahead of the crossing.
    private const float PREFETCH_REACH = 160f;
    // How long a section stays live after it stops being needed.
    private const float LINGER_SECONDS = 4f;
    // Loading work per frame. At least one step always runs, so a long queue still drains.
    private const ulong FRAME_BUDGET_USEC = 700;
    // Tiles further than this from every section area draw nothing worth owning.
    private const int RIM_REACH = 6;
    // Sections whose areas come this close are neighbours: a seam, or a short gap a jump or a bridge tile crosses.
    private const float NEIGHBOUR_GAP = 64f;

    private enum PartKind { Show, Light, Process }

    private readonly record struct Part(PartKind Kind, CanvasItem Item);

    private readonly List<SectionPlan> plans;
    private readonly int[,] owner;
    private readonly Rect2I[] ownedTiles;
    private readonly Node2D[] nodes;
    private readonly List<CanvasItem>[] extras;
    private readonly bool[] active;
    private readonly float[] lastNeeded;
    private readonly List<int>[] neighbours;
    // What switching a section touches, most visible first: shows, then lights, then processing.
    private readonly List<Part>[] parts;
    // How many of a section's parts already match its state; sections short of that wait in pending.
    private readonly int[] applied;
    private readonly List<int> pending = new();
    private GameWorld world;
    private float refreshTimer;
    private float clock;

    public int Count => plans.Count;

    // Parts applied in the last frame; for profiling.
    public int StepsLastFrame { get; private set; }

    public WorldSections(WorldGrid grid, IReadOnlyList<SectionPlan> sectionPlans)
    {
        plans = new List<SectionPlan>(sectionPlans);
        owner = new int[grid.Width, grid.Height];
        ownedTiles = new Rect2I[Count];
        nodes = new Node2D[Count];
        extras = new List<CanvasItem>[Count];
        active = new bool[Count];
        lastNeeded = new float[Count];
        parts = new List<Part>[Count];
        applied = new int[Count];
        for (int i = 0; i < Count; i++)
        {
            extras[i] = new List<CanvasItem>();
            parts[i] = new List<Part>();
            active[i] = true;
            lastNeeded[i] = float.MinValue;
        }
        AssignTiles(grid);
        neighbours = new List<int>[Count];
        for (int i = 0; i < Count; i++)
        {
            neighbours[i] = new List<int>();
            for (int j = 0; j < Count; j++)
                if (j != i && AreaOf(i).Grow(NEIGHBOUR_GAP).Intersects(AreaOf(j)))
                    neighbours[i].Add(j);
        }
    }

    public IReadOnlyList<int> NeighboursOf(int section) => neighbours[section];

    // Kio's section, or the nearest one while Kio stands in a gap between them.
    public int Current { get; private set; } = -1;

    public WorldSections() : this(new WorldGrid(1, 1), new[] { new SectionPlan("World", "All", new Rect2I(0, 0, 1, 1)) }) { }

    public void Setup(GameWorld gameWorld) => world = gameWorld;

    public SectionPlan PlanOf(int section) => plans[section];

    // The authored area in world pixels.
    public Rect2 AreaOf(int section) => TileRect(plans[section].Area);

    // Every tile this section draws, rims included.
    public Rect2I OwnedTilesOf(int section) => ownedTiles[section];

    public Node2D NodeOf(int section) => nodes[section];

    // Whether this section is live, or on its way there.
    public bool IsLive(int section) => active[section];

    public bool Owns(int section, int x, int y) =>
        x >= 0 && y >= 0 && x < owner.GetLength(0) && y < owner.GetLength(1) && owner[x, y] == section;

    // The section whose area holds this point, or -1 in the gaps between them.
    public int SectionAt(Vector2 position)
    {
        Vector2I tile = WorldGrid.ToTile(position);
        for (int i = 0; i < Count; i++)
            if (plans[i].Area.HasPoint(tile))
                return i;
        return -1;
    }

    // Like SectionAt, but a point in a gap falls to the nearest section.
    public int NearestSection(Vector2 position)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            float distance = DistanceTo(AreaOf(i), position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    // One node per region and per section, nested in the actor Y-sort so everything inside still sorts against Kio.
    public void BuildNodes(Node2D actors)
    {
        var regions = new Dictionary<string, Node2D>();
        for (int i = 0; i < Count; i++)
        {
            SectionPlan plan = plans[i];
            if (!regions.TryGetValue(plan.Region, out Node2D region))
            {
                region = new Node2D { Name = plan.Region, YSortEnabled = true };
                actors.AddChild(region);
                regions[plan.Region] = region;
            }
            nodes[i] = new Node2D { Name = plan.Name, YSortEnabled = true };
            region.AddChild(nodes[i]);
        }
    }

    // Static scenery moves into the section that holds it; scenery outside the actor sort only follows its section's
    // state. Entities that move stream on their own.
    public void RegisterScenery(Node parent)
    {
        bool reparent = parent == world.Actors;
        foreach (Node child in parent.GetChildren())
        {
            if (child is Enemy or Player or VoidHeart || child is not Node2D node || child is TerrainLayer)
                continue;
            if (node is not (Decoration or WorldLight or CpuParticles2D or Prop or Forge or Bonfire or ShortcutGate or Fracture))
                continue;
            int section = OwnerAt(node.GlobalPosition);
            if (reparent)
                node.Reparent(nodes[section]);
            else
                extras[section].Add(node);
        }
    }

    // Lists what switching each section touches. Runs once the world is built, before the first Refresh.
    public void IndexParts()
    {
        for (int i = 0; i < Count; i++)
        {
            List<Part> list = parts[i];
            list.Clear();
            list.Add(new Part(PartKind.Show, nodes[i]));
            foreach (CanvasItem extra in extras[i])
                list.Add(new Part(PartKind.Show, extra));
            CollectLights(nodes[i], list);
            foreach (CanvasItem extra in extras[i])
                CollectLights(extra, list);
            // Floors and terrain rows only draw; scenery may animate, emit or hold a body.
            foreach (Node child in nodes[i].GetChildren())
                if (child is CanvasItem item and not (FloorCanvas or TerrainRow))
                    list.Add(new Part(PartKind.Process, item));
            foreach (CanvasItem extra in extras[i])
                list.Add(new Part(PartKind.Process, extra));
            // Everything starts on as built; whatever is not needed sleeps from the first Refresh.
            applied[i] = list.Count;
        }
    }

    private static void CollectLights(Node node, List<Part> list)
    {
        if (node is Light2D light)
            list.Add(new Part(PartKind.Light, light));
        foreach (Node child in node.GetChildren())
            CollectLights(child, list);
    }

    public override void _Process(double delta)
    {
        clock += (float)delta;
        Drain(FRAME_BUDGET_USEC);
        refreshTimer -= (float)delta;
        if (refreshTimer > 0f)
            return;
        refreshTimer = REFRESH_INTERVAL;
        Refresh();
    }

    // Works out which sections should be live and queues the switches. Immediate (a teleport or a respawn) applies them
    // at once and drops what is left behind, since the screen cuts anyway.
    public void Refresh(bool immediate = false)
    {
        if (world?.Camera == null)
            return;
        Rect2 view = world.Camera.VisibleWorldRect().Grow(ACTIVE_MARGIN);
        Vector2 ground = world.Player.GlobalPosition;
        int here = SectionAt(ground);
        Current = here >= 0 ? here : NearestSection(ground);
        for (int i = 0; i < Count; i++)
        {
            if (TileRect(ownedTiles[i]).Intersects(view))
                lastNeeded[i] = clock;
            if (i != Current && DistanceTo(AreaOf(i), ground) > PREFETCH_REACH)
                continue;
            lastNeeded[i] = clock;
            foreach (int neighbour in neighbours[i])
                lastNeeded[neighbour] = clock;
        }
        float linger = immediate ? 0f : LINGER_SECONDS;
        for (int i = 0; i < Count; i++)
        {
            bool on = clock - lastNeeded[i] <= linger;
            if (on == active[i])
                continue;
            active[i] = on;
            // Parts switch in order and each switch can be repeated, so a section that flips back simply starts over.
            applied[i] = 0;
            if (!pending.Contains(i))
                pending.Add(i);
        }
        if (immediate)
            Drain(ulong.MaxValue);
    }

    // Applies parts until the frame budget runs out: the waking section nearest Kio first, sleeping ones only once
    // nothing waits to wake. Sleeping walks the parts backwards, so a section stops working before it disappears.
    private void Drain(ulong budgetUsec)
    {
        ulong start = Time.GetTicksUsec();
        bool first = true;
        StepsLastFrame = 0;
        while (pending.Count > 0 && (first || Time.GetTicksUsec() - start < budgetUsec))
        {
            first = false;
            int section = NextPending();
            List<Part> list = parts[section];
            if (applied[section] >= list.Count)
            {
                pending.Remove(section);
                continue;
            }
            bool on = active[section];
            Part part = list[on ? applied[section] : list.Count - 1 - applied[section]];
            applied[section]++;
            StepsLastFrame++;
            if (IsInstanceValid(part.Item))
                Apply(part, on);
        }
    }

    private int NextPending()
    {
        Vector2 ground = world.Player.GlobalPosition;
        int best = pending[0];
        float bestScore = float.MaxValue;
        foreach (int section in pending)
        {
            float score = DistanceTo(AreaOf(section), ground) + (active[section] ? 0f : 1_000_000f);
            if (score < bestScore)
            {
                bestScore = score;
                best = section;
            }
        }
        return best;
    }

    private int OwnerAt(Vector2 position)
    {
        Vector2I tile = WorldGrid.ToTile(position);
        if (tile.X >= 0 && tile.Y >= 0 && tile.X < owner.GetLength(0) && tile.Y < owner.GetLength(1) && owner[tile.X, tile.Y] >= 0)
            return owner[tile.X, tile.Y];
        return NearestSection(position);
    }

    private void AssignTiles(WorldGrid grid)
    {
        var first = new bool[Count];
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                owner[x, y] = -1;
                if (grid.HeightAt(x, y) < 0)
                    continue;
                int section = NearestTileSection(new Vector2I(x, y));
                if (section < 0)
                    continue;
                owner[x, y] = section;
                var tile = new Rect2I(x, y, 1, 1);
                ownedTiles[section] = first[section] ? ownedTiles[section].Merge(tile) : tile;
                first[section] = true;
            }
        }
    }

    private int NearestTileSection(Vector2I tile)
    {
        int best = -1;
        int bestDistance = RIM_REACH + 1;
        for (int i = 0; i < Count; i++)
        {
            Rect2I area = plans[i].Area;
            int dx = Mathf.Max(Mathf.Max(area.Position.X - tile.X, tile.X - (area.End.X - 1)), 0);
            int dy = Mathf.Max(Mathf.Max(area.Position.Y - tile.Y, tile.Y - (area.End.Y - 1)), 0);
            int distance = Mathf.Max(dx, dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
                if (distance == 0)
                    break;
            }
        }
        return best;
    }

    private static Rect2 TileRect(Rect2I tiles) => new(tiles.Position * WorldGrid.TILE_SIZE, tiles.Size * WorldGrid.TILE_SIZE);

    private static float DistanceTo(Rect2 rect, Vector2 point)
    {
        float dx = Mathf.Max(Mathf.Max(rect.Position.X - point.X, point.X - rect.End.X), 0f);
        float dy = Mathf.Max(Mathf.Max(rect.Position.Y - point.Y, point.Y - rect.End.Y), 0f);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static void Apply(Part part, bool on)
    {
        switch (part.Kind)
        {
            case PartKind.Show:
                RenderingServer.CanvasItemSetVisible(part.Item.GetCanvasItem(), on);
                break;
            case PartKind.Light:
                ((Light2D)part.Item).Enabled = on;
                break;
            case PartKind.Process:
                part.Item.ProcessMode = on ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
                break;
        }
    }
}
