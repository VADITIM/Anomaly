using System;
using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Maldor's menu. The forge camera holds him in the left fifth of the screen, so this plate takes the right four fifths:
// actions, the Scythe with its source slot, and the Sources that can be dragged onto it. The Scythe is always there; only
// its Source changes. Maldor raises the Scythe's level and the level of the Soul Arc it holds; every stat row shows its
// current value on the left and what the next level adds on the right.
public partial class ForgeMenu : CanvasLayer
{
    private const float POP_TIME = 0.22f;
    private const float POP_FROM = 0.92f;
    private const float ACTIONS_WIDTH = 92f;
    private const float WEAPON_WIDTH = 128f;
    private const float SOURCES_WIDTH = 124f;
    private static readonly Vector2 FramePosition = new(100f, 8f);
    private static readonly Vector2 FrameSize = new(372f, 254f);
    private static readonly Color Heal = new(0.55f, 0.82f, 0.52f);
    private static readonly Color Ember = new(1f, 0.62f, 0.28f);

    private readonly List<Button> buttons = new();
    private GameWorld world;
    private PanelContainer frame;
    private HBoxContainer body;
    private Label soulsLabel;
    private float popTime = POP_TIME;

    public bool IsOpen => Visible;

    private static WeaponStats Weapon => SaveManager.WeaponStats;
    private int EmpowerCost => ArcUpgrades.Cost(world.Player.ArcLevel);

    public void Setup(GameWorld gameWorld) => world = gameWorld;

