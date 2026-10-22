using Godot;

namespace Anomaly;

public partial class Hud : CanvasLayer
{
    private const string PLAYER_UI = "UI/Resources UI/Player/";

    private const float FADE_TIME = 0.35f;
    private const float PROMPT_FADE_TIME = 0.25f;
    private const float PROMPT_CHARS_PER_SECOND = 42f;
    private const float PROMPT_TOP = 166f;

    private Control root;
    private Control fadeLayer;
    private string promptText = "";
    private string shownPrompt = "";
    private float promptFade;
    private float promptTyped;
    private bool menuFocus;
    private float fade = 1f;
    private TextureProgressBar healthGhost;
    private TextureProgressBar healthBar;
    private TextureProgressBar staminaBar;
    private TextureProgressBar vesselBar;
    private TextureProgressBar corruptionStar;
    private TextureProgressBar healthStar;
    private TextureProgressBar staminaStar;
    private Control cluster;
    private Label corruptionHint;
    private Label soulsLabel;
    private TextureRect arcIcon;
    private Label arcLabel;
    private Control arcPanel;
    private Label regionTitle;
    private Label regionSubtitle;
    private Control regionCard;
    private Control prompt;
    private Label promptKey;
    private Label promptLabel;
    private Band promptBand;
    private Control toast;
    private Label toastLabel;
    private Control focusPanel;
    private Label focusName;
    private ColorRect focusFill;
    private ColorRect focusGhost;

