using System.Collections.Generic;
using Godot;

namespace Anomaly;

// Shared modal frame: pauses the tree, owns a vertical button list and a description line.
public abstract partial class MenuPanel : CanvasLayer
{
    protected GameWorld World;
    protected VBoxContainer Options;
    protected Label Title;
    protected Label Subtitle;
    protected Label Description;
    private Control dim;
    private PanelContainer panel;
    private readonly List<Button> buttons = new();
    private const float PADDING = 12f;
    private const float POP_TIME = 0.22f;
    private const float POP_FROM = 0.86f;
    private float popTime = POP_TIME;

    public bool IsOpen => Visible;

    public void Setup(GameWorld gameWorld) => World = gameWorld;

    public override void _Ready()
    {
        Layer = 30;
        ProcessMode = ProcessModeEnum.Always;
        ViewScale.Fit(this);
        Visible = false;

        dim = new ColorRect { Color = DimColor, MouseFilter = Control.MouseFilterEnum.Stop };
        dim.Position = new Vector2(-2000, -2000);
        dim.Size = new Vector2(4480, 4270);
        AddChild(dim);

        panel = new PanelContainer { Position = new Vector2(140, 50) };
        panel.AddThemeStyleboxOverride("panel", UiFactory.Frame().SetPadding(PADDING, 9f));
        AddChild(panel);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 2);
        panel.AddChild(layout);

        Title = UiFactory.Label("", Art.TitleFont, 12, UiFactory.TextColor, 3);
        Title.HorizontalAlignment = HorizontalAlignment.Center;
        layout.AddChild(Title);
        Subtitle = UiFactory.Label("", Art.BodyFont, 6, UiFactory.Accent);
        Subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        Subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(Subtitle);
        layout.AddChild(UiFactory.Divider());

        Options = new VBoxContainer();
        Options.AddThemeConstantOverride("separation", 1);
        layout.AddChild(Options);

        layout.AddChild(UiFactory.Divider());
        Description = UiFactory.Label("", Art.BodyFont, 6, UiFactory.DimText);
        Description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        layout.AddChild(Description);
        SetPanelWidth(200f);
    }

    protected Button AddOption(string text, System.Action onPressed, string description)
    {
        Button button = UiFactory.MenuButton(text);
        button.Pressed += () =>
        {
            Sfx.Play("ui_confirm", -4f);
            onPressed();
        };
        return AddRow(button, description);
    }

    protected virtual float RowHeight => 12f;

    protected virtual Vector2 PanelCenter => new(240f, 135f);

    protected virtual Color DimColor => UiFactory.Scrim;

    protected Button AddRow(Button button, string description)
    {
        button.CustomMinimumSize = new Vector2(0, RowHeight);
        button.FocusEntered += () => Description.Text = description;
        button.MouseEntered += button.GrabFocus;
        Options.AddChild(button);
        buttons.Add(button);
        return button;
    }

    protected void ClearOptions()
    {
        foreach (Button button in buttons)
            button.QueueFree();
        buttons.Clear();
    }

    protected virtual void OnOpening() { }

    public void Open()
    {
        OnOpening();
        Rebuild(0);
        Visible = true;
        World.GetTree().Paused = true;
        popTime = 0f;
        panel.Scale = Vector2.One * POP_FROM;
    }

    // Wrapped labels report no width of their own, so they are given the panel's inner width or they wrap per glyph.
    protected void SetPanelWidth(float width)
    {
        panel.CustomMinimumSize = new Vector2(width, 0);
        Subtitle.CustomMinimumSize = new Vector2(width - PADDING * 2f, 0);
        Description.CustomMinimumSize = new Vector2(width - PADDING * 2f, 23);
    }

    protected void HideKeepingPause() => Visible = false;

    public virtual void Close()
    {
        Visible = false;
        World.GetTree().Paused = false;
        Sfx.Play("ui_move", -6f, 0.7f);
    }

    protected void Rebuild(int focusIndex)
    {
        ClearOptions();
        BuildOptions();
        CallDeferred(MethodName.FocusOption, focusIndex);
    }

    private void FocusOption(int index)
    {
        if (buttons.Count == 0)
            return;
        buttons[Mathf.Clamp(index, 0, buttons.Count - 1)].GrabFocus();
        panel.ResetSize();
        panel.Position = PanelCenter - panel.Size * 0.5f;
        panel.PivotOffset = panel.Size * 0.5f;
    }

    // Arrival on back.out over the 220 ms clock: the panel lands from slightly small with one overshoot.
    public override void _Process(double delta)
    {
        if (!Visible || popTime >= POP_TIME)
            return;
        popTime += Motion.Unscaled(delta);
        panel.Scale = Vector2.One * Mathf.Lerp(POP_FROM, 1f, Motion.BackOut(popTime / POP_TIME));
    }

    protected int FocusedIndex()
    {
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].HasFocus())
                return i;
        return 0;
    }

    protected abstract void BuildOptions();

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!Visible)
            return;
        if (inputEvent.IsActionPressed(GameInput.PAUSE) || inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }
}