    public override void _Ready()
    {
        Layer = 30;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        Visible = false;

        var dim = new ColorRect { Color = new Color(0.02f, 0f, 0.05f, 0.3f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.Position = new Vector2(-2000, -2000);
        dim.Size = new Vector2(4480, 4270);
        AddChild(dim);

        frame = new PanelContainer { Position = FramePosition, CustomMinimumSize = FrameSize };
        frame.AddThemeStyleboxOverride("panel", UiFactory.Frame().SetPadding(10f, 8f));
        AddChild(frame);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 3);
        frame.AddChild(layout);

        var header = new HBoxContainer();
        layout.AddChild(header);
        var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 1);
        titles.AddChild(UiFactory.Label("MALDOR ARAKHAN", Art.TitleFont, 12, UiFactory.TextColor, 3));
        header.AddChild(titles);
        var held = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        held.AddThemeConstantOverride("separation", 4);
        soulsLabel = UiFactory.Label("0", Art.NumberFont, 12, UiFactory.TextColor);
        held.AddChild(soulsLabel);
        var glyphSlot = new Control { CustomMinimumSize = new Vector2(14, 14), MouseFilter = Control.MouseFilterEnum.Ignore };
        glyphSlot.AddChild(new SoulGlyph { Position = new Vector2(7, 7) });
        held.AddChild(glyphSlot);
        header.AddChild(held);
        layout.AddChild(UiFactory.Divider());

        body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 4);
        layout.AddChild(body);
    }

    public void Open()
    {
        Rebuild("reinforce");
        Visible = true;
        world.GetTree().Paused = true;
        world.Forge.FocusCamera(true);
        WorldSignals.EmitMenuFocusChanged(true);
        popTime = 0f;
        frame.Scale = Vector2.One * POP_FROM;
        Sfx.Play("ui_confirm", -4f, 0.8f);
    }

    public void Close()
    {
        Visible = false;
        world.GetTree().Paused = false;
        world.Forge.FocusCamera(false);
        WorldSignals.EmitMenuFocusChanged(false);
        Sfx.Play("ui_move", -6f, 0.7f);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible || !(inputEvent.IsActionPressed(GameInput.PAUSE) || inputEvent.IsActionPressed("ui_cancel")))
            return;
        GetViewport().SetInputAsHandled();
        Close();
    }

    // Arrival on back.out over the 220 ms clock, as every menu does.
    public override void _Process(double delta)
    {
        if (!Visible || popTime >= POP_TIME)
            return;
        popTime += Motion.Unscaled(delta);
        frame.Scale = Vector2.One * Mathf.Lerp(POP_FROM, 1f, Motion.BackOut(popTime / POP_TIME));
    }

    private void Rebuild(string focusKey)
    {
        foreach (Node child in body.GetChildren())
        {
            body.RemoveChild(child);
            child.QueueFree();
        }
        buttons.Clear();
        soulsLabel.Text = world.Player.Souls.ToString();
        BuildActions();
        BuildWeapon();
        BuildSources();
        CallDeferred(MethodName.FocusButton, focusKey);
    }

    private void FocusButton(string key)
    {
        Button target = buttons.Find(button => button.Name.ToString() == key && !button.Disabled) ?? buttons.Find(button => !button.Disabled);
        target?.GrabFocus();
        frame.ResetSize();
        frame.PivotOffset = frame.Size * 0.5f;
    }

    private VBoxContainer AddColumn(string title, Color tint, float width)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        card.AddThemeStyleboxOverride("panel", new FrameStyle { Fill = new Color(tint.Darkened(0.85f), 0.7f), Line = new Color(tint, 0.4f), Ornaments = false }.SetPadding(5f, 5f));
        body.AddChild(card);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        card.AddChild(column);
        column.AddChild(UiFactory.Label(title, Art.BodyFont, 6, tint));
        column.AddChild(UiFactory.Divider(false));
        return column;
    }

    private void BuildActions()
    {
        VBoxContainer column = AddColumn("FORGE", UiFactory.Accent, ACTIONS_WIDTH);
        Player player = world.Player;
        bool reinforceOpen = !Weapon.IsMaxLevel && player.Souls >= Weapon.NextLevelCost;
        string reinforceSub = Weapon.IsMaxLevel ? $"Lv {Weapon.Level}   MAX" : $"Lv {Weapon.Level}>{Weapon.Level + 1}   {Weapon.NextLevelCost}";
        Button reinforce = AddAction(column, "reinforce", "Reinforce", Ember, reinforceSub, reinforceOpen ? Ember : UiFactory.Rend, Reinforce);
        reinforce.Disabled = !reinforceOpen;

        SoulWeaponArc arc = player.CurrentArc;
        int arcLevel = player.ArcLevel;
        bool arcMaxed = arcLevel >= ArcUpgrades.MAX_LEVEL;
        bool empowerOpen = !arcMaxed && player.Souls >= EmpowerCost;
        string empowerSub = arcMaxed ? $"Arc Lv {arcLevel}   MAX" : $"Arc Lv {arcLevel}>{arcLevel + 1}   {EmpowerCost}";
        Button empower = AddAction(column, "empower", "Empower", arc.TrailColor, empowerSub, empowerOpen ? arc.TrailColor : UiFactory.Rend, Empower);
        empower.Disabled = !empowerOpen;
        AddAction(column, "leave", "Leave", UiFactory.DimText, "", UiFactory.DimText, Close);
    }

    private Button AddAction(VBoxContainer column, string key, string text, Color tint, string sub, Color subColor, Action pressed)
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

        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        row.AddChild(button);
        if (sub.Length > 0)
        {
            Label cost = UiFactory.Label(sub, Art.BodyFont, 6, subColor);
            cost.HorizontalAlignment = HorizontalAlignment.Center;
            row.AddChild(cost);
        }
        row.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore });
        column.AddChild(row);
        buttons.Add(button);
        return button;
    }

    private void BuildWeapon()
    {
        VBoxContainer column = AddColumn("THE SCYTHE", Ember, WEAPON_WIDTH);
        SoulWeaponArc arc = world.Player.CurrentArc;

        var plate = new PanelContainer { CustomMinimumSize = new Vector2(0, 30) };
        plate.AddThemeStyleboxOverride("panel", new FrameStyle { Fill = new Color(0.02f, 0.01f, 0.03f, 0.8f), Line = new Color(Ember, 0.35f), Ornaments = false }.SetPadding(2f, 2f));
        plate.AddChild(new TextureRect
        {
            Texture = Art.Texture("Weapons/Scythe/SoulScythe.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        column.AddChild(plate);

        column.AddChild(UiFactory.Label("SOURCE", Art.BodyFont, 6, UiFactory.DimText));
        var slot = new SourceSlot { CustomMinimumSize = new Vector2(0, 28), Accepts = id => SaveManager.Progression.UnlockedArcs.Contains(id) };
        slot.Style(arc.TrailColor);
        slot.Dropped += id => Callable.From(() => Equip(id)).CallDeferred();
        var content = Passive(new HBoxContainer());
        content.AddThemeConstantOverride("separation", 4);
        content.AddChild(Icon(arc.Icon, 20f));
        var names = Passive(new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        names.AddThemeConstantOverride("separation", 0);
        names.AddChild(UiFactory.Label(arc.ArcName, Art.BodyFont, 6, arc.TrailColor.Lightened(0.3f)));
        content.AddChild(names);
        slot.AddChild(content);
        column.AddChild(slot);

        Player player = world.Player;
        int level = Weapon.Level;
        bool maxed = Weapon.IsMaxLevel;
        int arcLevel = player.ArcLevel;
        bool arcMaxed = arcLevel >= ArcUpgrades.MAX_LEVEL;
        float strike = DamageScaling.StrikeDamage(level, arcLevel);

        GridContainer weapon = StatTable.Create();
        StatTable.Row(weapon, "Level", level.ToString(), maxed ? StatTable.MAXED : "+1", Ember);
        StatTable.Row(weapon, "Weapon dmg", Weapon.WeaponDamage.ToString("0.0"), maxed ? StatTable.MAXED : StatTable.Gain(WeaponStats.DAMAGE_PER_LEVEL), Ember);
        StatTable.Row(weapon, "Strike dmg", strike.ToString("0.0"),
            maxed ? StatTable.MAXED : StatTable.Gain(DamageScaling.StrikeDamage(level + 1, arcLevel) - strike), Ember);
        StatTable.Row(weapon, "Weakness", $"{arc.MatchedEfficiency * 100f:0}%", "", UiFactory.Accent);
        column.AddChild(weapon);

        column.AddChild(UiFactory.Label($"SOUL ARC  LV {arcLevel}", Art.BodyFont, 6, arc.TrailColor));
        GridContainer arcStats = StatTable.Create();
        float cooldown = ArcUpgrades.Cooldown(arc, arcLevel);
        StatTable.Row(arcStats, "Arc dmg", $"+{(ArcUpgrades.DamageMultiplier(arcLevel) - 1f) * 100f:0}%",
            arcMaxed ? StatTable.MAXED : StatTable.Gain(ArcUpgrades.DAMAGE_PER_LEVEL * 100f, "0", "%"), arc.TrailColor);
        StatTable.Row(arcStats, "Cooldown", $"{cooldown:0.0}s",
            arcMaxed ? StatTable.MAXED : StatTable.Gain(ArcUpgrades.Cooldown(arc, arcLevel + 1) - cooldown, "0.0", "s"), arc.TrailColor);
        StatTable.Row(arcStats, "Tenacity", $"+{(ArcUpgrades.TenacityMultiplier(arcLevel) - 1f) * 100f:0}%",
            arcMaxed ? StatTable.MAXED : StatTable.Gain(ArcUpgrades.TENACITY_PER_LEVEL * 100f, "0", "%"), arc.TrailColor);
        column.AddChild(arcStats);
    }

    private void BuildSources()
    {
        VBoxContainer column = AddColumn("SOURCES", UiFactory.VoiceColor, SOURCES_WIDTH);
        string equipped = world.Player.CurrentArc.ArcId;
        foreach (string id in ArcLibrary.ALL_ARCS)
        {
            SoulWeaponArc arc = ArcLibrary.Get(id);
            bool known = SaveManager.Progression.UnlockedArcs.Contains(id);
            bool active = id == equipped;
            Color tint = known ? arc.TrailColor : UiFactory.DimText;

            var card = new ArcCard { ArcId = id, Locked = !known, Art = known ? arc.Icon : null, Name = "arc_" + id, CustomMinimumSize = new Vector2(0, 28), Disabled = !known };
            UiFactory.Style(card);
            card.CustomMinimumSize = new Vector2(0, 28);
            var face = new FrameStyle { Fill = new Color(0.03f, 0.02f, 0.04f, 0.85f), Line = active ? UiFactory.Accent : new Color(tint, 0.35f), Ornaments = false }.SetPadding(MarkStyle.TEXT_INSET, 2f);
            var focus = new FrameStyle { Fill = new Color(0.1f, 0.04f, 0.06f, 0.95f), Line = UiFactory.Accent, Ornaments = false }.SetPadding(MarkStyle.TEXT_INSET, 2f);
            foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                card.AddThemeStyleboxOverride(state, face);
            card.AddThemeStyleboxOverride("focus", focus);

            var content = Passive(new HBoxContainer());
            content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            content.OffsetLeft = MarkStyle.TEXT_INSET;
            content.AddThemeConstantOverride("separation", 4);
            var icon = Icon(known ? arc.Icon : null, 20f);
            if (!known)
                icon.Modulate = new Color(0f, 0f, 0f, 0.5f);
            content.AddChild(icon);
            var names = Passive(new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            names.AddThemeConstantOverride("separation", 4);
            if (known)
                names.AddChild(UiFactory.Label($"Lv {ArcUpgrades.LevelOf(id)}", Art.BodyFont, 6, active ? UiFactory.Accent : UiFactory.DimText));
            names.AddChild(UiFactory.Label(known ? arc.ArcName : "Unknown Source", Art.BodyFont, 6, known ? UiFactory.TextColor : UiFactory.DimText));
            content.AddChild(names);
            card.AddChild(content);

            if (known)
            {
                card.Pressed += () => Equip(id);
                card.MouseEntered += card.GrabFocus;
            }
            else
                card.FocusMode = Control.FocusModeEnum.None;
            column.AddChild(card);
            if (known)
                buttons.Add(card);
        }
    }

    private static TextureRect Icon(Texture2D texture, float size) => new()
    {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    private static T Passive<T>(T control) where T : Control
    {
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
        return control;
    }

    private void Reinforce()
    {
        Player player = world.Player;
        if (Weapon.IsMaxLevel || !player.SpendSouls(Weapon.NextLevelCost))
            return;
        Weapon.Level++;
        SaveManager.Progression.HeldSouls = player.BankedSouls;
        SaveManager.Save(SavePoint.Maldor);
        Sfx.Play("hit_weak", -4f, 0.8f);
        world.Hud.Toast(Weapon.IsMaxLevel ? "The Scythe can hold no more." : $"The Scythe rises to level {Weapon.Level}.");
        Rebuild("reinforce");
    }

    private void Empower()
    {
        Player player = world.Player;
        if (player.ArcLevel >= ArcUpgrades.MAX_LEVEL || !player.SpendSouls(EmpowerCost))
            return;
        ArcUpgrades.Raise(player.CurrentArc.ArcId);
        SaveManager.Progression.HeldSouls = player.BankedSouls;
        SaveManager.Save(SavePoint.Maldor);
        PlayerSignals.EmitArcChanged(player.CurrentArc);
        Sfx.Play("tenacity_break", -8f, 1.2f);
        world.Hud.Toast($"The {player.CurrentArc.ArcName} deepens to level {player.ArcLevel}.");
        Rebuild("empower");
    }

    private void Equip(string id)
    {
        Player player = world.Player;
        if (player.CurrentArc.ArcId == id || !SaveManager.Progression.UnlockedArcs.Contains(id))
            return;
        player.EquipArc(ArcLibrary.Get(id));
        Sfx.Play("tenacity_break", -8f, 1f);
        world.Hud.Toast($"The Scythe now holds the {player.CurrentArc.ArcName}.");
        SaveManager.Save(SavePoint.Maldor);
        Rebuild("arc_" + id);
    }
}
