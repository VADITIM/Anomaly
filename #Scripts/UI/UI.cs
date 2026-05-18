using Godot;

public partial class UI : Control
{
    public Player Player;
    [Export] public WeaponMenu WeaponMenu;
    [Export] public Control BonfireMenu;
    [Export] public Button BonfireEquipmentButton;
    [Export] public Button BonfireEquipmentCloseButton;
    [Export] public Label fpsLabel;
    [Export] public Panel WeaponInventoryPanel;
    
    public override void _Ready()
    {
        if (Player == null)
            Player = GetTree().Root.FindChild("Player", true, false) as Player;

        ConnectPressed(BonfireEquipmentButton);
        ConnectPressed(BonfireEquipmentCloseButton);

        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        if (keyEvent.Keycode != Key.Escape && keyEvent.PhysicalKeycode != Key.Escape)
            return;

        if (BonfireMenu.Visible || (WeaponMenu != null && WeaponMenu.Visible))
        {
            DisableAllMenus();
            GetViewport().SetInputAsHandled();
            return;
        }

        // BonfireMenu.Visible = true;
        WeaponMenu.Visible = true;
        GetViewport().SetInputAsHandled();
    }

    private void DisableAllMenus()
    {
        BonfireMenu.Visible = false;
        WeaponMenu.Visible = false;
    }

    public void OnEquipmentButtonDown()
    {
    }

    private void ConnectPressed(Button button)
    {
        if (button == null)
            return;

        Callable callable = Callable.From(OnEquipmentButtonDown);

        if (button.IsConnected(Button.SignalName.Pressed, callable))
            button.Disconnect(Button.SignalName.Pressed, callable);

        button.Connect(Button.SignalName.Pressed, callable);
    }
}
