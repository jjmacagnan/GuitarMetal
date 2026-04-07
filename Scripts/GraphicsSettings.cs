using Godot;

/// <summary>
/// Gerencia e persiste configurações gráficas (fullscreen, FPS limit).
/// Persiste em user://graphics_settings.cfg.
/// </summary>
public static class GraphicsSettings
{
	private const string SavePath = "user://graphics_settings.cfg";

	public static bool Fullscreen { get; set; } = false;
	public static int  MaxFps     { get; set; } = 0;

	/// <summary>Carrega configurações salvas e aplica.</summary>
	public static void Initialize()
	{
		Load();
		Apply();
	}

	public static void Save()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("graphics", "fullscreen", Fullscreen);
		cfg.SetValue("graphics", "max_fps",    MaxFps);
		cfg.Save(SavePath);
	}

	private static void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SavePath) != Error.Ok) return;
		Fullscreen = (bool)cfg.GetValue("graphics", "fullscreen", false);
		MaxFps     = (int)cfg.GetValue("graphics",  "max_fps",    0);
	}

	private static void Apply()
	{
		DisplayServer.WindowSetMode(Fullscreen
			? DisplayServer.WindowMode.ExclusiveFullscreen
			: DisplayServer.WindowMode.Windowed);
		Engine.MaxFps = MaxFps;
	}
}
