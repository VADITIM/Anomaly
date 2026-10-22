using Godot;

namespace Anomaly;

// Full-screen map on Tab: the whole of the slice at 2px per tile, region names where Kio has been, and the markers worth returning to.
public partial class WorldMap : CanvasLayer
{
    // Largest scale that fits the whole world under the title; the world outgrew a fixed 2px per tile.
    private static float SCALE = 2f;
    private const float PAD = 5f;

    private GameWorld world;
    private MapData map;
    private MapCanvas canvas;
    private Control dim;
    private Label hint;

    public bool IsOpen => Visible;

    public void Setup(GameWorld gameWorld, MapData mapData)
    {
        world = gameWorld;
        map = mapData;
        SCALE = Mathf.Min(2f, Mathf.Min((ViewScale.DESIGN_SIZE.X - 40f) / map.Width, (ViewScale.DESIGN_SIZE.Y - 60f) / map.Height));
    }

    public override void _Ready()
    {
        Layer = 28;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        Visible = false;

        dim = new ColorRect { Color = new Color(0.02f, 0.0f, 0.03f, 0.85f), MouseFilter = Control.MouseFilterEnum.Stop, Position = new Vector2(-2000, -2000), Size = new Vector2(4480, 4270) };
        AddChild(dim);

        Vector2 mapSize = new Vector2(map.Width, map.Height) * SCALE;
        Vector2 framePosition = new((ViewScale.DESIGN_SIZE.X - mapSize.X) * 0.5f - PAD, 26f);
        var panel = new Panel { Position = framePosition, Size = mapSize + Vector2.One * PAD * 2f, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiFactory.Frame());
        AddChild(panel);

        canvas = new MapCanvas { Position = framePosition + Vector2.One * PAD, Size = mapSize, MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.Setup(world, map);
        AddChild(canvas);

        Label title = UiFactory.Label("THE FRACTURED REACH", Art.TitleFont, 12, UiFactory.TextColor, 3);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Position = new Vector2(0, 5);
        title.Size = new Vector2(ViewScale.DESIGN_SIZE.X, 12);
        AddChild(title);
        AddChild(new Divider { Position = new Vector2(ViewScale.DESIGN_SIZE.X * 0.5f - 70f, 17f), Size = new Vector2(140, 5) });

        hint = UiFactory.Label("", Art.BodyFont, 6, UiFactory.DimText);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.Position = new Vector2(0, framePosition.Y + mapSize.Y + PAD * 2f + 3f);
        hint.Size = new Vector2(ViewScale.DESIGN_SIZE.X, 12);
        AddChild(hint);
    }

    public void Open()
    {
        map.Reveal(WorldGrid.ToTile(world.Player.GlobalPosition));
        map.Flush();
        hint.Text = $"◆ Maldor   ◆ Portal   ◆ Void Heart   ○ Fracture   ▲ You      Click to teleport     {GameInput.Describe(GameInput.MAP)} / ESC close";
        canvas.Refresh();
        Visible = true;
        world.GetTree().Paused = true;
        Sfx.Play("ui_confirm", -6f, 0.9f);
    }

    public void Close()
    {
        Visible = false;
        world.GetTree().Paused = false;
        Sfx.Play("ui_move", -6f, 0.7f);
    }

    public override void _Process(double delta)
    {
        if (Visible)
            canvas.QueueRedraw();
    }

    // Click anywhere on the map to jump there; the nearest standable tile is used when the spot is a pit or blocked.
    public override void _Input(InputEvent inputEvent)
    {
        if (!Visible || inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;
        Vector2 local = canvas.GetLocalMousePosition() / SCALE;
        var target = new Vector2I(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
        if (!world.Grid.InBounds(target.X, target.Y))
            return;
        GetViewport().SetInputAsHandled();
        TeleportTo(target);
    }

    public void TeleportTo(Vector2I target)
    {
        if (!TryFindStandable(target, out Vector2 position))
            return;
        Close();
        world.Player.PlaceAt(position);
        map.Reveal(WorldGrid.ToTile(position));
        Sfx.Play("level_up", -8f, 1.3f);
    }

    private bool TryFindStandable(Vector2I from, out Vector2 position)
    {
        WorldGrid grid = world.Grid;
        for (int radius = 0; radius <= 8; radius++)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius)
                        continue;
                    int x = from.X + dx, y = from.Y + dy;
                    if (!grid.InBounds(x, y) || grid.IsPit(x, y))
                        continue;
                    Vector2 candidate = WorldGrid.TileCenter(x, y);
                    float z = grid.SurfaceZAt(candidate);
                    if (float.IsNegativeInfinity(z) || grid.Blocks(candidate, 8f, z) || grid.BlockedByObstacle(candidate, 8f, z))
                        continue;
                    position = candidate;
                    return true;
                }
        position = default;
        return false;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible)
            return;
        if (inputEvent.IsActionPressed(GameInput.MAP) || inputEvent.IsActionPressed(GameInput.PAUSE) || inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    private partial class MapCanvas : Control
    {
        private GameWorld world;
        private MapData map;
        private readonly System.Collections.Generic.List<Label> labels = new();

        public void Setup(GameWorld gameWorld, MapData mapData)
        {
            world = gameWorld;
            map = mapData;
        }

        public void Refresh()
        {
            foreach (Label label in labels)
                label.QueueFree();
            labels.Clear();
            foreach (MapRegion region in map.Blueprint.Regions)
            {
                bool known = map.RegionExplored(region.Area);
                Label label = UiFactory.Label(known ? region.Name.ToUpper() : "UNCHARTED", Art.BodyFont, 6, known ? BiomeArt.LabelColor(region.Biome) : UiFactory.DimText);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                Vector2 center = (new Vector2(region.Area.GetCenter().X, region.Area.GetCenter().Y)) * SCALE;
                label.Size = new Vector2(100, 12);
                label.Position = center - new Vector2(50, 6);
                AddChild(label);
                labels.Add(label);
            }
        }

        public override void _Draw()
        {
            if (world?.Player == null)
                return;
            DrawTextureRect(map.Texture, new Rect2(Vector2.Zero, new Vector2(map.Width, map.Height) * SCALE), false);
            float pulse = Mathf.Sin(Time.GetTicksMsec() * 0.005f) * 0.5f + 0.5f;
            foreach (MapMarker marker in map.Markers)
            {
                Vector2 position = (new Vector2(marker.Tile.X, marker.Tile.Y) + new Vector2(0.5f, 0.5f)) * SCALE;
                MapIcons.DrawMarker(this, marker.Kind, position, 4.5f, pulse);
            }

            Vector2 player = world.Player.GlobalPosition / WorldGrid.TILE_SIZE * SCALE;
            DrawArc(player, 5f + pulse * 3f, 0f, Mathf.Tau, 20, new Color(0.75f, 1f, 0.95f, 0.8f - pulse * 0.6f), 1f);
            MapIcons.DrawPlayer(this, player, world.Player.AimDirection, 4f);
        }
    }
}
