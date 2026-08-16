using Godot;

// Standalone Arc-picker screen. Design.md 3.5/3.10: Arc swapping belongs at
// Maldor Arakhan; no world trigger exists yet, so this is opened by the
// ui_weapon_panel keybind until that trigger is built.
public partial class WeaponArcSelectionMenu : Control
{
	private const int PageSize = 12;

	[Export] public PackedScene ItemControlScene;

	private WeaponManager _weaponManager;
	private WeaponArcInventory _inventory;
	private GridContainer _itemContainer;

	public override void _Ready()
	{
		_weaponManager = GetNodeOrNull<WeaponManager>("WeaponManager");
		_inventory = GetNodeOrNull<WeaponArcInventory>("Inventory");
		_itemContainer = GetNodeOrNull<GridContainer>("Frame/ItemContainer");

		GetNodeOrNull<Button>("Frame/CloseButton").Pressed += Close;

		Visible = false;
		SetProcessUnhandledInput(true);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Input.IsActionJustPressed(Keybinds.UiWeaponPanel))
			Toggle();
	}

	public void Open()
	{
		Visible = true;
		Refresh();
	}

	public void Close()
	{
		Visible = false;
	}

	public void Toggle()
	{
		if (Visible) Close();
		else Open();
	}

	private void Refresh()
	{
		if (_itemContainer == null || _inventory == null || _weaponManager == null || ItemControlScene == null)
			return;

		foreach (Node child in _itemContainer.GetChildren())
			child.QueueFree();

		foreach ((PackedScene scene, int index) in _inventory.GetPageItems(0, PageSize))
		{
			Control itemControl = ItemControlScene.Instantiate<Control>();
			_itemContainer.AddChild(itemControl);

			Button button = itemControl.GetNodeOrNull<Button>("Button");
			if (button == null)
				continue;

			button.Text = scene.ResourcePath.GetFile().GetBaseName();
			button.ButtonPressed = _weaponManager.IsSceneEquipped(scene);
			button.ToggleMode = true;

			button.Pressed += () =>
			{
				_weaponManager.EquipWeaponToSlot(scene, index);
				Refresh();
			};
		}
	}
}
