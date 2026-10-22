using System;
using System.Collections.Generic;
using Godot;

namespace Anomaly;

// One screen for the bonfire and for Esc: Kio's four Aspects and the Corruption he carries slowly orbit his Resonance,
// with the Void Hearts he has cleansed beside them, on a plain fade. Hovering an orbiting stat eases the orbit to a stop
// and unfolds that stat's bar and full name. At a bonfire a point of Resonance can be attuned into an Aspect and the
// bonfire's own actions sit to the right; under Esc the same screen is read-only and the right holds the pause actions.
public partial class StatusMenu : CanvasLayer
{
    private const float FADE_TIME = 0.22f;
    private const int HEART_SLOTS = 5;
    private const int RING_DIAMONDS = 8;
    private const float RING_RADIUS = 30f;
    private const float BAR_WIDTH = 62f;
    private const float BAR_HEIGHT = 10f;
    private const float NODE_HEIGHT = 32f;
    private const float BAR_TOP = 21f;
    private const float FRAME_CAP = 12f;
    private const float FILL_INSET_X = 3f;
    private const float FILL_INSET_Y = 3f;
    private const float FILL_HEIGHT = 4f;
    private const float SHIMMER_PERIOD = 2.6f;
    private const float SHIMMER_WIDTH = 10f;
    private const float NODE_PADDING = 16f;
    private const float ORBIT_SPEED = 0.12f;
    private const float ORBIT_EASE = 3f;
    private const float ORBIT_START = -Mathf.Pi * 0.7f;
    private const float EXPAND_TIME = 0.25f;
    private const float SAFE_LEFT = 100f;
    private const float SAFE_RIGHT = 400f;
    private static readonly Vector2 OrbitRadii = new(110f, 64f);
    private static readonly Vector2 STAR_SIZE = new(16f, 16f);
    private static readonly Vector2[] HeartSpots = { new(66f, 184f), new(105f, 184f), new(54f, 215f), new(117f, 215f), new(84f, 233f) };
    private static readonly Vector2[] AmuletSpots = { new(410f, 57f), new(440f, 111f), new(440f, 170f), new(410f, 221f) };
    private const float AMULET_SIZE = 30f;
    private const string AMULET_SCENE = "res://#Scenes/AmuletWindow.tscn";
    private static readonly Vector2 HeartSize = new(18f, 26f);
    private static readonly Vector2 ResonanceCenter = new(240f, 120f);
    private static readonly Color Heal = new(0.55f, 0.82f, 0.52f);
    private static readonly Color CorruptionColor = new(0.8f, 0.5f, 1f);

    // The skill tree grows out of the ring's four diagonal stars. Opening it slides the hearts and amulets off screen
    // and docks the stat bars in two columns at the sides.
    private const float TREE_TIME = 0.55f;
    private const float TREE_STAGGER = 0.08f;
    private const float SLIDE_AWAY = 170f;
    private const float SKILL_SIZE = 14f;
    private const int TRUNK_STAR = 0;
    private const float TRUNK_INNER = 54f;
    private const float TRUNK_OUTER = 78f;
    private static readonly int[] BranchStars = { 7, 1, 5, 3 };
    private static readonly Vector2 LevelsAway = new(-182f, 72f);
    private static readonly Vector2 InnerRadii = new(84f, 52f);
    private static readonly Vector2 OuterRadii = new(150f, 88f);
    // Docked, the stats stack in one column on the right, a clear gap between each bar and the next name.
    private static readonly Vector2[] Docks = { new(422f, 70f), new(422f, 112f), new(422f, 154f), new(422f, 196f), new(422f, 238f) };

    // Clockwise from the top left, as the mock had them: VIT, RAG, RES, then COR below and END on the left.
    private static readonly Aspect[] ASPECTS = { Aspect.Vitality, Aspect.Might, Aspect.Resolve };

    private sealed class Orbiter
    {
        public Button Button;
        public Control Bar;
        public NinePatchRect Frame;
        public VBoxContainer Detail;
        public string Short;
        public string Rest;
        public float Ratio;
        public Color Color;
        public float BarWidth = BAR_WIDTH;
        public float Expand;
        public bool Hovered;
    }

    private readonly List<Button> buttons = new();
    private readonly List<Orbiter> orbiters = new();
    private GameWorld world;
    private OptionsMenu options;
    private AmuletWindow amuletWindow;
    private readonly List<AmuletIcon> amuletIcons = new();
    private Control root;
    private Control content;
    private VBoxContainer info;
    private Action focusInfo;
    private bool bonfire;
    private float fade = FADE_TIME;
    private bool closing;
    private float orbitAngle;
    private float orbitSpeed = ORBIT_SPEED;
    private Orbiter raised;
    private Control popup;
    private string hoveredKey;
    private float hoveredExpand;
    private Control ring;
    private Button center;
    private Control treeLines;
    private float tree;
    private bool treeShown;
    private readonly List<(Control Node, Vector2 Home, Vector2 Away, bool Leaves)> slides = new();
    private readonly List<(Button Button, CombatSkillInfo Info)> skillNodes = new();

    public bool IsOpen => Visible && !closing;

    public bool OptionsOpen => options != null && options.IsOpen;

    public bool AmuletsOpen => amuletWindow != null && amuletWindow.IsOpen;

    // God mode hands out endless level points, so every Aspect can be tried at any height.
    private bool EndlessPoints => world.Player.GodMode;

    private bool CanAttune => bonfire && (EndlessPoints || SaveManager.PlayerStats.UnspentPoints > 0);

    public void Setup(GameWorld gameWorld) => world = gameWorld;

