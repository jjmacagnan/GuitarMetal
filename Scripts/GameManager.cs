using Godot;
using System.Collections.Generic;

/// <summary>
/// Controlador principal do jogo — orquestrador.
/// Delega scoring, partículas, pause e timing para componentes dedicados.
/// </summary>
public partial class GameManager : Node3D
{
	[Export] public float NoteSpeed { get; set; } = GameData.DefaultNoteSpeed;
	[Export] public float BPM       { get; set; } = 128f;
	/// <summary>
	/// Compensação manual de latência de áudio em segundos.
	/// Ajuste se as notas aparecerem muito cedo (valor positivo) ou tarde (valor negativo).
	/// Padrão: 0 (usa GetOutputLatency automático).
	/// </summary>
	[Export] public float AudioLatencyOffset { get; set; } = 0f;

	private const float SpawnZ    = -GameData.NoteSpawnDistance;
	private float TravelTime => Mathf.Abs(SpawnZ) / NoteSpeed;

	// ── Nodes ──────────────────────────────────────────────────────────────
	private Lane[]            _lanes;
	private AudioStreamPlayer _audio;
	private Label             _scoreLabel;
	private Label             _comboLabel;
	private Label             _multLabel;
	private Label             _feedbackLabel;
	private Label             _accuracyLabel;
	private Label             _keyHintsLabel;

	// ── Componentes extraídos ──────────────────────────────────────────────
	private ScoreManager     _scoring;
	private SongTimeClock    _clock;
	private HitParticlePool  _particles;
	private PauseController  _pause;

	// ── Estado ─────────────────────────────────────────────────────────────
	private bool           _songEnded;
	private int            _nextNoteIndex;
	private List<NoteData> _noteList;
	private SongChart      _chart;

	// ── _Ready ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		GD.Print("[GameManager] Iniciando...");

		SetupInputMap();
		_audio         = GetNodeOrNull<AudioStreamPlayer>("AudioPlayer");
		_scoreLabel    = GetNodeOrNull<Label>("HUD/ScoreLabel");
		_comboLabel    = GetNodeOrNull<Label>("HUD/ComboLabel");
		_multLabel     = GetNodeOrNull<Label>("HUD/MultLabel");
		_feedbackLabel = GetNodeOrNull<Label>("HUD/FeedbackLabel");
		_accuracyLabel = GetNodeOrNull<Label>("HUD/AccuracyLabel");
		_keyHintsLabel = GetNodeOrNull<Label>("HUD/KeyHints");
		if (_keyHintsLabel != null) _keyHintsLabel.Text = KeybindingStorage.BuildControlsHint(includePauseHint: true);

		// Inicializa lanes
		_lanes = new Lane[LaneConfig.LaneCount];
		for (int i = 0; i < LaneConfig.LaneCount; i++)
		{
			_lanes[i] = GetNodeOrNull<Lane>($"Lanes/Lane{i}");
			if (_lanes[i] == null) { GD.PushError($"Lane{i} não encontrada!"); continue; }

			int captured = i;
			_lanes[i].LaneIndex = i;
			_lanes[i].InputKey  = LaneConfig.LaneKeys[i];
			_lanes[i].LaneColor = LaneConfig.LaneColors[i];
			_lanes[i].ApplyColor();

			_lanes[i].NoteHitInLane      += OnNoteHit;
			_lanes[i].HoldCompleteInLane += OnHoldComplete;
			_lanes[i].NoteMissedInLane   += (_) => OnNoteMiss(captured);
		}

		// BPM é resolvido ANTES de publicar NoteSpeed no GameData
		if (GameData.LoadedBPM > 0) BPM = GameData.LoadedBPM;
		GameData.SetNoteSpeedForRun(NoteSpeed);

		// 1. Usa notas pré-carregadas ou gera fallback
		if (GameData.PreparedNotes != null && GameData.PreparedNotes.Count > 0)
		{
			_noteList = GameData.PreparedNotes;
			GD.Print($"[GameManager] Usando {_noteList.Count} notas pré-geradas.");
		}
		else
		{
			GD.PushWarning("[GameManager] Sem notas pré-geradas — gerando localmente.");
			_chart = new SongChart { BPM = BPM };

			if (_audio != null && _audio.Stream == null)
			{
				var s = GD.Load<AudioStream>(_chart.AudioPath);
				if (s != null) _audio.Stream = s;
			}

			int beats = 64;
			if (_audio?.Stream != null)
			{
				double len = _audio.Stream.GetLength();
				if (len > 0) beats = Mathf.CeilToInt((float)len / (60f / BPM)) + 4;
			}
			_chart.StartOffset = TravelTime;
			_chart.GenerateDemoChart(beats);
			_noteList = new List<NoteData>();
			foreach (var nd in _chart.Notes) _noteList.Add(nd);
			_noteList.Sort((a, b) => a.Time.CompareTo(b.Time));
		}

