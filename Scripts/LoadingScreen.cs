using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de carregamento com máquina de estados.
/// Delega a leitura de charts para ChartLoader.
/// </summary>
public partial class LoadingScreen : Control
{
	private Label       _songLabel;
	private Label       _statusLabel;
	private Label       _loadingLabel;
	private ProgressBar _progressBar;

	private enum State { Init, RequestAudio, LoadAudio, ReadMetadata, GenerateChart, Ready, Error }
	private State _state = State.Init;

	// Metadados lidos pelo ChartLoader (ou defaults)
	private float _bpm         = 128f;
	private float _startOffset = 0f;
	private int   _beatCount   = 64;

	// Notas carregadas pelo ChartLoader
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
		if (_state == State.Error && @event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile(ScenePaths.SongSelect);
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

			// ── Etapa 2: lê chart via ChartLoader ───────────────────────
			case State.ReadMetadata:
			{
				var chartResult = ChartLoader.TryLoadChart(GameData.SelectedSongPath, GameData.SelectedDifficulty);

				if (chartResult != null)
				{
					_bpm         = chartResult.BPM;
					_startOffset = chartResult.StartOffset;
					_chartNotes  = chartResult.Notes;
				}

				float travelTime     = GameData.TravelTime;
				float MinStartOffset = travelTime;
				if (_chartNotes == null && _startOffset < MinStartOffset)
				{
					GD.Print($"[Loading] startOffset {_startOffset:F2}s → ajustando para TravelTime={travelTime:F2}s");
					_startOffset = MinStartOffset;
				}

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
			}

			// ── Etapa 3: gera o chart (ou usa o carregado) ──────────────
			case State.GenerateChart:
			{
				List<NoteData> notes;

				if (_chartNotes != null && _chartNotes.Count > 0)
				{
					notes = _chartNotes;
					GD.Print($"[Loading] {notes.Count} notas carregadas do chart");
				}
				else
				{
					notes = ChartLoader.GenerateProceduralChart(_bpm, _startOffset, _beatCount);
					GD.Print($"[Loading] {notes.Count} notas geradas proceduralmente");
				}

				GameData.PreparedNotes = notes;
				GameData.LoadedBPM     = _bpm;

				SetStatus(Locale.Tr("LOADING_READY"), 100);
				_state = State.Ready;

				var t = GetTree().CreateTimer(0.6f);
				t.Timeout += () => GetTree().ChangeSceneToFile(ScenePaths.Game);
				break;
			}

			case State.Ready:
			case State.Error:
				SetProcess(false);
				break;
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