    public override void _Ready()
    {
        Layer = 28;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        Visible = false;
        VisibilityChanged += () => world.Hud.SetSoulsVisible(!Visible);

        root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(root);
        var dim = new ColorRect { Color = new Color(0.01f, 0f, 0.02f, 0.82f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.Position = new Vector2(-2000, -2000);
        dim.Size = new Vector2(4480, 4270);
        root.AddChild(dim);
        content = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(content);

        options = new OptionsMenu { Name = "Options" };
        options.Setup(world, ReturnFromOptions);
        world.CallDeferred(Node.MethodName.AddChild, options);

        amuletWindow = GD.Load<PackedScene>(AMULET_SCENE).Instantiate<AmuletWindow>();
        amuletWindow.Name = "Amulets";
        world.CallDeferred(Node.MethodName.AddChild, amuletWindow);
    }

    public void Open(bool atBonfire)
    {
        bonfire = atBonfire;
        closing = false;
        hoveredKey = null;
        tree = 0f;
        treeShown = false;
        orbiters.Clear();
        Rebuild(DefaultFocus());
        Visible = true;
        world.GetTree().Paused = true;
        WorldSignals.EmitMenuFocusChanged(true);
        fade = 0f;
        root.Modulate = new Color(1f, 1f, 1f, 0f);
    }

    public void Close()
    {
        amuletWindow.Close();
        closing = true;
        world.GetTree().Paused = false;
        WorldSignals.EmitMenuFocusChanged(false);
        Sfx.Play("ui_move", -6f, 0.7f);
    }

    public void CloseAll()
    {
        if (OptionsOpen)
            options.Close();
        if (IsOpen)
            Close();
    }

    public void OpenOptionsForTest() => OpenOptions();

    private string DefaultFocus() => CanAttune ? "attune_" + Aspect.Vitality : bonfire ? "rest" : "close";

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsOpen || !(inputEvent.IsActionPressed(GameInput.PAUSE) || inputEvent.IsActionPressed("ui_cancel")))
            return;
        GetViewport().SetInputAsHandled();
        if (AmuletsOpen)
            amuletWindow.Close();
        else if (treeShown)
            ToggleTree();
        else
            Close();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        if (closing)
        {
            fade = Mathf.Max(0f, fade - Motion.Unscaled(delta));
            root.Modulate = new Color(1f, 1f, 1f, fade / FADE_TIME);
            if (fade <= 0f)
            {
                Visible = false;
                closing = false;
            }
            return;
        }
        UpdateTree(Motion.Unscaled(delta));
        UpdateOrbit(Motion.Unscaled(delta));
        if (fade >= FADE_TIME)
            return;
        fade = Mathf.Min(FADE_TIME, fade + Motion.Unscaled(delta));
        root.Modulate = new Color(1f, 1f, 1f, fade / FADE_TIME);
    }

    private void Rebuild(string focusKey)
    {
        foreach (Node child in content.GetChildren())
        {
            content.RemoveChild(child);
            child.QueueFree();
        }
        buttons.Clear();
        amuletIcons.Clear();
        hoveredExpand = orbiters.Find(orbiter => orbiter.Hovered)?.Expand ?? 0f;
        orbiters.Clear();
        slides.Clear();
        skillNodes.Clear();
        raised = null;
        popup = null;
        focusInfo = null;
        info = new VBoxContainer { Position = new Vector2(130f, 205f), CustomMinimumSize = new Vector2(220f, 0f) };
        info.AddThemeConstantOverride("separation", 2);
        content.AddChild(info);

        BuildResonance();
        BuildOrbit();
        BuildHearts();
        BuildAmulets();
        BuildSide();
        BuildTree();
        UpdateTree(0f);
        UpdateOrbit(0f);
        CallDeferred(MethodName.FocusButton, focusKey);
    }

    private void FocusButton(string key)
    {
        Button target = buttons.Find(button => button.Name.ToString() == key && !button.Disabled)
            ?? buttons.Find(button => !button.Disabled);
        target?.GrabFocus();
    }

    private Label Text(string text, Font font, int size, Color color, Vector2 center, float width = 160f, int outline = 2)
    {
        Label label = UiFactory.Label(text, font, size, color, outline);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Size = new Vector2(width, 20f);
        label.Position = center - label.Size * 0.5f + new Vector2(TrailNudge(font, size), 0f);
        content.AddChild(label);
        return label;
    }

    // Porto Buena's glyphs carry a pixel of spacing after them, so centered text sits left of center; the larger sizes
    // are pushed back by a whole pixel so they stay crisp.
    private static float TrailNudge(Font font, int size)
    {
        int glyph = Art.UiPixelSize(font, size) / 8;
        return font == Art.BodyFont && glyph >= 1 ? Mathf.Max(1, glyph / 2) : 0f;
    }

    private static (string Short, string Full, Color Color) Look(Aspect aspect) => aspect switch
    {
        Aspect.Vitality => ("VIT", "VITALITY", new Color(0.45f, 0.95f, 0.5f)),
        Aspect.Might => ("RAG", "RAGE", new Color(0.95f, 0.2f, 0.2f)),
        Aspect.Endurance => ("END", "ENDURANCE", new Color(1f, 0.62f, 0.1f)),
        _ => ("RES", "RESOLVE", new Color(0.2f, 0.62f, 1f)),
    };

