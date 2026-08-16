using Godot;

public partial class SettingsMenu : Control
{
	public override void _Ready()
	{
		SettingsManager.Load();

		HSlider volumeSlider = GetNodeOrNull<HSlider>("Tabs/Audio/VolumeSlider");
		CheckButton fullscreenCheck = GetNodeOrNull<CheckButton>("Tabs/Display/FullscreenCheck");
		CheckButton vsyncCheck = GetNodeOrNull<CheckButton>("Tabs/Display/VSyncCheck");

		if (volumeSlider != null)
		{
			volumeSlider.Value = SettingsManager.MasterVolume;
			volumeSlider.ValueChanged += OnMasterVolumeChanged;
		}

		if (fullscreenCheck != null)
		{
			fullscreenCheck.ButtonPressed = SettingsManager.Fullscreen;
			fullscreenCheck.Toggled += OnFullscreenToggled;
		}

		if (vsyncCheck != null)
		{
			vsyncCheck.ButtonPressed = SettingsManager.VSyncEnabled;
			vsyncCheck.Toggled += OnVSyncToggled;
		}
	}

	private void OnMasterVolumeChanged(double value)
	{
		SettingsManager.MasterVolume = (float)value;
		SettingsManager.Apply();
		SettingsManager.Save();
	}

	private void OnFullscreenToggled(bool pressed)
	{
		SettingsManager.Fullscreen = pressed;
		SettingsManager.Apply();
		SettingsManager.Save();
	}

	private void OnVSyncToggled(bool pressed)
	{
		SettingsManager.VSyncEnabled = pressed;
		SettingsManager.Apply();
		SettingsManager.Save();
	}
}
