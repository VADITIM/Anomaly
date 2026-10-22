using Godot;

namespace Anomaly;

// Top-right radar: a scrolling window of the shared map centred on Kio, with enemies, anvils and the Heart on top.
public partial class MiniMap : CanvasLayer
{
    private const float VIEW_SIZE = 78f;
    private const float SCALE = 2f;
    private const float MARGIN = 8f;
    private const float REVEAL_INTERVAL = 0.2f;

    private static readonly Color EnemyColor = new(1f, 0.3f, 0.35f);

    private GameWorld world;
    private MapData map;
    private MapView view;
    private const float FADE_TIME = 0.35f;

    private Control frame;
    private Label regionLabel;
    private bool menuFocus;
    private float fade = 1f;
    private float revealTimer;

    public void Setup(GameWorld gameWorld, MapData mapData)
    {
        world = gameWorld;
        map = mapData;
    }

    public override void _Ready()
    {
        Layer = 19;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        WorldSignals.MenuFocusChanged += OnMenuFocusChanged;

        frame = new Control
        {
            Position = new Vector2(ViewScale.DESIGN_SIZE.X - MARGIN - VIEW_SIZE - 4f, MARGIN),
            Size = new Vector2(VIEW_SIZE + 4f, VIEW_SIZE + 4f + 11f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(frame);

        view = new MapView { Position = new Vector2(2, 2), Size = new Vector2(VIEW_SIZE, VIEW_SIZE), ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        view.Setup(world, map);
        frame.AddChild(view);

        // The plate rides over the map, so its inner line and corners frame the scrolling view instead of hiding under it.
        var panel = new Panel { Position = Vector2.Zero, Size = new Vector2(VIEW_SIZE + 4f, VIEW_SIZE + 4f), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new FrameStyle { Fill = Colors.Transparent });
        frame.AddChild(panel);

        regionLabel = UiFactory.Label("", Art.BodyFont, 6, UiFactory.DimText);
        regionLabel.Position = new Vector2(0, VIEW_SIZE + 6f);
        regionLabel.Size = new Vector2(VIEW_SIZE + 4f, 8f);
        regionLabel.HorizontalAlignment = HorizontalAlignment.Right;
        frame.AddChild(regionLabel);
    }

    public override void _ExitTree() => WorldSignals.MenuFocusChanged -= OnMenuFocusChanged;

    private void OnMenuFocusChanged(bool focused) => menuFocus = focused;

    public override void _Process(double delta)
    {
        fade = Mathf.MoveToward(fade, menuFocus ? 0f : 1f, (float)delta / FADE_TIME);
        frame.Modulate = new Color(1f, 1f, 1f, fade);
        frame.Visible = fade > 0f;
        if (world?.Player == null)
            return;
        revealTimer -= (float)delta;
        if (revealTimer <= 0f)
        {
            revealTimer = REVEAL_INTERVAL;
            map.Reveal(WorldGrid.ToTile(world.Player.GlobalPosition));
            map.Flush();
        }
        Vector2I tile = WorldGrid.ToTile(world.Player.GlobalPosition);
        Biome biome = world.Grid.BiomeAt(tile.X, tile.Y);
        string name = BiomeArt.Name(biome);
        foreach (MapRegion region in map.Blueprint.Regions)
            if (region.Area.HasPoint(tile))
                name = region.Name;
        if (name != regionLabel.Text)
        {
            regionLabel.Text = name;
            regionLabel.LabelSettings.FontColor = BiomeArt.LabelColor(biome);
        }
        view.QueueRedraw();
    }

    // The scrolling viewport: draws the whole map texture offset so Kio stays at the centre; ClipContents trims the rest.
    private partial class MapView : Control
    {
        private GameWorld world;
        private MapData map;

        public void Setup(GameWorld gameWorld, MapData mapData)
        {
            world = gameWorld;
            map = mapData;
        }

        public override void _Draw()
        {
            if (world?.Player == null)
                return;
            Vector2 center = Size * 0.5f;
            Vector2 focus = world.Player.GlobalPosition / WorldGrid.TILE_SIZE;
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.02f, 0.0f, 0.05f));
            Vector2 origin = center - focus * SCALE;
            DrawTextureRect(map.Texture, new Rect2(origin, new Vector2(map.Width, map.Height) * SCALE), false);

            float pulse = Mathf.Sin(Time.GetTicksMsec() * 0.006f) * 0.5f + 0.5f;
            foreach (MapMarker marker in map.Markers)
            {
                Vector2 position = origin + (new Vector2(marker.Tile.X, marker.Tile.Y) + new Vector2(0.5f, 0.5f)) * SCALE;
                if (new Rect2(Vector2.Zero, Size).Grow(4f).HasPoint(position))
                    MapIcons.DrawMarker(this, marker.Kind, position, 3f, pulse);
            }

            foreach (Entity entity in world.Entities)
            {
                if (entity is not Enemy enemy || enemy.IsDead || entity is TargetDummy)
                    continue;
                Vector2 tile = entity.GlobalPosition / WorldGrid.TILE_SIZE;
                if (!map.IsExplored((int)tile.X, (int)tile.Y))
                    continue;
                Vector2 position = origin + tile * SCALE;
                if (new Rect2(Vector2.Zero, Size).Grow(2f).HasPoint(position))
                    DrawRect(new Rect2(position - Vector2.One, new Vector2(2, 2)), enemy.IsAggroed ? EnemyColor : EnemyColor.Darkened(0.35f));
            }

            MapIcons.DrawPlayer(this, center, world.Player.AimDirection, 3f);
        }
    }
}