    private Button FlatButton(string key, string text, Color color, int size, Vector2 buttonSize)
    {
        var button = new Button
        {
            Name = key,
            Text = text,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            TextureFilter = UiFactory.TEXT_FILTER,
            CustomMinimumSize = buttonSize,
            Size = buttonSize,
        };
        Color lit = color.Lightened(0.4f);
        button.AddThemeFontOverride("font", Art.TitleFont);
        button.AddThemeFontSizeOverride("font_size", Art.UiPixelSize(Art.TitleFont, size));
        button.AddThemeColorOverride("font_color", color);
        button.AddThemeColorOverride("font_hover_color", lit);
        button.AddThemeColorOverride("font_focus_color", lit);
        button.AddThemeColorOverride("font_pressed_color", lit);
        button.AddThemeColorOverride("font_hover_pressed_color", lit);
        button.AddThemeColorOverride("font_disabled_color", color);
        button.AddThemeColorOverride("font_outline_color", UiFactory.Outline);
        button.AddThemeConstantOverride("outline_size", 3);
        foreach (string style in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(style, new StyleBoxEmpty { ContentMarginBottom = BAR_HEIGHT + 6f });
        button.FocusEntered += () => Sfx.Play("ui_move", -8f);
        button.MouseEntered += button.GrabFocus;
        buttons.Add(button);
        content.AddChild(button);
        return button;
    }

    // The stat bar: the hand-drawn frame, stretched in the middle, over a fill in the stat's own color. The fill is
    // shaded top to bottom and carries a soft highlight that sweeps along it, with a pulse on its leading edge.
    private static Control Bar(Orbiter orbiter)
    {
        var bar = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        float phase = orbiter.Color.R * 1.7f + orbiter.Color.G * 2.3f;
        bar.Draw += () =>
        {
            float width = orbiter.BarWidth - FILL_INSET_X * 2f;
            bar.DrawRect(new Rect2(FILL_INSET_X, FILL_INSET_Y, width, FILL_HEIGHT), new Color(0.05f, 0.04f, 0.06f));
            float filled = Mathf.Round(width * Mathf.Clamp(orbiter.Ratio, 0f, 1f));
            if (filled < 1f)
                return;
            Color c = orbiter.Color;
            bar.DrawRect(new Rect2(FILL_INSET_X, FILL_INSET_Y, filled, 1f), c.Lightened(0.35f));
            bar.DrawRect(new Rect2(FILL_INSET_X, FILL_INSET_Y + 1f, filled, 2f), c);
            bar.DrawRect(new Rect2(FILL_INSET_X, FILL_INSET_Y + 3f, filled, 1f), c.Darkened(0.4f));

            float time = (float)Time.GetTicksMsec() / 1000f + phase;
            float sweep = (time % SHIMMER_PERIOD) / SHIMMER_PERIOD * (filled + SHIMMER_WIDTH * 2f) - SHIMMER_WIDTH;
            for (int i = 0; i < (int)SHIMMER_WIDTH; i++)
            {
                float x = Mathf.Round(sweep + i - SHIMMER_WIDTH * 0.5f);
                if (x < 0f || x >= filled)
                    continue;
                float strength = 1f - Mathf.Abs(i - SHIMMER_WIDTH * 0.5f) / (SHIMMER_WIDTH * 0.5f);
                bar.DrawRect(new Rect2(FILL_INSET_X + x, FILL_INSET_Y, 1f, FILL_HEIGHT), new Color(1f, 1f, 1f, 0.28f * strength));
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
            bar.DrawRect(new Rect2(FILL_INSET_X + filled - 1f, FILL_INSET_Y, 1f, FILL_HEIGHT), new Color(1f, 1f, 1f, 0.25f + 0.35f * pulse));
        };
        var frame = new NinePatchRect
        {
            Texture = Art.Texture("UI/GUI/stat_bar_frame.png"),
            PatchMarginLeft = (int)FRAME_CAP,
            PatchMarginRight = (int)FRAME_CAP,
            // Repeat the middle at its drawn size instead of smearing it, so every pixel of the frame stays one pixel.
            AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        orbiter.Frame = frame;
        bar.AddChild(frame);
        return bar;
    }

    private void BuildOrbit()
    {
        PlayerStats stats = SaveManager.PlayerStats;
        int unspent = stats.UnspentPoints;
        if (unspent > 0 || EndlessPoints)
        {
            // The tree's trunk climbs into this spot, so the count steps aside into the left column under the actions.
            Label count = Text(EndlessPoints ? "+999" : $"+{unspent}", Art.NumberFont, 14, UiFactory.TextColor, new Vector2(240f, 32f), 60f);
            Label caption = Text("LEVELS REMAINING", Art.BodyFont, 6, UiFactory.DimText, new Vector2(240f, 43f), 100f);
            slides.Add((count, count.Position, LevelsAway, false));
            slides.Add((caption, caption.Position, LevelsAway, false));
        }
        foreach (Aspect aspect in ASPECTS)
            AddAspect(aspect);
        Orbiter corruption = AddOrbiter("corruption", "COR", "CORRUPTION", CorruptionColor, world.Player.Corruption / Player.CORRUPTION_MAXIMUM);
        FillDetail(corruption, () => DetailLine(corruption.Detail, $"{Mathf.RoundToInt(world.Player.Corruption)} / {Player.CORRUPTION_MAXIMUM:0}", UiFactory.DimText));
        AddAspect(Aspect.Endurance);
        UpdateOrbit(0f);
    }

    private void AddAspect(Aspect aspect)
    {
        (string abbreviation, string full, Color color) = Look(aspect);
        Orbiter orbiter = AddOrbiter("attune_" + aspect, abbreviation, full, color, AspectRatio(aspect));
        FillDetail(orbiter, () => DetailTable(orbiter.Detail, grid => AspectRows(grid, aspect, !CanAttune)));
        orbiter.Button.Pressed += () => Attune(aspect);
    }

    // Every Aspect fills toward its 40 points; Vitality and Endurance read full early if their 300 cap comes first.
    private float AspectRatio(Aspect aspect)
    {
        Player player = world.Player;
        float points = (float)SaveManager.PlayerStats.GetAspect(aspect) / LevelingSystem.MAX_ASPECT;
        return aspect switch
        {
            Aspect.Vitality => Mathf.Max(points, (player.MaxHealth - player.Stats.MaxHealth) / (LevelingSystem.MAX_HEALTH - player.Stats.MaxHealth)),
            Aspect.Endurance => Mathf.Max(points, (player.MaxStamina - player.BaseStamina) / (LevelingSystem.MAX_STAMINA - player.BaseStamina)),
            _ => points,
        };
    }

    private bool CanRaise(Aspect aspect) =>
        LevelingSystem.CanRaise(SaveManager.PlayerStats, aspect, world.Player.Stats.MaxHealth, world.Player.BaseStamina);

    private Orbiter AddOrbiter(string key, string abbreviation, string full, Color color, float ratio)
    {
        var orbiter = new Orbiter { Short = abbreviation, Rest = full[abbreviation.Length..], Color = color, Ratio = ratio };
        orbiter.Button = FlatButton(key, abbreviation, color, 14, new Vector2(BAR_WIDTH + NODE_PADDING, NODE_HEIGHT));
        orbiter.Bar = Bar(orbiter);
        orbiter.Button.AddChild(orbiter.Bar);
        // A rebuilt menu (after attuning) keeps the stat under the cursor hovered and unfolded.
        if (key == hoveredKey)
        {
            orbiter.Hovered = true;
            orbiter.Expand = hoveredExpand;
        }
        orbiter.Button.MouseEntered += () =>
        {
            orbiter.Hovered = true;
            hoveredKey = key;
        };
        orbiter.Button.MouseExited += () =>
        {
            orbiter.Hovered = false;
            if (hoveredKey == key)
                hoveredKey = null;
        };
        orbiter.Button.FocusEntered += () => SetInfo(null);
        orbiters.Add(orbiter);
        return orbiter;
    }

    // What the stat governs hangs under its bar and fades in as the bar unfolds.
    private static void FillDetail(Orbiter orbiter, Action fill)
    {
        orbiter.Detail = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        orbiter.Detail.AddThemeConstantOverride("separation", 4);
        orbiter.Button.AddChild(orbiter.Detail);
        fill();
    }

    // The stats turn slowly around the level. The hovered one (or the focused one on a gamepad) eases the turn to a
    // stop, doubles its bar and spells out the rest of its name. A bar grows evenly from its own center, two pixels at a
    // time so both ends move together, and only shifts sideways when it would reach the side columns. Positions stay
    // fractional so the orbit glides instead of stepping from pixel to pixel.
    private void UpdateOrbit(float delta)
    {
        if (orbiters.Count == 0)
            return;
        Orbiter active = orbiters.Find(orbiter => orbiter.Hovered)
            ?? (GameInput.UsingGamepad ? orbiters.Find(orbiter => orbiter.Button.HasFocus()) : null);
        orbitSpeed = Mathf.Lerp(orbitSpeed, active == null && !treeShown ? ORBIT_SPEED : 0f, 1f - Mathf.Exp(-delta * ORBIT_EASE));
        orbitAngle = Mathf.Wrap(orbitAngle + orbitSpeed * delta, 0f, Mathf.Tau);
        if (active != null && active != raised)
        {
            active.Button.MoveToFront();
            raised = active;
        }

        for (int i = 0; i < orbiters.Count; i++)
        {
            Orbiter orbiter = orbiters[i];
            orbiter.Expand = Mathf.MoveToward(orbiter.Expand, orbiter == active ? 1f : 0f, delta / EXPAND_TIME);
            float eased = Mathf.SmoothStep(0f, 1f, orbiter.Expand);
            orbiter.BarWidth = Even(BAR_WIDTH * (1f + eased));
            string text = orbiter.Short + orbiter.Rest[..Mathf.RoundToInt(eased * orbiter.Rest.Length)];
            if (orbiter.Button.Text != text)
                orbiter.Button.Text = text;

            float angle = ORBIT_START + orbitAngle + Mathf.Tau * i / orbiters.Count;
            // With the tree open the stats leave the orbit for their docks in the side columns.
            float dock = Motion.Power3InOut(tree);
            Vector2 at = (ResonanceCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OrbitRadii).Lerp(Docks[i % Docks.Length], dock);
            orbiter.Button.CustomMinimumSize = new Vector2(orbiter.BarWidth + NODE_PADDING, NODE_HEIGHT);
            float width = Even(Mathf.Max(orbiter.BarWidth + NODE_PADDING, orbiter.Button.GetCombinedMinimumSize().X));
            float left = Mathf.Clamp(at.X - width * 0.5f, Mathf.Lerp(SAFE_LEFT, 4f, dock), Mathf.Lerp(SAFE_RIGHT, 476f, dock) - width);
            orbiter.Button.Size = new Vector2(width, NODE_HEIGHT);
            orbiter.Button.Position = new Vector2(left, at.Y - NODE_HEIGHT * 0.5f);
            orbiter.Bar.Position = new Vector2((width - orbiter.BarWidth) * 0.5f, BAR_TOP);
            orbiter.Bar.Size = new Vector2(orbiter.BarWidth, BAR_HEIGHT);
            orbiter.Frame.Size = orbiter.Bar.Size;
            if (orbiter.Detail != null)
            {
                orbiter.Detail.Visible = eased > 0f;
                orbiter.Detail.Modulate = new Color(1f, 1f, 1f, eased);
                orbiter.Detail.Position = new Vector2(0f, NODE_HEIGHT);
                orbiter.Detail.Size = new Vector2(width, 0f);
            }
            orbiter.Bar.QueueRedraw();
        }
    }

    private static float Even(float value) => Mathf.Ceil(value * 0.5f) * 2f;

    private void BuildResonance()
    {
        PlayerStats stats = SaveManager.PlayerStats;
        bool maxed = stats.Resonance >= LevelingSystem.MAX_RESONANCE;
        int needed = LevelingSystem.ResidueToNext(stats.Resonance);
        float ratio = maxed ? 1f : Mathf.Clamp((float)stats.Residue / needed, 0f, 1f);
        int lit = maxed ? RING_DIAMONDS : Mathf.FloorToInt(ratio * RING_DIAMONDS);

        Texture2D star = Art.Texture("UI/GUI/corruption_star.png");
        ring = new Control { Size = new Vector2(88f, 88f), MouseFilter = Control.MouseFilterEnum.Stop, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        ring.Position = ResonanceCenter - ring.Size * 0.5f;
        ring.Draw += () =>
        {
            float flare = Motion.Power3InOut(tree);
            for (int i = 0; i < RING_DIAMONDS; i++)
            {
                float angle = Mathf.Tau * i / RING_DIAMONDS - Mathf.Pi * 0.5f;
                Vector2 at = ring.Size * 0.5f + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RING_RADIUS;
                Color tint = new(1f, 1f, 1f, i < lit ? 1f : 0.25f);
                Vector2 size = STAR_SIZE;
                int branch = i == TRUNK_STAR ? CombatSkills.TRUNK : Array.IndexOf(BranchStars, i);
                // The stars the tree grows from swell and take on their branch's color as it opens.
                if ((i == TRUNK_STAR || branch >= 0) && flare > 0f)
                {
                    size = new Vector2(Even(STAR_SIZE.X * (1f + 0.5f * flare)), Even(STAR_SIZE.Y * (1f + 0.5f * flare)));
                    tint = tint.Lerp(CombatSkills.BranchColor(branch).Lightened(0.3f), flare);
                }
                ring.DrawTextureRect(star, new Rect2((at - size * 0.5f).Round(), size), false, tint);
            }
        };
        ring.MouseEntered += () => ShowResonance(maxed, needed);
        ring.MouseExited += RestoreInfo;
        content.AddChild(ring);
        int spent = stats.Vitality + stats.Endurance + stats.Might + stats.Resolve;
        BuildCenter(spent.ToString(), () => ShowResonance(maxed, needed));
    }

    // The level in the middle of the ring opens and closes the skill tree.
    private void BuildCenter(string text, Action show)
    {
        Vector2 size = new(48f, 28f);
        center = new Button
        {
            Name = "skills",
            Text = text,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            TextureFilter = UiFactory.TEXT_FILTER,
            Size = size,
            Position = ResonanceCenter - size * 0.5f,
        };
        Color lit = UiFactory.Accent.Lightened(0.2f);
        center.AddThemeFontOverride("font", Art.NumberFont);
        center.AddThemeFontSizeOverride("font_size", Art.UiPixelSize(Art.NumberFont, 24));
        center.AddThemeColorOverride("font_color", UiFactory.TextColor);
        center.AddThemeColorOverride("font_hover_color", lit);
        center.AddThemeColorOverride("font_focus_color", lit);
        center.AddThemeColorOverride("font_pressed_color", lit);
        center.AddThemeColorOverride("font_hover_pressed_color", lit);
        center.AddThemeColorOverride("font_outline_color", UiFactory.Outline);
        center.AddThemeConstantOverride("outline_size", 3);
        // A left margin of twice the nudge moves the centered text right by the nudge.
        float margin = TrailNudge(Art.NumberFont, 24) * 2f;
        foreach (string style in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            center.AddThemeStyleboxOverride(style, new StyleBoxEmpty { ContentMarginLeft = margin });
        center.FocusEntered += () => Sfx.Play("ui_move", -8f);
        center.FocusEntered += () => SetInfo(show);
        center.MouseEntered += center.GrabFocus;
        center.Pressed += ToggleTree;
        buttons.Add(center);
        content.AddChild(center);
    }

    private void BuildHearts()
    {
        int cleansed = SaveManager.World.DestroyedVoidHearts.Count;
        for (int i = 0; i < HEART_SLOTS; i++)
        {
            int index = i;
            bool done = i < cleansed;
            var slot = new TextureRect
            {
                Texture = Art.Texture(done ? "UI/GUI/void_heart_filled.png" : "UI/GUI/void_heart_empty.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Size = HeartSize,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            slot.Position = HeartSpots[i] - slot.Size * 0.5f;
            slot.MouseEntered += () => ShowHeart(index, done);
            slot.MouseExited += HideHeart;
            content.AddChild(slot);
            slides.Add((slot, slot.Position, new Vector2(-SLIDE_AWAY, 0f), true));
        }
    }

    // The four slots on the right. Hovering one opens its popup to the left; pressing one opens the amulet window.
    private void BuildAmulets()
    {
        Text(bonfire ? "BONFIRE" : "PAUSE", Art.TitleFont, 12, UiFactory.TextColor, new Vector2(240f, 14f), 160f, 3);
        for (int i = 0; i < AmuletSpots.Length; i++)
        {
            int index = i;
            Vector2 size = new(AMULET_SIZE, AMULET_SIZE);
            var slot = new Button
            {
                Name = "amulet_" + i,
                Flat = true,
                FocusMode = Control.FocusModeEnum.All,
                Size = size,
                Position = AmuletSpots[i] - size * 0.5f,
            };
            var frame = new StyleBoxFlat
            {
                BgColor = new Color(0f, 0f, 0f, 0.6f),
                BorderColor = UiFactory.DimText.Darkened(0.4f),
                BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            };
            var lit = (StyleBoxFlat)frame.Duplicate();
            lit.BorderColor = UiFactory.Accent;
            foreach (string style in new[] { "normal", "hover", "disabled" })
                slot.AddThemeStyleboxOverride(style, frame);
            foreach (string style in new[] { "pressed", "hover_pressed", "focus" })
                slot.AddThemeStyleboxOverride(style, lit);
            var icon = new AmuletIcon { Size = size };
            icon.Display(Amulets.InSlot(i));
            slot.AddChild(icon);
            amuletIcons.Add(icon);
            slot.FocusEntered += () => Sfx.Play("ui_move", -8f);
            slot.FocusEntered += () => ShowAmulet(index);
            slot.FocusExited += HideHeart;
            slot.MouseEntered += slot.GrabFocus;
            slot.Pressed += () => OpenAmulets(index);
            buttons.Add(slot);
            content.AddChild(slot);
            slides.Add((slot, slot.Position, new Vector2(SLIDE_AWAY, 0f), true));
        }
        Label hint = Text("AMULETS CAN BE EQUIPPED ANYWHERE", Art.BodyFont, 6, UiFactory.DimText, new Vector2(425f, 252f), 110f);
        slides.Add((hint, hint.Position, new Vector2(SLIDE_AWAY, 0f), true));
    }

    private void OpenAmulets(int index)
    {
        HideHeart();
        amuletWindow.Open(world.Player, index, RefreshAmuletIcons, swapped =>
        {
            // A swap moves the numbers the stats show, so the screen is rebuilt behind the closing window.
            if (swapped)
                Rebuild("amulet_" + index);
            else
                FocusButton("amulet_" + index);
        });
    }

    private void RefreshAmuletIcons()
    {
        for (int i = 0; i < amuletIcons.Count; i++)
            amuletIcons[i].Display(Amulets.InSlot(i));
    }

    private void ShowAmulet(int index)
    {
        AmuletInfo amulet = Amulets.InSlot(index);
        var lines = new List<(string, Font, int, Color)>
        {
            (amulet?.Name.ToUpper() ?? $"AMULET SLOT {index + 1}", Art.TitleFont, 8, amulet?.Color ?? UiFactory.DimText),
            (amulet?.Line ?? "Empty", Art.BodyFont, 6, UiFactory.DimText),
        };
        if (amulet != null)
            foreach ((AmuletStat stat, float amount) in amulet.Stats)
                lines.Add(($"{Amulets.Percent(amount)} {Amulets.Label(stat)}", Art.BodyFont, 6, Amulets.IsGood(stat, amount) ? UiFactory.TextColor : UiFactory.Rend));
        else
            lines.Add(("Press to equip an amulet", Art.BodyFont, 6, UiFactory.Accent));
        ShowPopup(lines, popupWidth => AmuletSpots[index] + new Vector2(-AMULET_SIZE * 0.5f - 6f - popupWidth, -22f), HorizontalAlignment.Right);
    }

    private void BuildSide()
    {
        var column = new VBoxContainer { Position = new Vector2(20f, 18f), CustomMinimumSize = new Vector2(72f, 0f) };
        column.AddThemeConstantOverride("separation", 4);
        content.AddChild(column);
        SideAction(column, "close", bonfire ? "Leave" : "Resume", UiFactory.DimText.Lightened(0.15f), "", UiFactory.DimText, Close, null);
        if (bonfire)
        {
            SideAction(column, "rest", "Rest", Heal, "", UiFactory.DimText, Rest, null);
        }
        else
        {
            SideAction(column, "options", "Options", UiFactory.Accent, "", UiFactory.DimText, OpenOptions, null);
            SideAction(column, "erase", "Erase save", UiFactory.Rend, "", UiFactory.DimText, EraseSave, null);
            SideAction(column, "quit", "Quit", UiFactory.DimText, "", UiFactory.DimText, () => world.GetTree().Quit(), null);
        }
    }

    private Button SideAction(VBoxContainer column, string key, string text, Color tint, string sub, Color subColor, Action pressed, Action show)
    {
        Button button = UiFactory.MenuButton(text.ToUpper());
        button.Name = key;
        button.CustomMinimumSize = new Vector2(0, 14);
        button.AddThemeColorOverride("font_color", tint.Darkened(0.1f));
        button.AddThemeColorOverride("font_hover_color", tint.Lightened(0.3f));
        button.AddThemeColorOverride("font_focus_color", tint.Lightened(0.3f));
        button.Pressed += () =>
        {
            Sfx.Play("ui_confirm", -4f);
            pressed();
        };
        button.MouseEntered += button.GrabFocus;
        button.FocusEntered += () => SetInfo(show);
        buttons.Add(button);

        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        row.AddChild(button);
        if (sub.Length > 0)
        {
            Label label = UiFactory.Label(sub, Art.BodyFont, 6, subColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            row.AddChild(label);
        }
        column.AddChild(row);
        return button;
    }

    // The info block at the bottom only speaks for the ring and the hearts while one is hovered; the stats carry their own.
    private void SetInfo(Action show)
    {
        focusInfo = show;
        RestoreInfo();
    }

    private void RestoreInfo()
    {
        ClearInfo();
        focusInfo?.Invoke();
    }

    private void ClearInfo()
    {
        foreach (Node child in info.GetChildren())
        {
            info.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void InfoTitle(string text, Color color)
    {
        Label title = UiFactory.Label(text, Art.TitleFont, 8, color, 3);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        info.AddChild(title);
    }

    private void InfoLine(string text, Color color) => DetailLine(info, text, color);

    private static void DetailLine(VBoxContainer box, string text, Color color)
    {
        Label line = UiFactory.Label(text, Art.BodyFont, 6, color);
        line.HorizontalAlignment = HorizontalAlignment.Center;
        line.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(line);
    }

    private static void DetailTable(VBoxContainer box, Action<GridContainer> fill)
    {
        GridContainer grid = StatTable.Create();
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        fill(grid);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.AddChild(grid);
        box.AddChild(center);
    }

    private void ShowResonance(bool maxed, int needed)
    {
        ClearInfo();
        PlayerStats stats = SaveManager.PlayerStats;
        InfoTitle($"RESONANCE  {stats.Resonance}", UiFactory.TextColor);
        InfoLine(maxed ? "Fully resonant" : $"Residue {stats.Residue} / {needed}", UiFactory.DimText);
        InfoLine(treeShown ? "Press to close the skill tree" : "Press to open the skill tree", UiFactory.Accent);
    }

    // The heart's popup opens to its right on a band that fades out at both ends, and eases in.
    private void ShowHeart(int index, bool cleansed)
    {
        ShowPopup(new List<(string, Font, int, Color)>
        {
            ($"VOID HEART {index + 1}", Art.TitleFont, 8, cleansed ? CorruptionColor : UiFactory.DimText),
            (cleansed ? "Cleansed" : "Not yet cleansed", Art.BodyFont, 6, UiFactory.DimText),
            (HeartPower(index), Art.BodyFont, 6, cleansed ? UiFactory.TextColor : UiFactory.DimText),
        }, _ => HeartSpots[index] + new Vector2(HeartSize.X * 0.5f + 6f, -22f));
    }

    // A popup opening to the left of its owner sets its lines flush right, so the text starts at the side nearest it.
    private void ShowPopup(List<(string Text, Font Font, int Size, Color Color)> lines, Func<float, Vector2> place, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        HideHeart();
        const float width = 180f;
        const float pad = 22f;
        popup = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 10 };
        var band = new Band { Fade = 0.22f, Fill = new Color(UiFactory.Ink, 0.85f), Edge = new Color(UiFactory.FrameLine, 0.6f), Size = new Vector2(width, 34f + 10f * Mathf.Max(1, lines.Count - 2)) };
        popup.AddChild(band);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(pad, 5f), Size = new Vector2(width - pad * 2f, 0f) };
        box.AddThemeConstantOverride("separation", 2);
        popup.AddChild(box);
        foreach ((string text, Font font, int size, Color color) in lines)
            AddPopupLine(box, text, font, size, color, align);
        popup.Position = place(width);
        popup.Modulate = new Color(1f, 1f, 1f, 0f);
        content.AddChild(popup);
        popup.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).TweenProperty(popup, "modulate:a", 1f, 0.12f);
    }

    private void HideHeart()
    {
        popup?.QueueFree();
        popup = null;
    }

    private static void AddPopupLine(VBoxContainer box, string text, Font font, int size, Color color, HorizontalAlignment align)
    {
        Label line = UiFactory.Label(text, font, size, color, 3);
        line.HorizontalAlignment = align;
        line.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(line);
    }

    private static string HeartPower(int index) => index switch
    {
        < Fracture.HEARTS_TO_UNSEAL - 1 => "Foes grow stronger",
        Fracture.HEARTS_TO_UNSEAL - 1 => "Foes grow stronger. The Fracture unseals",
        _ => "Its power is not yet known",
    };

    // What the Aspect governs now, and what one more point would add, and how many of its 40 points are spent.
    private void AspectRows(GridContainer grid, Aspect aspect, bool dim)
    {
        PlayerStats stats = SaveManager.PlayerStats;
        Player player = world.Player;
        Color gain = UiFactory.Accent;
        bool open = stats.GetAspect(aspect) < LevelingSystem.MAX_ASPECT;
        StatTable.Row(grid, "Points", $"{stats.GetAspect(aspect)}/{LevelingSystem.MAX_ASPECT}", open ? "+1" : StatTable.MAXED, gain, dim);
        switch (aspect)
        {
            case Aspect.Vitality:
                StatTable.Row(grid, "Max HP", Mathf.RoundToInt(player.MaxHealth).ToString(), CanRaise(aspect) ? StatTable.Gain(LevelingSystem.HEALTH_PER_VITALITY, "0") : StatTable.MAXED, gain, dim);
                break;
            case Aspect.Endurance:
                StatTable.Row(grid, "Stamina", Mathf.RoundToInt(player.MaxStamina).ToString(), CanRaise(aspect) ? StatTable.Gain(LevelingSystem.STAMINA_PER_ENDURANCE, "0") : StatTable.MAXED, gain, dim);
                StatTable.Row(grid, "Regen", $"{player.StaminaRegenRate:0}/s", CanRaise(aspect) ? StatTable.Gain(LevelingSystem.REGEN_PER_ENDURANCE, "0", "/s") : StatTable.MAXED, gain, dim);
                break;
            case Aspect.Might:
                // Rage only feeds the Aetheriac State.
                float rageDamage = (LevelingSystem.AetheriacDamage(stats) - 1f) * 100f;
                float lifesteal = LevelingSystem.AetheriacLifesteal(stats) * 100f;
                float drainCut = (1f - LevelingSystem.AetheriacDrainScale(stats)) * 100f;
                StatTable.Row(grid, "Rage dmg", $"+{rageDamage:0.#}%", open ? StatTable.Gain(LevelingSystem.AETHERIAC_DAMAGE_PER_RAGE * 100f, "0.#", "%") : StatTable.MAXED, gain, dim);
                StatTable.Row(grid, "Lifesteal", $"+{lifesteal:0.#}%", open ? StatTable.Gain(LevelingSystem.LIFESTEAL_PER_RAGE * 100f, "0.#", "%") : StatTable.MAXED, gain, dim);
                StatTable.Row(grid, "Drain", $"-{drainCut:0}%", open ? $"-{LevelingSystem.DRAIN_CUT_PER_RAGE * 100f:0}%" : StatTable.MAXED, gain, dim);
                break;
            default:
                float tenacity = (LevelingSystem.TenacityMultiplier(stats) - 1f) * 100f;
                StatTable.Row(grid, "Tenacity", $"+{tenacity:0}%", open ? StatTable.Gain(LevelingSystem.TENACITY_PER_RESOLVE * 100f, "0", "%") : StatTable.MAXED, gain, dim);
                StatTable.Row(grid, "Potency", player.Potency.ToString("0.#"), open ? StatTable.Gain(LevelingSystem.POTENCY_PER_RESOLVE, "0.#") : StatTable.MAXED, gain, dim);
                break;
        }
    }

    private void BuildTree()
    {
        treeLines = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, Size = new Vector2(480f, 270f) };
        treeLines.Draw += DrawTreeLines;
        content.AddChild(treeLines);
        foreach (CombatSkillInfo info in CombatSkills.All)
            AddSkillNode(info);
    }

    private static int StarOf(int branch) => branch == CombatSkills.TRUNK ? TRUNK_STAR : BranchStars[branch];

    private static Vector2 BranchDirection(int branch)
    {
        float angle = Mathf.Tau * StarOf(branch) / RING_DIAMONDS - Mathf.Pi * 0.5f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // The trunk climbs straight up from the top star; the branches reach out along the diagonals.
    private static Vector2 SkillPoint(CombatSkillInfo info) => info.Branch == CombatSkills.TRUNK
        ? ResonanceCenter + new Vector2(0f, info.Outer ? -TRUNK_OUTER : -TRUNK_INNER)
        : (ResonanceCenter + BranchDirection(info.Branch) * (info.Outer ? OuterRadii : InnerRadii)).Round();

    // The trunk grows first, then each branch a beat after the one before it; they retract in the same order.
    private float BranchProgress(int branch) =>
        Mathf.Clamp((tree - (branch + 1) * TREE_STAGGER) / (1f - TREE_STAGGER * CombatSkills.BRANCHES), 0f, 1f);

    private static float Segment(float progress, float from, float to) => Mathf.Clamp((progress - from) / (to - from), 0f, 1f);

    private bool CanLearn(CombatSkillInfo info) =>
        bonfire && CombatSkills.Unlockable(info.Skill) && (EndlessPoints || SaveManager.PlayerStats.UnspentPoints >= info.Cost);

    // The vine runs out of the star to the inner art, then on to the outer one, with a bright tip while it grows.
    private void DrawTreeLines()
    {
        for (int branch = CombatSkills.TRUNK; branch < CombatSkills.BRANCHES; branch++)
        {
            float progress = BranchProgress(branch);
            if (progress <= 0f)
                continue;
            CombatSkillInfo inner = Array.Find(CombatSkills.All, info => info.Branch == branch && !info.Outer);
            CombatSkillInfo outer = Array.Find(CombatSkills.All, info => info.Branch == branch && info.Outer);
            Color color = CombatSkills.BranchColor(branch);
            Vector2 star = ResonanceCenter + BranchDirection(branch) * (RING_RADIUS + 7f);
            DrawVine(star, SkillPoint(inner), Segment(progress, 0f, 0.45f), color, CombatSkills.Has(inner.Skill));
            DrawVine(SkillPoint(inner), SkillPoint(outer), Segment(progress, 0.5f, 0.85f), color, CombatSkills.Has(outer.Skill));
        }
    }

    private void DrawVine(Vector2 from, Vector2 to, float grown, Color color, bool lit)
    {
        if (grown <= 0f)
            return;
        Vector2 tip = from.Lerp(to, Motion.Power2Out(grown)).Round();
        treeLines.DrawLine(from.Round(), tip, new Color(color, lit ? 0.3f : 0.15f), 3f);
        treeLines.DrawLine(from.Round(), tip, new Color(color, lit ? 0.95f : 0.45f), 1f);
        if (grown < 1f)
            treeLines.DrawRect(new Rect2(tip - Vector2.One, new Vector2(2f, 2f)), Colors.White);
    }

    private void AddSkillNode(CombatSkillInfo info)
    {
        Vector2 size = new(SKILL_SIZE, SKILL_SIZE);
        var button = new Button
        {
            Name = "skill_" + info.Skill,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            Size = size,
            PivotOffset = size * 0.5f,
            Position = SkillPoint(info) - size * 0.5f,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        foreach (string style in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            button.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        button.Draw += () => DrawSkill(button, info);
        Color color = CombatSkills.BranchColor(info.Branch);
        bool open = CombatSkills.Has(info.Skill) || CombatSkills.Unlockable(info.Skill);
        Label name = UiFactory.Label(info.Name, Art.BodyFont, 6, CombatSkills.Has(info.Skill) ? color : open ? UiFactory.DimText : UiFactory.DimText.Darkened(0.4f));
        name.Size = new Vector2(80f, 10f);
        // The trunk's names sit beside it, clear of the node stacked above.
        if (info.Branch == CombatSkills.TRUNK)
        {
            name.Position = new Vector2(SKILL_SIZE + 3f, (SKILL_SIZE - name.Size.Y) * 0.5f);
        }
        else
        {
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Position = new Vector2(SKILL_SIZE * 0.5f - 40f, SKILL_SIZE + 1f);
        }
        button.AddChild(name);
        button.FocusEntered += () => Sfx.Play("ui_move", -8f);
        button.FocusEntered += () => SetInfo(() => ShowSkill(info));
        button.MouseEntered += button.GrabFocus;
        button.Pressed += () => LearnSkill(info);
        buttons.Add(button);
        skillNodes.Add((button, info));
        content.AddChild(button);
    }

    // A diamond: filled in its branch color once learned, a pulsing outline while it can be learned, dim while locked.
    private void DrawSkill(Button button, CombatSkillInfo info)
    {
        Color color = CombatSkills.BranchColor(info.Branch);
        bool learned = CombatSkills.Has(info.Skill);
        bool open = CombatSkills.Unlockable(info.Skill);
        bool learnable = CanLearn(info);
        bool lit = button.HasFocus() || button.IsHovered();
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 1000f * 4f);
        Vector2 c = button.Size * 0.5f;
        float r = SKILL_SIZE * 0.5f - (lit ? 0f : 1f);
        Vector2[] diamond = Diamond(c, r);
        if (learnable)
            button.DrawColoredPolygon(Diamond(c, r + 2f), new Color(color, 0.15f + 0.25f * pulse));
        button.DrawColoredPolygon(diamond, learned ? color.Darkened(0.15f) : new Color(0.05f, 0.03f, 0.06f, 0.92f));
        Color edge = lit ? Colors.White
            : learned ? color.Lightened(0.4f)
            : learnable ? color.Lerp(Colors.White, 0.5f * pulse)
            : open ? color.Darkened(0.35f)
            : UiFactory.DimText.Darkened(0.5f);
        button.DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, edge, 1f);
        if (learned)
            button.DrawColoredPolygon(Diamond(c, 2f), Colors.White);
    }

    private static Vector2[] Diamond(Vector2 c, float r) => new[] { c + new Vector2(0f, -r), c + new Vector2(r, 0f), c + new Vector2(0f, r), c + new Vector2(-r, 0f) };

    private void UpdateTree(float delta)
    {
        if (ring == null)
            return;
        float before = tree;
        tree = Mathf.MoveToward(tree, treeShown ? 1f : 0f, delta / TREE_TIME);
        float slide = Motion.Power3InOut(tree);
        foreach ((Control node, Vector2 home, Vector2 away, bool leaves) in slides)
        {
            node.Position = home + (away * slide).Round();
            node.Visible = !leaves || slide < 1f;
        }
        if (tree != before || delta == 0f)
            ring.QueueRedraw();
        treeLines.Visible = tree > 0f;
        treeLines.QueueRedraw();
        foreach ((Button button, CombatSkillInfo info) in skillNodes)
        {
            float progress = BranchProgress(info.Branch);
            float pop = info.Outer ? Segment(progress, 0.75f, 1f) : Segment(progress, 0.35f, 0.6f);
            button.Visible = pop > 0f || treeShown;
            button.Scale = Vector2.One * Mathf.Max(Motion.BackOut(pop), 0.01f);
            button.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(pop * 2f, 0f, 1f));
            button.FocusMode = treeShown ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
            button.QueueRedraw();
        }
    }

    private void ToggleTree()
    {
        treeShown = !treeShown;
        HideHeart();
        Sfx.Play(treeShown ? "ui_confirm" : "ui_move", -6f, treeShown ? 1.1f : 0.8f);
        if (!treeShown)
        {
            center.GrabFocus();
            return;
        }
        Button first = skillNodes.Find(node => CanLearn(node.Info)).Button ?? skillNodes[0].Button;
        first.FocusMode = Control.FocusModeEnum.All;
        first.Visible = true;
        first.GrabFocus();
    }

    private void ShowSkill(CombatSkillInfo skill)
    {
        ClearInfo();
        PlayerStats stats = SaveManager.PlayerStats;
        InfoTitle(skill.Name, CombatSkills.BranchColor(skill.Branch));
        InfoLine(skill.Line, UiFactory.Accent);
        Label detail = UiFactory.Label(skill.Detail, Art.BodyFont, 6, UiFactory.TextColor);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.CustomMinimumSize = new Vector2(200f, 0f);
        info.AddChild(detail);
        string cost = skill.Cost == 1 ? "1 level" : $"{skill.Cost} levels";
        if (CombatSkills.Has(skill.Skill))
            InfoLine("LEARNED", CombatSkills.BranchColor(skill.Branch));
        else if (skill.Requires is CombatSkill required && !CombatSkills.Has(required))
            InfoLine($"Requires {CombatSkills.Info(required).Name}", UiFactory.DimText);
        else if (!bonfire)
            InfoLine($"Costs {cost}. Learn at a bonfire", UiFactory.DimText);
        else if (CanLearn(skill))
            InfoLine($"Costs {cost}. Press to learn", UiFactory.Accent);
        else
            InfoLine($"Costs {cost}. {stats.UnspentPoints} remaining", UiFactory.Rend);
    }

    private void LearnSkill(CombatSkillInfo info)
    {
        if (!CanLearn(info))
        {
            Sfx.Play("ui_move", -6f, 0.6f);
            return;
        }
        if (!EndlessPoints)
            SaveManager.PlayerStats.UnspentPoints -= info.Cost;
        CombatSkills.Learn(info.Skill);
        world.Player.BroadcastAll();
        SaveManager.Save(SavePoint.PlayerUpgrade);
        Sfx.Play("level_up", -8f, 1.25f);
        Rebuild("skill_" + info.Skill);
    }

    private void Attune(Aspect aspect)
    {
        if (!CanAttune || !CanRaise(aspect))
            return;
        PlayerStats stats = SaveManager.PlayerStats;
        if (!EndlessPoints)
            stats.UnspentPoints--;
        stats.RaiseAspect(aspect);
        world.Player.RefreshMaxHealth();
        world.Player.BroadcastAll();
        SaveManager.Save(SavePoint.PlayerUpgrade);
        Sfx.Play("level_up", -8f, 1.1f);
        Rebuild(CanAttune && CanRaise(aspect) ? "attune_" + aspect : "close");
    }

    private void Rest()
    {
        Close();
        world.RestAtBonfire();
    }

    private void OpenOptions()
    {
        Visible = false;
        options.Open();
    }

    private void ReturnFromOptions()
    {
        Open(false);
        Rebuild("options");
    }

    private void EraseSave()
    {
        foreach (string file in new[] { "player_stats.res", "weapon_stats.res", "progression.res", "world_data.res", "difficulty_data.res" })
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://" + file));
        SaveManager.NewGame();
        GameWorld.ResetRealm();
        Close();
        world.GetTree().ReloadCurrentScene();
    }
}