    private ShaderMaterial edgeGlow;
    private float rageGlow;
    private float healGlow;
    private float healthRatio = 1f;
    private float ghostRatio = 1f;
    private float lastHealthChange;
    private float toastTimer;
    private int displayedSouls;
    private int targetSouls;
    private bool aetheriacActive;
    private float corruptionRatio;
    private float time;
    private Entity focusEntity;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        BuildEdgeGlow();
        // Resource cluster and weapon panel live here so the forge menu can fade them out together.
        fadeLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        fadeLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(fadeLayer);
        BuildResourceCluster();
        BuildSouls();
        BuildArcPanel();
        BuildNarrative();
        BuildFocusBar();
        Subscribe();
    }

    public override void _ExitTree() => Unsubscribe();

    // Layout mirrors the former project's "Segment Manager": authored in its local units, scaled 1.25 so it covers
    // the same share of the 480x270 canvas that the old 5x cluster covered of 1920x1080. Draw order is the same too.
    private const float CLUSTER_SCALE = 1.25f;
    private static readonly Vector2 HEALTH_POS = new(28.8f, 13.4f);
    private static readonly Vector2 HEALTH_SIZE = new(50f, 10f);
    private static readonly Vector2 HEALTH_STAR_POS = new(70.2f, 3.2f);
    private static readonly Vector2 VESSEL_POS = new(36f, 13f);
    private static readonly Vector2 VESSEL_SIZE = new(42f, 3f);
    private static readonly Vector2 STAMINA_POS = new(43.2f, 22.4f);
    private static readonly Vector2 STAMINA_SIZE = new(80f, 7f);
    private static readonly Vector2 STAMINA_STAR_POS = new(33.6f, 20.8f);
    private const float BASE_HEALTH = 100f;
    private const float BASE_STAMINA = 80f;

    // Its own unscaled layer under the HUD, so the glow hugs the real screen edge even when the HUD is letterboxed.
    private void BuildEdgeGlow()
    {
        var layer = new CanvasLayer { Layer = Layer - 1 };
        AddChild(layer);
        edgeGlow = new ShaderMaterial { Shader = ShaderLibrary.EdgeGlow };
        var rect = new ColorRect { Material = edgeGlow, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
    }

    private void BuildResourceCluster()
    {
        cluster = new Control { Scale = Vector2.One * CLUSTER_SCALE, MouseFilter = Control.MouseFilterEnum.Ignore };
        fadeLayer.AddChild(cluster);

        vesselBar = ClusterBar("Vessel Bar/Vessel_Bar", VESSEL_POS, VESSEL_SIZE);
        vesselBar.Value = 0;
        staminaBar = ClusterBar("Stamina Bar/Stamina_Bar", STAMINA_POS, STAMINA_SIZE);
        staminaStar = ClusterStar("Stamina Bar/Stamina_Star", STAMINA_STAR_POS, new Vector2(13, 13));

        healthGhost = ClusterBar("Health Bar/Health_Bar", HEALTH_POS, HEALTH_SIZE);
        healthGhost.TextureOver = null;
        healthGhost.TextureProgress = RecolorGreenToRed(healthGhost.TextureProgress);
        healthGhost.TintProgress = new Color(1f, 1f, 1f, 0.85f);
        healthBar = ClusterBar("Health Bar/Health_Bar", HEALTH_POS, HEALTH_SIZE);
        healthBar.TextureUnder = null;
        healthStar = ClusterStar("Health Bar/Health_Star", HEALTH_STAR_POS, new Vector2(27, 27));
        healthStar.Value = 0;

        corruptionStar = ClusterStar("Corruption Bar/Corruption_Bar", new Vector2(0f, 6.4f), new Vector2(35, 37), "_Overr");
        corruptionStar.Value = 0;

        corruptionHint = UiFactory.Label("R", Art.NumberFont, 6, UiFactory.Rend.Lightened(0.3f));
        corruptionHint.Position = new Vector2(20, 24);
        corruptionHint.Visible = false;
        fadeLayer.AddChild(corruptionHint);
    }

    // Bars stretch like the former scene: 20px nine-patch caps, no vertical margins.
    // The lost-health trail reuses the health fill, so its green pixels are remapped to dark red; the dark outline stays.
    private static Texture2D RecolorGreenToRed(Texture2D source)
    {
        Image image = source.GetImage();
        image.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (pixel.A > 0f && pixel.G > pixel.R + 0.15f)
                    image.SetPixel(x, y, new Color(0.62f * pixel.G, 0.06f * pixel.G, 0.08f * pixel.G, pixel.A));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private TextureProgressBar ClusterBar(string path, Vector2 position, Vector2 size)
    {
        TextureProgressBar bar = UiFactory.Bar(PLAYER_UI + path + "_Under.png", PLAYER_UI + path + "_Progress.png", PLAYER_UI + path + "_Over.png", position, size, 20);
        bar.StretchMarginTop = 0;
        bar.StretchMarginBottom = 0;
        cluster.AddChild(bar);
        return bar;
    }

    // Stars fill bottom to top and keep their native size.
    private TextureProgressBar ClusterStar(string path, Vector2 position, Vector2 size, string overSuffix = "_Over")
    {
        TextureProgressBar star = UiFactory.Bar(PLAYER_UI + path + "_Under.png", PLAYER_UI + path + "_Progress.png", PLAYER_UI + path + overSuffix + ".png", position, size, 0);
        star.NinePatchStretch = false;
        star.FillMode = (int)TextureProgressBar.FillModeEnum.BottomToTop;
        star.PivotOffset = size * 0.5f;
        cluster.AddChild(star);
        return star;
    }

    // Souls sit on their own layer above the stats menu (and below the forge and options), so those menus show this
    // very counter through their fade instead of a copy. The stats menu hides it.
    private const int SOULS_LAYER = 29;

    private CanvasLayer soulsLayer;

    public void SetSoulsVisible(bool visible) => soulsLayer.Visible = visible;

    private void BuildSouls()
    {
        var layer = soulsLayer = new CanvasLayer { Layer = SOULS_LAYER };
        AddChild(layer);
        ViewScale.Fit(layer);
        var souls = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        souls.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(souls);
        souls.AddChild(new Band { Position = new Vector2(380, 249), Size = new Vector2(98, 14), Fade = 0.5f, Fill = new Color(UiFactory.Ink, 0.75f), Edge = new Color(UiFactory.FrameLine, 0.6f) });
        var icon = new SoulGlyph { Position = new Vector2(460, 256) };
        souls.AddChild(icon);
        soulsLabel = UiFactory.Label("0", Art.NumberFont, 12, UiFactory.TextColor);
        soulsLabel.Position = new Vector2(398, 248);
        soulsLabel.Size = new Vector2(52, 16);
        soulsLabel.VerticalAlignment = VerticalAlignment.Center;
        soulsLabel.HorizontalAlignment = HorizontalAlignment.Right;
        souls.AddChild(soulsLabel);
    }

    private void BuildArcPanel()
    {
        arcPanel = new Panel { Position = new Vector2(8, 238), Size = new Vector2(26, 26), MouseFilter = Control.MouseFilterEnum.Ignore };
        arcPanel.AddThemeStyleboxOverride("panel", UiFactory.Frame(0.8f));
        fadeLayer.AddChild(arcPanel);
        arcIcon = new TextureRect
        {
            Position = new Vector2(4, 4),
            Size = new Vector2(18, 18),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        arcPanel.AddChild(arcIcon);
        arcLabel = UiFactory.Label("", Art.BodyFont, 6, UiFactory.DimText);
        arcLabel.Position = new Vector2(39, 243);
        fadeLayer.AddChild(arcLabel);
    }

    public static string ControlsHint()
    {
        string move = GameInput.Primary(GameInput.MOVE_UP) + GameInput.Primary(GameInput.MOVE_LEFT) + GameInput.Primary(GameInput.MOVE_DOWN) + GameInput.Primary(GameInput.MOVE_RIGHT);
        return $"{move} move   {GameInput.Describe(GameInput.ATTACK)} strike   {GameInput.Describe(GameInput.SPECIAL)} arc special\n"
            + $"{GameInput.Describe(GameInput.DASH)} dash   {GameInput.Describe(GameInput.JUMP)} jump   {GameInput.Describe(GameInput.HEAL)} heal   {GameInput.Describe(GameInput.AETHERIAC)} Aetheriac\n"
            + $"{GameInput.Describe(GameInput.INTERACT)} interact   {GameInput.Describe(GameInput.MAP)} map   {GameInput.Describe(GameInput.CAMERA_UNLOCK)} camera   ESC pause";
    }

    private void BuildNarrative()
    {
        regionCard = new Control { Position = new Vector2(0, 56), Size = new Vector2(480, 48), Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(regionCard);
        regionCard.AddChild(new Band { Position = new Vector2(90, 0), Size = new Vector2(300, 44), Fade = 0.4f, Fill = new Color(UiFactory.Ink, 0.55f), Edge = Colors.Transparent });
        regionTitle = UiFactory.Label("", Art.TitleFont, 27, UiFactory.TextColor, 4);
        regionTitle.HorizontalAlignment = HorizontalAlignment.Center;
        regionTitle.Position = new Vector2(0, 2);
        regionTitle.Size = new Vector2(480, 28);
        regionCard.AddChild(regionTitle);
        regionCard.AddChild(new Divider { Position = new Vector2(170, 28), Size = new Vector2(140, 5) });
        regionSubtitle = UiFactory.Label("", Art.BodyFont, 6, UiFactory.Accent);
        regionSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        regionSubtitle.Position = new Vector2(0, 33);
        regionSubtitle.Size = new Vector2(480, 8);
        regionCard.AddChild(regionSubtitle);

        // Interaction prompt: the key on its own line, centred above a band that fades in while the name types out.
        prompt = new Control { Position = new Vector2(0, PROMPT_TOP), Size = new Vector2(480, 22), Modulate = new Color(1, 1, 1, 0), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(prompt);
        promptBand = new Band { Position = new Vector2(160, 10), Size = new Vector2(160, 12), Fade = 0.4f, Fill = new Color(UiFactory.Ink, 0.75f) };
        prompt.AddChild(promptBand);
        promptKey = UiFactory.Label("", Art.BodyFont, 6, UiFactory.Accent);
        promptKey.HorizontalAlignment = HorizontalAlignment.Center;
        promptKey.Size = new Vector2(480, 9);
        prompt.AddChild(promptKey);
        promptLabel = UiFactory.Label("", Art.BodyFont, 6, UiFactory.TextColor);
        promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
        promptLabel.VerticalAlignment = VerticalAlignment.Center;
        promptLabel.Position = new Vector2(0, 10);
        promptLabel.Size = new Vector2(480, 12);
        prompt.AddChild(promptLabel);

        toast = new Control { Position = new Vector2(0, 122), Size = new Vector2(480, 11), Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(toast);
        toast.AddChild(new Band { Position = new Vector2(130, 0), Size = new Vector2(220, 11) });
        toastLabel = UiFactory.Label("", Art.BodyFont, 6, UiFactory.TextColor);
        toastLabel.HorizontalAlignment = HorizontalAlignment.Center;
        toastLabel.VerticalAlignment = VerticalAlignment.Center;
        toastLabel.Size = new Vector2(480, 11);
        toast.AddChild(toastLabel);
    }

    private void BuildFocusBar()
    {
        focusPanel = new Control { Position = new Vector2(140, 250), Size = new Vector2(200, 12), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(focusPanel);
        focusName = UiFactory.Label("", Art.TitleFont, 9, UiFactory.TextColor);
        focusName.Position = new Vector2(0, -10);
        focusName.Size = new Vector2(200, 10);
        focusName.HorizontalAlignment = HorizontalAlignment.Center;
        focusPanel.AddChild(focusName);
        focusPanel.AddChild(new ColorRect { Color = UiFactory.Outline, Size = new Vector2(200, 5), Position = new Vector2(0, 2) });
        focusPanel.AddChild(new ColorRect { Color = new Color(0.16f, 0.06f, 0.09f), Size = new Vector2(198, 3), Position = new Vector2(1, 3) });
        focusGhost = new ColorRect { Color = new Color(0.95f, 0.88f, 0.8f, 0.85f), Size = new Vector2(198, 3), Position = new Vector2(1, 3) };
        focusPanel.AddChild(focusGhost);
        focusFill = new ColorRect { Color = UiFactory.Rend, Size = new Vector2(198, 3), Position = new Vector2(1, 3) };
        focusPanel.AddChild(focusFill);
        focusPanel.AddChild(new DiamondMark { Position = new Vector2(0.5f, 4.5f) });
        focusPanel.AddChild(new DiamondMark { Position = new Vector2(199.5f, 4.5f) });
    }

    private void Subscribe()
    {
        PlayerSignals.HealthChanged += OnHealthChanged;
        PlayerSignals.StaminaChanged += OnStaminaChanged;
        PlayerSignals.VesselChanged += OnVesselChanged;
        PlayerSignals.CorruptionChanged += OnCorruptionChanged;
        PlayerSignals.SoulsChanged += OnSoulsChanged;
        PlayerSignals.ArcChanged += OnArcChanged;
        PlayerSignals.SpecialReadinessChanged += OnSpecialReadiness;
        PlayerSignals.Damaged += OnDamaged;
        WorldSignals.RegionEntered += OnRegionEntered;
        WorldSignals.InteractionPromptChanged += OnPromptChanged;
        WorldSignals.MenuFocusChanged += OnMenuFocusChanged;
        EnemySignals.Aggroed += OnEnemyAggroed;
    }

    private void Unsubscribe()
    {
        PlayerSignals.HealthChanged -= OnHealthChanged;
        PlayerSignals.StaminaChanged -= OnStaminaChanged;
        PlayerSignals.VesselChanged -= OnVesselChanged;
        PlayerSignals.CorruptionChanged -= OnCorruptionChanged;
        PlayerSignals.SoulsChanged -= OnSoulsChanged;
        PlayerSignals.ArcChanged -= OnArcChanged;
        PlayerSignals.SpecialReadinessChanged -= OnSpecialReadiness;
        PlayerSignals.Damaged -= OnDamaged;
        WorldSignals.RegionEntered -= OnRegionEntered;
        WorldSignals.InteractionPromptChanged -= OnPromptChanged;
        WorldSignals.MenuFocusChanged -= OnMenuFocusChanged;
        EnemySignals.Aggroed -= OnEnemyAggroed;
    }

    private void OnHealthChanged(float current, float maximum)
    {
        float ratio = maximum > 0f ? current / maximum : 0f;
        if (ratio < healthRatio - 0.001f)
            lastHealthChange = 0f;
        healthRatio = ratio;
        // Each point of max health widens the bar by one pixel; the Health Star rides the right edge, the Vessel follows.
        float extra = Mathf.Max(0f, maximum - BASE_HEALTH);
        healthBar.Size = new Vector2(HEALTH_SIZE.X + extra, HEALTH_SIZE.Y);
        healthGhost.Size = healthBar.Size;
        vesselBar.Size = new Vector2(VESSEL_SIZE.X + extra, VESSEL_SIZE.Y);
        healthStar.Position = HEALTH_STAR_POS + new Vector2(extra, 0f);
    }

    private void OnStaminaChanged(float current, float maximum)
    {
        staminaBar.Value = current / maximum;
        float extra = Mathf.Max(0f, maximum - BASE_STAMINA) * 0.5f;
        staminaBar.Size = new Vector2(STAMINA_SIZE.X + extra, STAMINA_SIZE.Y);
    }

    private void OnVesselChanged(float current, float maximum, float healCharge)
    {
        vesselBar.Value = current / maximum;
        healthStar.Value = healCharge;
    }

    private void OnCorruptionChanged(float current, float maximum, bool active)
    {
        corruptionRatio = current / maximum;
        corruptionStar.Value = corruptionRatio;
        aetheriacActive = active;
    }

    private void OnSoulsChanged(int souls) => targetSouls = souls;

    private void OnArcChanged(SoulWeaponArc arc)
    {
        arcIcon.Texture = arc.Icon;
        arcLabel.Text = $"{arc.ArcName}\n{arc.PrimaryDamageType}  /  {arc.SpecialMoveDamageType}";
    }

    private void OnSpecialReadiness(float readiness)
    {
        staminaStar.Value = readiness;
        arcIcon.Modulate = readiness >= 1f ? Colors.White : new Color(0.45f, 0.45f, 0.5f, 0.7f + readiness * 0.3f);
    }

    private void OnDamaged()
    {
        healthBar.Position = HEALTH_POS + new Vector2((float)GD.RandRange(-2, 2), (float)GD.RandRange(-1, 1));
    }

    private void OnEnemyAggroed(Enemy enemy)
    {
        if (enemy is Brute)
            ShowFocusBar(enemy, enemy.DisplayName.ToUpper());
    }

    public void ShowFocusBar(Entity entity, string name)
    {
        focusEntity = entity;
        focusName.Text = name;
        focusPanel.Visible = true;
        focusGhost.Size = new Vector2(198 * entity.HealthRatio, 3);
    }

    private void OnRegionEntered(string title, string subtitle)
    {
        regionTitle.Text = title;
        regionSubtitle.Text = subtitle;
        Tween tween = regionCard.CreateTween();
        regionCard.Modulate = new Color(1, 1, 1, 0);
        tween.TweenProperty(regionCard, "modulate:a", 1f, 0.9f);
        tween.TweenInterval(2.6f);
        tween.TweenProperty(regionCard, "modulate:a", 0f, 1.2f);
    }

    private void OnPromptChanged(string text) => promptText = text ?? "";

    private void OnMenuFocusChanged(bool focused) => menuFocus = focused;

    // Every modal (pause, menus, map, narration) pauses the tree, so the prompt steps aside for all of them.
    private void UpdatePrompt(float dt)
    {
        bool show = promptText != "" && !menuFocus && !GetTree().Paused;
        if (show && (shownPrompt != promptText || promptFade <= 0f))
            SetPrompt(promptText);
        promptFade = Mathf.MoveToward(promptFade, show ? 1f : 0f, dt / PROMPT_FADE_TIME);
        prompt.Modulate = new Color(1f, 1f, 1f, promptFade);
        prompt.Visible = promptFade > 0f;
        if (promptLabel.VisibleCharacters < 0)
            return;
        promptTyped += dt * PROMPT_CHARS_PER_SECOND;
        promptLabel.VisibleCharacters = promptTyped >= promptLabel.GetTotalCharacterCount() ? -1 : (int)promptTyped;
    }

    // The band hugs the name, with room for its faded ends.
    private void SetPrompt(string text)
    {
        shownPrompt = text;
        promptKey.Text = $"[{GameInput.Describe(GameInput.INTERACT)}]";
        promptLabel.Text = text;
        promptLabel.VisibleCharacters = 0;
        promptTyped = 0f;
        LabelSettings settings = promptLabel.LabelSettings;
        float width = Mathf.Max(100f, Mathf.Round(settings.Font.GetStringSize(text, HorizontalAlignment.Left, -1, settings.FontSize).X + 60f));
        promptBand.Position = new Vector2(Mathf.Round(240f - width * 0.5f), promptBand.Position.Y);
        promptBand.Size = new Vector2(width, promptBand.Size.Y);
    }

    public void Toast(string message, float duration = 2.2f)
    {
        toastLabel.Text = message;
        toast.Modulate = Colors.White;
        toastTimer = duration;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        time += dt;
        lastHealthChange += dt;
        fade = Mathf.MoveToward(fade, menuFocus ? 0f : 1f, dt / FADE_TIME);
        fadeLayer.Modulate = new Color(1f, 1f, 1f, fade);        fadeLayer.Visible = fade > 0f;
        UpdatePrompt(dt);

        healthBar.Value = healthRatio;
        healthBar.Position = healthBar.Position.Lerp(HEALTH_POS, 1f - Mathf.Exp(-20f * dt));
        if (lastHealthChange > 0.5f || healthRatio > ghostRatio)
            ghostRatio = Mathf.MoveToward(ghostRatio, healthRatio, dt * (healthRatio > ghostRatio ? 4f : 0.6f));
        healthGhost.Value = Mathf.Max(ghostRatio, healthRatio);
        // Full meters stay still; the screen edge says it instead (red top edge for Corruption, green top-left for a heal).
        bool healReady = healthStar.Value >= 1f;
        bool rageReady = corruptionRatio >= 1f && !aetheriacActive;
        float glowEase = 1f - Mathf.Exp(-2.5f * dt);
        rageGlow = Mathf.Lerp(rageGlow, rageReady ? 1f : 0f, glowEase);
        healGlow = Mathf.Lerp(healGlow, healReady ? 1f : 0f, glowEase);
        Vector2 screen = GetViewport().GetVisibleRect().Size;
        edgeGlow.SetShaderParameter(StringNames.Of("rage"), rageGlow);
        edgeGlow.SetShaderParameter(StringNames.Of("heal"), healGlow);
        edgeGlow.SetShaderParameter(StringNames.Of("aspect"), screen.X / Mathf.Max(1f, screen.Y));
        staminaStar.Modulate = staminaStar.Value >= 1f ? Colors.White : new Color(0.75f, 0.75f, 0.8f);
        // The crimson screen grade swallows the grey of the empty part; a dark red keeps how much health is left readable.
        healthGhost.TintUnder = aetheriacActive ? new Color(2.4f, 0.25f, 0.25f) : Colors.White;

        if (aetheriacActive)
        {
            float flicker = 0.75f + 0.25f * Mathf.Sin(time * 14f);
            corruptionStar.Modulate = new Color(1.3f, flicker * 0.5f, flicker * 0.55f);
            corruptionStar.Scale = Vector2.One * (1.05f + 0.05f * Mathf.Sin(time * 9f));
            corruptionHint.Visible = false;
        }
        else
        {
            corruptionStar.Modulate = Colors.White;
            corruptionStar.Scale = Vector2.One;
            corruptionHint.Visible = rageReady;
        }

        if (displayedSouls != targetSouls)
        {
            int step = Mathf.Max(1, Mathf.Abs(targetSouls - displayedSouls) / 8);
            displayedSouls = (int)Mathf.MoveToward(displayedSouls, targetSouls, step);
            soulsLabel.Text = displayedSouls.ToString();
        }

        if (toastTimer > 0f)
        {
            toastTimer -= dt;
            if (toastTimer < 0.6f)
                toast.Modulate = new Color(1, 1, 1, Mathf.Max(0f, toastTimer / 0.6f));
        }

        if (focusPanel.Visible)
        {
            if (focusEntity == null || !IsInstanceValid(focusEntity) || focusEntity.IsDead)
            {
                focusPanel.Visible = false;
                focusEntity = null;
            }
            else
            {
                float ratio = focusEntity.HealthRatio;
                focusFill.Size = new Vector2(198 * ratio, 3);
                focusGhost.Size = new Vector2(Mathf.MoveToward(focusGhost.Size.X, 198 * ratio, dt * 60f), 3);
            }
        }
    }
}
