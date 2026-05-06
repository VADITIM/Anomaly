using Godot;
using System.Collections.Generic;

public partial class ControlsMenu : Control
{
	private Panel _panel;
	private VBoxContainer _rowsContainer;
	private Label _statusLabel;
	private string _pendingAction;
	private Button _pendingButton;

	public override void _Ready()
	{
		Keybinds.EnsureInitialized();
		Keybinds.LoadBindings();

		_panel = GetNodeOrNull<Panel>("Panel");
		BuildUi();
		RefreshBindingTexts();
		SetProcessUnhandledInput(true);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (string.IsNullOrEmpty(_pendingAction))
			return;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Escape)
			{
				CancelRebind();
				return;
			}

			ApplyBinding(keyEvent);
			return;
		}

		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.Pressed && !mouseButtonEvent.DoubleClick)
		{
			ApplyBinding(mouseButtonEvent);
		}
	}

	private void BuildUi()
	{
		if (_panel == null)
		{
			GD.PushWarning("ControlsMenu needs a Panel child named 'Panel'.");
			return;
		}

		foreach (Node child in _panel.GetChildren())
			child.QueueFree();

		MarginContainer margin = new();
		margin.AnchorRight = 1f;
		margin.AnchorBottom = 1f;
		margin.OffsetLeft = 32f;
		margin.OffsetTop = 32f;
		margin.OffsetRight = -32f;
		margin.OffsetBottom = -32f;
		_panel.AddChild(margin);

		VBoxContainer root = new();
		root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);

		Label title = new();
		title.Text = "Controls";
		title.AddThemeFontSizeOverride("font_size", 44);
		root.AddChild(title);

		_statusLabel = new Label
		{
			Text = "Click a binding, then press a key or mouse button. Esc cancels.",
			AutowrapMode = TextServer.AutowrapMode.Word
		};
		root.AddChild(_statusLabel);

		ScrollContainer scroll = new();
		scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		root.AddChild(scroll);

		_rowsContainer = new VBoxContainer();
		_rowsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_rowsContainer.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_rowsContainer);

		foreach (string action in Keybinds.ActionOrder)
		{
			AddBindingRow(action);
		}

		HBoxContainer footer = new();
		footer.AddThemeConstantOverride("separation", 12);
		root.AddChild(footer);

		Button resetButton = new();
		resetButton.Text = "Reset Defaults";
		resetButton.Pressed += OnResetDefaultsPressed;
		footer.AddChild(resetButton);

		Button saveButton = new();
		saveButton.Text = "Save Now";
		saveButton.Pressed += OnSavePressed;
		footer.AddChild(saveButton);
	}

	private void AddBindingRow(string action)
	{
		HBoxContainer row = new();
		row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddThemeConstantOverride("separation", 12);
		_rowsContainer.AddChild(row);

		Label actionLabel = new();
		actionLabel.Text = Keybinds.GetActionLabel(action);
		actionLabel.CustomMinimumSize = new Vector2(260f, 0f);
		actionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(actionLabel);

		Button bindingButton = new();
		bindingButton.CustomMinimumSize = new Vector2(320f, 0f);
		bindingButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bindingButton.Pressed += () => BeginRebind(action, bindingButton);
		row.AddChild(bindingButton);

		bindingButton.Name = action;
		bindingButton.Text = Keybinds.GetBindingText(action);
	}

	private void RefreshBindingTexts()
	{
		if (_rowsContainer == null)
			return;

		foreach (Node rowNode in _rowsContainer.GetChildren())
		{
			if (rowNode is not HBoxContainer row || row.GetChildCount() < 2)
				continue;

			Button bindingButton = row.GetChildOrNull<Button>(1);
			if (bindingButton != null && !string.IsNullOrEmpty(bindingButton.Name))
				bindingButton.Text = Keybinds.GetBindingText(bindingButton.Name);
		}
	}

	private void BeginRebind(string action, Button button)
	{
		_pendingAction = action;
		_pendingButton = button;
		_pendingButton.Text = "Press a key or button...";
		_statusLabel.Text = $"Rebinding {Keybinds.GetActionLabel(action)}";
	}

	private void CancelRebind()
	{
		if (_pendingButton != null && !string.IsNullOrEmpty(_pendingAction))
			_pendingButton.Text = Keybinds.GetBindingText(_pendingAction);

		_pendingAction = null;
		_pendingButton = null;
		_statusLabel.Text = "Rebinding cancelled.";
	}

	private void ApplyBinding(InputEvent binding)
	{
		if (string.IsNullOrEmpty(_pendingAction))
			return;

		Keybinds.SetActionBinding(_pendingAction, binding);
		_pendingButton.Text = Keybinds.GetBindingText(_pendingAction);
		_pendingAction = null;
		_pendingButton = null;
		_statusLabel.Text = "Binding saved.";
	}

	private void OnResetDefaultsPressed()
	{
		Keybinds.ResetToDefaults();
		RefreshBindingTexts();
		_statusLabel.Text = "Bindings reset to defaults.";
	}

	private void OnSavePressed()
	{
		Keybinds.SaveBindings();
		_statusLabel.Text = "Bindings saved.";
	}
}