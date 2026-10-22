using System;
using Godot;

namespace Anomaly;

// Opened from one of the status screen's amulet slots. The amulet worn there sits top left with what it adds to each
// stat, as the stat without it > the stat with it. The amulet under the cursor (or focus) sits top right with the
// swap spelled out: each stat it would change, the change, and the stat now > the stat after the swap. Every owned
// amulet waits in the row along the bottom, led by an empty socket that takes the worn one off; pressing one wears it.
public partial class AmuletWindow : CanvasLayer
{
    private const float FADE_TIME = 0.15f;

    private sealed class Section
    {
        public AmuletIcon Icon;
        public Label Name;
        public Label Line;
        public Label Perks;
        public GridContainer Stats;
    }

    private readonly Section equipped = new();
    private readonly Section hovered = new();
    private Control root;
    private Label title;
    private HBoxContainer row;
    private Button template;
    private Player player;
    private int slot;
    private Action changed;
    private Action<bool> closed;
    private bool dirty;
    private float fade;
    private bool closing;

    public bool IsOpen => Visible && !closing;

    // The look lives in AmuletWindow.tscn, laid out as it appears in game with sample amulets and rows. Ready keeps the
    // first slot as the template for the row and frees the samples; the game fills both sections per hover.
    public override void _Ready()
    {
        ViewScale.Fit(this);
        Visible = false;
        root = GetNode<Control>("Root");
        title = GetNode<Label>("Root/Panel/Title");
        Bind(equipped, "Root/Panel/Equipped");
        Bind(hovered, "Root/Panel/Hovered");
        row = GetNode<HBoxContainer>("Root/Panel/Row");
        template = row.GetNode<Button>("Slot");
        row.RemoveChild(template);
        Clear(row);
        GetNode<Control>("Root/Blocker").GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventMouseButton { Pressed: true })
                Close();
        };
    }

    public override void _ExitTree() => template?.Free();

    private void Bind(Section section, string path)
    {
        section.Icon = GetNode<AmuletIcon>(path + "/Icon");
        section.Name = GetNode<Label>(path + "/Name");
        // Each section tints its own name, so it gets its own copy of the shared settings.
        section.Name.LabelSettings = (LabelSettings)section.Name.LabelSettings.Duplicate();
        section.Line = GetNode<Label>(path + "/Line");
        section.Perks = GetNode<Label>(path + "/Perks");
        section.Stats = GetNode<GridContainer>(path + "/Stats");
        Clear(section.Stats);
    }

    private static void Clear(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    // Changed runs after every swap; closed runs once the window shuts, told whether anything was swapped.
    public void Open(Player owner, int slotIndex, Action onChanged, Action<bool> onClosed)
    {
        player = owner;
        slot = slotIndex;
        changed = onChanged;
        closed = onClosed;
        dirty = false;
        closing = false;
        fade = 0f;
        root.Modulate = new Color(1f, 1f, 1f, 0f);
        Visible = true;
        Refresh(Amulets.InSlot(slot)?.Id ?? string.Empty);
        Sfx.Play("ui_confirm", -6f);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        closing = true;
        Sfx.Play("ui_move", -6f, 0.7f);
        closed?.Invoke(dirty);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        float step = Motion.Unscaled(delta);
        fade = closing ? Mathf.Max(0f, fade - step) : Mathf.Min(FADE_TIME, fade + step);
        root.Modulate = new Color(1f, 1f, 1f, fade / FADE_TIME);
        if (closing && fade <= 0f)
        {
            Visible = false;
            closing = false;
        }
    }

    private void Refresh(string focusId)
    {
        title.Text = $"AMULET SLOT {slot + 1}";
        ShowEquipped();
        Clear(row);
        Button focus = AddSlot(null);
        foreach (AmuletInfo amulet in Amulets.Owned)
        {
            Button button = AddSlot(amulet);
            if (amulet.Id == focusId)
                focus = button;
        }
        // Focus stays in the row and wraps at its ends, so a gamepad never wanders onto the screen behind.
        int count = row.GetChildCount();
        for (int i = 0; i < count; i++)
        {
            var button = row.GetChild<Button>(i);
            button.FocusNeighborTop = button.FocusNeighborBottom = button.GetPath();
            button.FocusNeighborLeft = row.GetChild<Button>((i + count - 1) % count).GetPath();
            button.FocusNeighborRight = row.GetChild<Button>((i + 1) % count).GetPath();
        }
        ShowHovered(Amulets.Get(focusId));
        focus.CallDeferred(Control.MethodName.GrabFocus);
    }

    private Button AddSlot(AmuletInfo amulet)
    {
        var button = (Button)template.Duplicate();
        button.Name = amulet?.Id ?? "empty";
        button.FocusMode = Control.FocusModeEnum.All;
        button.GetNode<AmuletIcon>("Icon").Display(amulet);
        // The mark shows where the amulet is already worn: bright for this slot, dim for another.
        int wornAt = amulet == null ? -1 : Array.IndexOf(Amulets.Equipped, amulet.Id);
        var worn = button.GetNode<DiamondMark>("Worn");
        worn.Visible = wornAt >= 0;
        worn.Fill = wornAt == slot ? UiFactory.Accent : UiFactory.DimText;
        button.MouseEntered += button.GrabFocus;
        button.FocusEntered += () =>
        {
            Sfx.Play("ui_move", -8f);
            ShowHovered(amulet);
        };
        button.Pressed += () => Wear(amulet);
        row.AddChild(button);
        return button;
    }

    private void Wear(AmuletInfo amulet)
    {
        string id = amulet?.Id ?? string.Empty;
        if (Amulets.Equipped[slot] == id)
        {
            Sfx.Play("ui_move", -6f, 0.6f);
            return;
        }
        Amulets.Equip(slot, id);
        dirty = true;
        SaveManager.Save(SavePoint.None);
        player.BroadcastAll();
        Sfx.Play("level_up", -10f, 1.4f);
        changed?.Invoke();
        Refresh(id);
    }

    private static void Fill(Section section, AmuletInfo amulet, string emptyName, string emptyLine)
    {
        section.Icon.Display(amulet);
        section.Name.Text = amulet?.Name ?? emptyName;
        section.Name.LabelSettings.FontColor = amulet?.Color ?? UiFactory.DimText;
        section.Line.Text = amulet?.Line ?? emptyLine;
        section.Perks.Text = amulet == null ? string.Empty : Perks(amulet);
        Clear(section.Stats);
    }

    private static string Perks(AmuletInfo amulet)
    {
        var parts = new string[amulet.Stats.Length];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = $"{Amulets.Percent(amulet.Stats[i].Amount)} {Amulets.Label(amulet.Stats[i].Stat)}";
        return string.Join("  ", parts);
    }

    private void ShowEquipped()
    {
        AmuletInfo amulet = Amulets.InSlot(slot);
        Fill(equipped, amulet, "Empty", "Nothing is worn here");
        if (amulet == null)
            return;
        string[] without = Amulets.Swapped(slot, string.Empty);
        string[] with = Amulets.Equipped;
        foreach ((AmuletStat stat, float amount) in amulet.Stats)
            Row(equipped.Stats, stat, amount, Amulets.Effective(player, stat, without), Amulets.Effective(player, stat, with));
    }

    // Every stat the swap moves, counting an amulet that leaves another slot to come here.
    private void ShowHovered(AmuletInfo amulet)
    {
        Fill(hovered, amulet, "Take off", "Leave the slot empty");
        string[] now = Amulets.Equipped;
        string[] after = Amulets.Swapped(slot, amulet?.Id ?? string.Empty);
        bool any = false;
        foreach (AmuletStat stat in Enum.GetValues<AmuletStat>())
        {
            float change = Amulets.Bonus(stat, after) - Amulets.Bonus(stat, now);
            if (Mathf.IsZeroApprox(change))
                continue;
            any = true;
            Row(hovered.Stats, stat, change, Amulets.Effective(player, stat, now), Amulets.Effective(player, stat, after));
        }
        if (!any)
        {
            Label same = UiFactory.Label(now[slot] == (amulet?.Id ?? string.Empty) ? "Worn here already" : "No change", Art.BodyFont, 6, UiFactory.DimText);
            hovered.Stats.AddChild(same);
        }
    }

    private static void Row(GridContainer grid, AmuletStat stat, float change, float from, float to)
    {
        Color tone = Amulets.IsGood(stat, change) ? UiFactory.Accent : UiFactory.Rend;
        Label name = UiFactory.Label(Amulets.Label(stat), Art.BodyFont, 6, UiFactory.DimText);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        grid.AddChild(name);
        Label amount = UiFactory.Label(Amulets.Percent(change), Art.BodyFont, 6, tone);
        amount.CustomMinimumSize = new Vector2(26f, 0f);
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        grid.AddChild(amount);
        Label outcome = UiFactory.Label($"{Amulets.Format(stat, from)} > {Amulets.Format(stat, to)}", Art.BodyFont, 6, UiFactory.TextColor);
        outcome.CustomMinimumSize = new Vector2(52f, 0f);
        outcome.HorizontalAlignment = HorizontalAlignment.Right;
        grid.AddChild(outcome);
    }
}
