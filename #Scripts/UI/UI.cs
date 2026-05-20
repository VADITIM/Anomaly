using Godot;

public partial class UI : Control
{
    public Player Player;
    
    public override void _Ready()
    {
        if (Player == null)
            Player = GetTree().Root.FindChild("Player", true, false) as Player;


        SetProcessUnhandledInput(true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        //     return;

        // if (keyEvent.Keycode == Key.F5 || keyEvent.PhysicalKeycode == Key.F5)
        // {
        //     Enemy.ToggleDebugLabels();
        //     GetViewport().SetInputAsHandled();
        //     return;
        // }

        // if (keyEvent.Keycode != Key.Escape && keyEvent.PhysicalKeycode != Key.Escape)
        //     return;

        // if (BonfireMenu?.Visible || (WeaponMenu != null && WeaponMenu.Visible))
        // {
        //     DisableAllMenus();
        //     GetViewport().SetInputAsHandled();
        //     return;
        // }

        // WeaponMenu?.Visible = true;
        // GetViewport().SetInputAsHandled();
    }

    private void DisableAllMenus()
    {
        // BonfireMenu.Visible = false;
        // WeaponMenu.Visible = false;
    }
}
