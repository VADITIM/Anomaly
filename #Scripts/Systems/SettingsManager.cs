using Godot;

public partial class SettingsManager : Node
{
	public static float MasterVolume { get; set; } = 1f;
	public static bool Fullscreen { get; set; } = false;
	public static bool VSyncEnabled { get; set; } = true;

	private const string SavePath = "user://settings.cfg";
	private const string SaveSection = "settings";

	public override void _Ready()
	{
		Load();
		Apply();
	}

	public static void Load()
	{
		ConfigFile config = new();
		if (config.Load(SavePath) != Error.Ok)
			return;

		MasterVolume = (float)config.GetValue(SaveSection, "master_volume", MasterVolume);
		Fullscreen = (bool)config.GetValue(SaveSection, "fullscreen", Fullscreen);
		VSyncEnabled = (bool)config.GetValue(SaveSection, "vsync_enabled", VSyncEnabled);
	}

	public static void Save()
	{
		ConfigFile config = new();
		config.SetValue(SaveSection, "master_volume", MasterVolume);
		config.SetValue(SaveSection, "fullscreen", Fullscreen);
		config.SetValue(SaveSection, "vsync_enabled", VSyncEnabled);
		config.Save(SavePath);
	}

	public static void Apply()
	{
		int masterBus = AudioServer.GetBusIndex("Master");
		if (masterBus >= 0)
			AudioServer.SetBusVolumeDb(masterBus, Mathf.LinearToDb(Mathf.Clamp(MasterVolume, 0.0001f, 1f)));

		DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
		DisplayServer.WindowSetVsyncMode(VSyncEnabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
	}
}
