using Godot;

/// <summary>
/// Gerencia volumes dos buses de áudio (Master, Music, SFX).
/// Cria os buses Music e SFX se não existirem.
/// Persiste em user://audio_settings.cfg.
/// </summary>
public static class AudioSettings
{
	private const string SavePath = "user://audio_settings.cfg";

	// Volumes em dB (range: -30 a 0). -30 tratado como mudo.
	public static float MasterVolumeDb { get; private set; } = 0f;
	public static float MusicVolumeDb  { get; private set; } = 0f;
	public static float SfxVolumeDb    { get; private set; } = 0f;

	private static int _masterBus;
	private static int _musicBus;
	private static int _sfxBus;

	/// <summary>Inicializa buses e carrega configurações salvas.</summary>
	public static void Initialize()
	{
		EnsureBus("Music");
		EnsureBus("SFX");

		_masterBus = AudioServer.GetBusIndex("Master");
		_musicBus  = AudioServer.GetBusIndex("Music");
		_sfxBus    = AudioServer.GetBusIndex("SFX");

		Load();
		ApplyAll();
	}

	public static void SetMasterVolume(float db)
	{
		MasterVolumeDb = db;
		ApplyBus(_masterBus, db);
	}

	public static void SetMusicVolume(float db)
	{
		MusicVolumeDb = db;
		ApplyBus(_musicBus, db);
	}

	public static void SetSfxVolume(float db)
	{
		SfxVolumeDb = db;
		ApplyBus(_sfxBus, db);
	}

	public static void Save()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("audio", "master", MasterVolumeDb);
		cfg.SetValue("audio", "music",  MusicVolumeDb);
		cfg.SetValue("audio", "sfx",    SfxVolumeDb);
		cfg.Save(SavePath);
	}

	private static void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SavePath) != Error.Ok) return;
		MasterVolumeDb = (float)cfg.GetValue("audio", "master", 0f);
		MusicVolumeDb  = (float)cfg.GetValue("audio", "music",  0f);
		SfxVolumeDb    = (float)cfg.GetValue("audio", "sfx",    0f);
	}

	private static void ApplyAll()
	{
		ApplyBus(_masterBus, MasterVolumeDb);
		ApplyBus(_musicBus,  MusicVolumeDb);
		ApplyBus(_sfxBus,    SfxVolumeDb);
	}

	private static void ApplyBus(int idx, float db)
	{
		if (idx < 0) return;
		bool mute = db <= -30f;
		AudioServer.SetBusMute(idx, mute);
		AudioServer.SetBusVolumeDb(idx, mute ? -80f : db);
	}

	private static void EnsureBus(string name)
	{
		if (AudioServer.GetBusIndex(name) >= 0) return;
		int count = AudioServer.BusCount;
		AudioServer.AddBus(count);
		AudioServer.SetBusName(count, name);
		AudioServer.SetBusSend(count, "Master");
	}
}
