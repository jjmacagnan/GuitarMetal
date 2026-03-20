using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Tela de carregamento com máquina de estados.
/// Lê metadados JSON da música (BPM, startOffset) para sincronizar as notas.
/// </summary>
public partial class LoadingScreen : Control
{
	private Label       _songLabel;
	private Label       _statusLabel;
	private Label       _loadingLabel;
	private ProgressBar _progressBar;

	private enum State { Init, RequestAudio, LoadAudio, ReadMetadata, GenerateChart, Ready, Error }
	private State _state = State.Init;

	// Metadados lidos do JSON (ou defaults)
	private float _bpm         = 128f;
	private float _startOffset = 0f;
	private int   _beatCount   = 64;

	// Notas explícitas do JSON (se houver)
	private List<NoteData> _chartNotes = null;

	public override void _Ready()
	{
		_songLabel    = GetNodeOrNull<Label>("VBox/SongLabel");
		_statusLabel  = GetNodeOrNull<Label>("VBox/StatusLabel");
		_loadingLabel = GetNodeOrNull<Label>("VBox/LoadingLabel");
		_progressBar  = GetNodeOrNull<ProgressBar>("VBox/ProgressBar");

		if (_loadingLabel != null) _loadingLabel.Text = Locale.Tr("LOADING");
		if (_songLabel    != null) _songLabel.Text    = GameData.SelectedSongName;
		if (_progressBar  != null) _progressBar.Value = 0;

		_bpm = GameData.LoadedBPM > 0 ? GameData.LoadedBPM : 128f;
		SetStatus(Locale.Tr("LOADING_INIT"), 5);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Permite voltar para a seleção de música em caso de erro (ou a qualquer momento)
		if (_state == State.Error && @event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		switch (_state)
		{
			// ── Etapa 0: inicializa e dispara o carregamento assíncrono ──
			case State.Init:
				SetStatus(Locale.Tr("LOADING_AUDIO"), 5);
				_state = State.RequestAudio;
				break;

			// ── Etapa 1a: enfileira carregamento em background thread ────
			case State.RequestAudio:
			{
				string ap = GameData.SelectedSongPath;
				if (!ResourceLoader.Exists(ap) && !FileAccess.FileExists(ap))
				{
					GD.PushError($"[Loading] Áudio não encontrado: {ap}");
					SetStatus($"Erro: {Locale.Tr("ERR_NOT_FOUND")}\n[ESC para voltar]", 0);
					_state = State.Error;
					break;
				}
				Error reqErr = ResourceLoader.LoadThreadedRequest(ap, "AudioStream");
				if (reqErr != Error.Ok)
				{
					// Fallback síncrono se o threaded request falhar (p.ex. no editor)
					GameData.LoadedStream = GD.Load<AudioStream>(ap);
					if (GameData.LoadedStream == null)
					{
						SetStatus($"Erro: {Locale.Tr("ERR_UNSUPPORTED")}\n[ESC para voltar]", 0);
						_state = State.Error;
					}
					else
					{
						SetStatus(Locale.Tr("LOADING_META"), 30);
						_state = State.ReadMetadata;
					}
					break;
				}
				SetStatus(Locale.Tr("LOADING_AUDIO"), 10);
				_state = State.LoadAudio;
				break;
			}

			// ── Etapa 1b: aguarda o carregamento (poll sem bloquear) ─────
			case State.LoadAudio:
			{
				string ap     = GameData.SelectedSongPath;
				var    status = ResourceLoader.LoadThreadedGetStatus(ap);

				if (status == ResourceLoader.ThreadLoadStatus.InProgress)
				{
					Godot.Collections.Array progress = new();
					ResourceLoader.LoadThreadedGetStatus(ap, progress);
					float pct = progress.Count > 0 ? (float)(double)progress[0] : 0f;
					if (_progressBar != null) _progressBar.Value = 10f + pct * 20f;
					break;
				}

				if (status == ResourceLoader.ThreadLoadStatus.Loaded)
					GameData.LoadedStream = ResourceLoader.LoadThreadedGet(ap) as AudioStream;

				if (GameData.LoadedStream == null)
				{
					bool hasImport = FileAccess.FileExists(ap + ".import");
					string reason  = !hasImport
						? Locale.Tr("ERR_NOT_IMPORTED")
						: Locale.Tr("ERR_UNSUPPORTED");
					GD.PushError($"[Loading] Falha ao carregar áudio ({reason}): {ap}");
					SetStatus($"Erro: {reason}\n[ESC para voltar]", 0);
					_state = State.Error;
					break;
				}

				GD.Print($"[Loading] Áudio carregado: {ap} ({GameData.LoadedStream.GetLength():F1}s)");
				SetStatus(Locale.Tr("LOADING_META"), 30);
				_state = State.ReadMetadata;
				break;
			}

			// ── Etapa 2: lê JSON de metadados (BPM, offset, notas) ──────
			case State.ReadMetadata:
				ReadChartMetadata();

				// TravelTime vem de GameData (fonte única) → evita dessincronização com GameManager.
				float travelTime     = GameData.TravelTime;
				float MinStartOffset = travelTime;
				if (_chartNotes == null && _startOffset < MinStartOffset)
				{
					GD.Print($"[Loading] startOffset {_startOffset:F2}s → ajustando para TravelTime={travelTime:F2}s");
					_startOffset = MinStartOffset;
				}

				// Calcula quantos beats precisamos gerar
				if (_chartNotes == null && GameData.LoadedStream != null)
				{
					double songLen = GameData.LoadedStream.GetLength();
					if (songLen > _startOffset)
					{
						float remaining = (float)(songLen - _startOffset);
						_beatCount = Mathf.CeilToInt(remaining / (60f / _bpm)) + 8;
					}
					GD.Print($"[Loading] BPM={_bpm}, offset={_startOffset:F1}s, {_beatCount} beats");
				}

				SetStatus(Locale.Tr("LOADING_NOTES_FMT", _bpm, _beatCount), 60);
				_state = State.GenerateChart;
				break;

			// ── Etapa 3: gera o chart ────────────────────────────────────
			case State.GenerateChart:
				List<NoteData> notes;

				if (_chartNotes != null && _chartNotes.Count > 0)
				{
					// Usa notas do arquivo JSON
					notes = _chartNotes;
					GD.Print($"[Loading] {notes.Count} notas carregadas do JSON");
				}
				else
				{
					// Gera proceduralmente com BPM e offset corretos
					var chart = new SongChart
					{
						BPM         = _bpm,
						StartOffset = _startOffset
					};
					chart.GenerateDemoChart(_beatCount);

					notes = new List<NoteData>();
					foreach (var nd in chart.Notes) notes.Add(nd);
					notes.Sort((a, b) => a.Time.CompareTo(b.Time));
					GD.Print($"[Loading] {notes.Count} notas geradas proceduralmente");
				}

				GameData.PreparedNotes = notes;
				GameData.LoadedBPM     = _bpm;

				SetStatus(Locale.Tr("LOADING_READY"), 100);
				_state = State.Ready;

				var t = GetTree().CreateTimer(0.6f);
				t.Timeout += () => GetTree().ChangeSceneToFile("res://Scenes/Game.tscn");
				break;

			case State.Ready:
			case State.Error:
				SetProcess(false);
				break;
		}
	}

	// ── Leitor de metadados ───────────────────────────────────────────────

	private void ReadChartMetadata()
	{
		string audioPath = GameData.SelectedSongPath;
		int    lastDot   = audioPath.LastIndexOf('.');
		string basePath  = lastDot >= 0 ? audioPath[..lastDot] : audioPath;
		string dir       = audioPath[..(audioPath.LastIndexOf('/') + 1)];

		// Lê song.ini da pasta (se existir) para nome e delay de áudio
		float iniDelayMs = 0f;
		string iniPath = dir + "song.ini";
		if (FileAccess.FileExists(iniPath))
		{
			var info = SongIniReader.Read(iniPath);
			iniDelayMs = info.DelayMs;
			string displayName = SongIniReader.BuildDisplayName(info, GameData.SelectedSongName);
			if (!string.IsNullOrEmpty(displayName))
				GameData.SelectedSongName = displayName;
		}

		// Prioridade: notes.chart → [nome].chart → .json → notes.mid → procedural
		if (TryLoadDotChart(dir + "notes.chart", iniDelayMs)) return;
		if (TryLoadDotChart(basePath + ".chart", iniDelayMs)) return;
		if (TryLoadJson(basePath + ".json")) return;
		TryLoadMidi(dir + "notes.mid", iniDelayMs);
	}

	private bool TryLoadDotChart(string chartPath, float iniDelayMs = 0f)
	{
		if (!FileAccess.FileExists(chartPath)) return false;

		var imported = ChartImporter.Import(chartPath, GameData.SelectedDifficulty);
		if (imported == null) return false;

		_bpm = imported.BPM;

		// Offset: combina o offset do .chart com o delay do song.ini (ambos podem ser não-zero)
		_startOffset = imported.StartOffset + iniDelayMs / 1000f;

		// Usa o nome do .chart apenas como fallback — song.ini e nome da pasta têm prioridade.
		// GameData.SelectedSongName já vem preenchido pelo SongSelectMenu (pasta ou song.ini),
		// então só sobrescrevemos se ainda estiver vazio para evitar perder "Artista - Título".
		if (!string.IsNullOrEmpty(imported.SongName) && string.IsNullOrEmpty(GameData.SelectedSongName))
			GameData.SelectedSongName = imported.SongName;

		if (imported.Notes.Count > 0)
		{
			_chartNotes = new List<NoteData>();

			// Ajusta o tempo de cada nota se houver diferença entre o offset do chart e o calculado
			float chartOffset      = imported.StartOffset;
			float offsetDifference = _startOffset - chartOffset;

			foreach (var nd in imported.Notes)
			{
				_chartNotes.Add(new NoteData
				{
					Time     = nd.Time + offsetDifference,
					Lane     = nd.Lane,
					IsLong   = nd.IsLong,
					Duration = nd.Duration
				});
			}

			GD.Print($"[Loading] .chart carregado: {_chartNotes.Count} notas, BPM={_bpm}, offset={_startOffset:F3}s (chart offset={chartOffset:F3}s, diff={offsetDifference:F3}s)");
		}
		return true;
	}

	private bool TryLoadJson(string jsonPath)
	{
		if (!FileAccess.FileExists(jsonPath))
		{
			GD.Print($"[Loading] Sem chart JSON: '{jsonPath}'");
			return false;
		}

		using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
		string json = file.GetAsText();

		try
		{
			var doc  = JsonDocument.Parse(json);
			var root = doc.RootElement;

			if (root.TryGetProperty("bpm",         out var bv)) _bpm         = bv.GetSingle();
			if (root.TryGetProperty("startOffset", out var so)) _startOffset = so.GetSingle();
			if (root.TryGetProperty("songName",    out var sn) && !string.IsNullOrEmpty(sn.GetString()))
				GameData.SelectedSongName = sn.GetString();

			if (root.TryGetProperty("notes", out var notesEl) && notesEl.GetArrayLength() > 0)
			{
				_chartNotes = new List<NoteData>();
				foreach (var n in notesEl.EnumerateArray())
				{
					var nd = new NoteData();
					if (n.TryGetProperty("time",     out var t))  nd.Time     = t.GetDouble();
					if (n.TryGetProperty("lane",     out var l))  nd.Lane     = l.GetInt32();
					if (n.TryGetProperty("isLong",   out var il)) nd.IsLong   = il.GetBoolean();
					if (n.TryGetProperty("duration", out var d))  nd.Duration = d.GetSingle();
					_chartNotes.Add(nd);
				}
				_chartNotes.Sort((a, b) => a.Time.CompareTo(b.Time));
			}

			GD.Print($"[Loading] JSON lido: BPM={_bpm}, offset={_startOffset}s" +
					 (_chartNotes != null ? $", {_chartNotes.Count} notas" : ", procedural"));
			return _chartNotes != null;
		}
		catch (System.Exception ex)
		{
			GD.PushError($"[Loading] Erro ao ler JSON: {ex.Message}");
			SetStatus($"Erro ao ler chart JSON:\n{ex.Message}\n[ESC para voltar]", 0);
			_state = State.Error;
			return false;
		}
	}

	private bool TryLoadMidi(string midiPath, float iniDelayMs = 0f)
	{
		if (!FileAccess.FileExists(midiPath))
		{
			GD.Print($"[Loading] Sem MIDI: '{midiPath}' — usando procedural");
			return false;
		}

		var imported = MidiImporter.Import(midiPath, GameData.SelectedDifficulty);
		if (imported == null || imported.Notes.Count == 0)
		{
			GD.PushWarning($"[Loading] MIDI sem notas jogáveis: {midiPath}");
			return false;
		}

		_bpm         = imported.BPM;
		_startOffset = imported.StartOffset + iniDelayMs / 1000f;

		// Mesmo critério do TryLoadDotChart: não sobrescreve nome já resolvido pelo song.ini / pasta.
		if (!string.IsNullOrEmpty(imported.SongName) && imported.SongName != "thefinalcountdown"
		    && string.IsNullOrEmpty(GameData.SelectedSongName))
			GameData.SelectedSongName = imported.SongName;

		_chartNotes = new List<NoteData>();
		float offsetDiff = iniDelayMs / 1000f;

		foreach (var nd in imported.Notes)
		{
			_chartNotes.Add(new NoteData
			{
				Time     = nd.Time + offsetDiff,
				Lane     = nd.Lane,
				IsLong   = nd.IsLong,
				Duration = nd.Duration
			});
		}

		GD.Print($"[Loading] MIDI carregado: {_chartNotes.Count} notas, BPM={_bpm:F1}, offset={_startOffset:F3}s");
		return true;
	}

	private void SetStatus(string text, float progress)
	{
		if (_statusLabel != null) _statusLabel.Text = text;
		if (_progressBar != null)
		{
			var tw = CreateTween();
			tw.TweenProperty(_progressBar, "value", progress, 0.25f);
		}
	}
}
