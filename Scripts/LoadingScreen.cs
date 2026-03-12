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
	private ProgressBar _progressBar;

	private enum State { Init, LoadAudio, ReadMetadata, GenerateChart, Ready, Error }
	private State _state = State.Init;

	// Metadados lidos do JSON (ou defaults)
	private float _bpm         = 128f;
	private float _startOffset = 0f;
	private int   _beatCount   = 64;

	// Notas explícitas do JSON (se houver)
	private List<NoteData> _chartNotes = null;

	public override void _Ready()
	{
		_songLabel   = GetNodeOrNull<Label>("VBox/SongLabel");
		_statusLabel = GetNodeOrNull<Label>("VBox/StatusLabel");
		_progressBar = GetNodeOrNull<ProgressBar>("VBox/ProgressBar");

		if (_songLabel   != null) _songLabel.Text   = GameData.SelectedSongName;
		if (_progressBar != null) _progressBar.Value = 0;

		_bpm = GameData.LoadedBPM > 0 ? GameData.LoadedBPM : 128f;
		SetStatus("Inicializando...", 5);
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
			// ── Etapa 1: lê o arquivo de áudio ──────────────────────────
			case State.Init:
				SetStatus("Lendo arquivo de áudio...", 10);
				_state = State.LoadAudio;
				break;

			case State.LoadAudio:
				string audioPath = GameData.SelectedSongPath;
				bool fileExists  = FileAccess.FileExists(audioPath);
				bool hasImport   = FileAccess.FileExists(audioPath + ".import");

				var stream = GD.Load<AudioStream>(audioPath);
				GameData.LoadedStream = stream;

				if (stream == null)
				{
					string reason = !fileExists
						? "arquivo não encontrado"
						: !hasImport
							? "não importado (abra o editor Godot para importar)"
							: "formato não suportado (.opus não é aceito — use .ogg/.mp3/.wav)";
					GD.PushError($"[Loading] Falha ao carregar áudio ({reason}): {audioPath}");
					SetStatus($"Erro: {reason}\n[ESC para voltar]", 0);
					_state = State.Error;
					break;
				}

				GD.Print($"[Loading] Áudio carregado: {audioPath} ({stream.GetLength():F1}s)");
				SetStatus("Lendo metadados da música...", 30);
				_state = State.ReadMetadata;
				break;

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

				SetStatus($"Gerando notas (BPM {_bpm}, {_beatCount} beats)...", 60);
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

				SetStatus("Pronto!", 100);
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

		// Prioridade: notes.chart (Clone Hero / Enchor) → [nome].chart → .json → procedural
		if (TryLoadDotChart(dir + "notes.chart", iniDelayMs)) return;
		if (TryLoadDotChart(basePath + ".chart", iniDelayMs)) return;
		TryLoadJson(basePath + ".json");
	}

	private bool TryLoadDotChart(string chartPath, float iniDelayMs = 0f)
	{
		if (!FileAccess.FileExists(chartPath)) return false;

		var imported = ChartImporter.Import(chartPath, GameData.SelectedDifficulty);
		if (imported == null) return false;

		_bpm = imported.BPM;

		// Offset: combina o offset do .chart com o delay do song.ini (ambos podem ser não-zero)
		_startOffset = imported.StartOffset + iniDelayMs / 1000f;

		if (!string.IsNullOrEmpty(imported.SongName))
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

	private void TryLoadJson(string jsonPath)
	{
		if (!FileAccess.FileExists(jsonPath))
		{
			GD.Print($"[Loading] Sem chart para '{jsonPath}' — usando procedural");
			return;
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
		}
		catch (System.Exception ex)
		{
			GD.PushError($"[Loading] Erro ao ler JSON: {ex.Message}");
			// FIX M5: Propaga o erro para o usuário em vez de continuar silenciosamente
			// com valores padrão, o que causaria chart incorreto sem aviso.
			SetStatus($"Erro ao ler chart JSON:\n{ex.Message}\n[ESC para voltar]", 0);
			_state = State.Error;
		}
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