		// 2. Aplica stream pré-carregado
		if (_audio != null && GameData.LoadedStream != null && _audio.Stream == null)
			_audio.Stream = GameData.LoadedStream;

		int totalNotes = _noteList.Count;
		GD.Print($"[GameManager] {totalNotes} notas, BPM={BPM}");

		// 3. Prepara GameData para o run
		GameData.ResetRun();
		GameData.TotalNotes = totalNotes;

		// 4. Inicializa componentes
		_scoring = new ScoreManager(totalNotes);

		_clock = new SongTimeClock(TravelTime, AudioLatencyOffset);
		GameData.SongTime = _clock.SongTime;

		// 5. Toca música com delay sincronizado
		if (_audio?.Stream != null)
		{
			double delay = _clock.GetAudioStartDelay(TravelTime, AudioLatencyOffset);
			var t = GetTree().CreateTimer(delay);
			t.Timeout += () => { _audio.Play(); };
			double outputLatency = AudioServer.GetOutputLatency() + AudioLatencyOffset;
			GD.Print($"[GameManager] Música em {delay:F2}s | TravelTime={TravelTime:F2}s | Latência={outputLatency*1000:F1}ms (offset={AudioLatencyOffset*1000:F1}ms)");
		}

		// 6. Partículas
		_particles = new HitParticlePool();
		AddChild(_particles);
		_particles.Initialize();

		// 7. Pause
		_pause = new PauseController();
		var hud = GetNodeOrNull<CanvasLayer>("HUD");
		_pause.Initialize(hud, _audio);
		_pause.RestartRequested += OnRestartRequested;
		_pause.QuitRequested    += OnQuitRequested;

