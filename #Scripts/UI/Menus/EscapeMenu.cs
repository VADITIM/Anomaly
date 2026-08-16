using Godot;

public partial class EscapeMenu : Control
{
	private Control _mainPanel;
	private Control _settingsPanel;

	public override void _Ready()
	{
		SetProcessModeRecursive(this);

		_mainPanel = GetNodeOrNull<Control>("MainPanel");
		_settingsPanel = GetNodeOrNull<Control>("SettingsPanel");

		GetNodeOrNull<Button>("MainPanel/Buttons/ResumeButton").Pressed += Close;
		GetNodeOrNull<Button>("MainPanel/Buttons/SettingsButton").Pressed += ShowSettings;
		GetNodeOrNull<Button>("MainPanel/Buttons/QuitButton").Pressed += () => GetTree().Quit();
		GetNodeOrNull<Button>("SettingsPanel/BackButton").Pressed += ShowMain;

		Visible = false;
		ShowMain();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Input.IsActionJustPressed(Keybinds.ToggleEscape))
			Toggle();
	}

	public void Toggle()
	{
		if (Visible) Close();
		else Open();
	}

	public void Open()
	{
		Visible = true;
		ShowMain();
		GetTree().Paused = true;
	}

	public void Close()
	{
		Visible = false;
		GetTree().Paused = false;
	}

	private void ShowMain()
	{
		if (_mainPanel != null) _mainPanel.Visible = true;
		if (_settingsPanel != null) _settingsPanel.Visible = false;
	}

	private void ShowSettings()
	{
		if (_mainPanel != null) _mainPanel.Visible = false;
		if (_settingsPanel != null) _settingsPanel.Visible = true;
	}

	private static void SetProcessModeRecursive(Node node)
	{
		node.ProcessMode = ProcessModeEnum.Always;
		foreach (Node child in node.GetChildren())
			SetProcessModeRecursive(child);
	}
}
