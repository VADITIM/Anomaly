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

        BonfireMenu.Visible = false;
        ConnectPressed(BonfireEquipmentButton);
        ConnectPressed(BonfireEquipmentCloseButton);
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustReleased("Toggle Escape"))
        {
            if (BonfireMenu.Visible)
                DisableAllMenus();
            else
                BonfireMenu.Visible = !BonfireMenu.Visible;
        }
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