		UpdateHUD();
	}

	// ── Input Map ──────────────────────────────────────────────────────────
	private void SetupInputMap()
	{
		KeybindingStorage.ApplyToInputMap();
		GD.Print("[GameManager] InputMap configurado via KeybindingStorage.");

		const string pauseAction = "ui_cancel";
		if (!InputMap.HasAction(pauseAction))
			InputMap.AddAction(pauseAction);

		var evStart = new InputEventJoypadButton { ButtonIndex = JoyButton.Start };
		if (!InputMap.ActionHasEvent(pauseAction, evStart))
			InputMap.ActionAddEvent(pauseAction, evStart);
	}

	// ── Pause callbacks ────────────────────────────────────────────────────
	private void OnRestartRequested()
	{
		if (GameData.AvailableDifficulties != null && GameData.AvailableDifficulties.Count > 1)
			GetTree().ChangeSceneToFile(ScenePaths.DifficultySelect);
		else
			GetTree().ReloadCurrentScene();
	}

	private void OnQuitRequested()
	{
		GetTree().ChangeSceneToFile(ScenePaths.SongSelect);
	}

	// ── _Process ───────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		if (_songEnded || (_pause != null && _pause.IsPaused)) return;

		_clock.Update(delta, _audio, AudioLatencyOffset);
		GameData.SongTime = _clock.SongTime;

		SpawnNotes();
		UpdateHUD();
	}

	// ── Spawn ──────────────────────────────────────────────────────────────
	private void SpawnNotes()
	{
		while (_nextNoteIndex < _noteList.Count)
		{
			var nd = _noteList[_nextNoteIndex];
			if (_clock.SongTime + TravelTime >= nd.Time)
			{
				SpawnNote(nd);
				_nextNoteIndex++;
			}
			else break;
		}

		double lastNoteCutoff = _noteList.Count > 0 ? _noteList[^1].Time + 0.5 : 0;
		if (!_songEnded
			&& _nextNoteIndex >= _noteList.Count
			&& _clock.SongTime > lastNoteCutoff
			&& (_audio == null || !_audio.Playing))
		{
			CallDeferred(nameof(EndSong));
		}
	}

	private void SpawnNote(NoteData data)
	{
		if (data.Lane < 0 || data.Lane >= LaneConfig.LaneCount)
		{
			GD.PushError($"[GameManager] Lane inválida ({data.Lane}) ignorada — verifique o chart.");
			return;
		}

		var note = new Note
		{
			Lane     = data.Lane,
			Speed    = NoteSpeed,
			BeatTime = data.Time,
			IsLong   = data.IsLong,
			Duration = data.Duration,
			Position = new Vector3(LaneConfig.LaneX[data.Lane], 0f, SpawnZ)
		};

		note.SetupVisuals(LaneConfig.LaneColors[data.Lane]);
		AddChild(note);

		_lanes[data.Lane]?.RegisterNote(note);
	}

	// ── Eventos de acerto ──────────────────────────────────────────────────
	private void OnNoteHit(int lane, Note note)
	{
		if (_songEnded) return;

		var (_, label, color) = _scoring.ProcessHit(note, GameData.SelectedDifficulty, NoteSpeed);

		ShowFeedback(label, color);
		_particles.SpawnHitEffect(lane);
		if (note.IsLong) _particles.StartHoldFire(lane);
		CheckSongEnd();
	}

	private void OnHoldComplete(int lane, Note note)
	{
		if (_songEnded) return;

		_scoring.ProcessHoldComplete();

		_particles.StopHoldFire(lane);
		ShowFeedback(Locale.Tr("HOLD"), new Color(0.8f, 0.4f, 1f));
		_particles.SpawnHitEffect(lane);
		CheckSongEnd();
	}

	private void OnNoteMiss(int lane)
	{
		if (_songEnded) return;

		_scoring.ProcessMiss();

		_particles.StopHoldFire(lane);
		ShowFeedback(Locale.Tr("MISS"), Colors.Red);
		CheckSongEnd();
	}

	// ── Fim de jogo ────────────────────────────────────────────────────────
	private void CheckSongEnd()
	{
		if (_scoring.AllResolved)
			CallDeferred(nameof(EndSong));
	}

	private void EndSong()
	{
		if (_songEnded) return;
		_songEnded = true;
		_pause?.SetSongEnded(true);

		_particles.StopAllHoldFire();

		int unresolved = _scoring.FinalizeUnresolved();
		if (unresolved > 0)
			GD.Print($"[GameManager] {unresolved} notas não-resolvidas contadas como miss");

		GameData.Score = _scoring.Score;
		GD.Print($"[GameManager] Fim! Score={_scoring.Score}, Acc={GameData.Accuracy:F1}%, Grade={GameData.Grade}");

		if (_audio != null && _audio.Playing)
		{
			_audio.Finished += ScheduleResultsTransition;
		}
		else
		{
			_audio?.Stop();
			ScheduleResultsTransition();
		}
	}

	private void ScheduleResultsTransition()
	{
		var t = GetTree().CreateTimer(1f);
		t.Timeout += () => GetTree().ChangeSceneToFile(ScenePaths.Results);
	}

	// ── UI ─────────────────────────────────────────────────────────────────
	private void ShowFeedback(string text, Color color)
	{
		if (_feedbackLabel == null) return;
		_feedbackLabel.Text = text;
		_feedbackLabel.AddThemeColorOverride("font_color", color);
		_feedbackLabel.Modulate = Colors.White;
		var tw = CreateTween();
		tw.TweenProperty(_feedbackLabel, "modulate:a", 0f, 0.5f).SetDelay(0.2f);
	}

	private void UpdateHUD()
	{
		if (_scoreLabel != null) _scoreLabel.Text = $"Score: {_scoring.Score:N0}";
		if (_comboLabel != null) _comboLabel.Text = _scoring.Combo > 1 ? Locale.Tr("COMBO_FMT", _scoring.Combo) : "";
		if (_multLabel  != null) _multLabel.Text  = _scoring.Multiplier > 1 ? $"{_scoring.Multiplier}x" : "";

		if (_accuracyLabel != null)
		{
			if (_scoring.ResolvedNotes > 0)
			{
				float acc = Mathf.Min((float)_scoring.NotesHit / _scoring.ResolvedNotes * 100f, 100f);
				_accuracyLabel.Text = $"{acc:F1}%";
				Color col = acc >= 95f ? Colors.Cyan
						  : acc >= 85f ? Colors.LightGreen
						  : acc >= 70f ? Colors.Yellow
						  : acc >= 55f ? new Color(1f, 0.55f, 0f)
						  :              Colors.Red;
				_accuracyLabel.AddThemeColorOverride("font_color", col);
			}
			else
			{
				_accuracyLabel.Text = "--%";
			}
		}
	}
}
