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
	private PracticeManager  _practice;
	private StarPowerManager _starPower;

	// ── Estado ─────────────────────────────────────────────────────────────
	private bool           _songEnded;
	private int            _nextNoteIndex;
	private List<NoteData> _noteList;
	private SongChart      _chart;
	private Label          _timingLabel;
	private Label          _practiceLabel;
	private double         _songDuration;
	private ProgressBar    _spGauge;
	private Label          _spLabel;
	private int            _lastHitLane = -1;
	private double         _lastHitTime = -1;

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
		_starPower = new StarPowerManager();
		_scoring = new ScoreManager(totalNotes);

		// Practice mode
		if (GameData.IsPracticeMode)
		{
			_practice = new PracticeManager();
			_scoring.NoFail = true;
			if (_audio != null) _audio.PitchScale = _practice.SpeedMultiplier;
			GD.Print("[GameManager] Modo Prática ativo.");
		}

		_clock = new SongTimeClock(TravelTime, AudioLatencyOffset);
		GameData.SongTime = _clock.SongTime;

		// Duração da música para progress e loops
		if (_audio?.Stream != null)
			_songDuration = _audio.Stream.GetLength();
		else if (_noteList.Count > 0)
			_songDuration = _noteList[^1].Time + 2.0;

		// 5. Toca música com delay sincronizado
		if (_audio?.Stream != null)
		{
			double delay = _clock.GetAudioStartDelay(TravelTime);
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

		// 8. Practice HUD
		if (_practice != null && hud != null)
		{
			_timingLabel = new Label
			{
				Name = "TimingLabel",
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			_timingLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
			_timingLabel.OffsetTop    = -100;
			_timingLabel.OffsetBottom = -70;
			_timingLabel.OffsetLeft   = -100;
			_timingLabel.OffsetRight  =  100;
			_timingLabel.AddThemeFontSizeOverride("font_size", 22);
			hud.AddChild(_timingLabel);

			_practiceLabel = new Label
			{
				Name = "PracticeLabel",
				HorizontalAlignment = HorizontalAlignment.Left,
				Text = BuildPracticeHudText(),
			};
			_practiceLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
			_practiceLabel.OffsetLeft   = 16;
			_practiceLabel.OffsetTop    = 100;
			_practiceLabel.OffsetRight  = 500;
			_practiceLabel.OffsetBottom = 160;
			_practiceLabel.AddThemeFontSizeOverride("font_size", 16);
			_practiceLabel.AddThemeColorOverride("font_color", Colors.Yellow);
			hud.AddChild(_practiceLabel);

			// Botões touch para prática (apenas no mobile)
			string os = OS.GetName();
			if (os == "Android" || os == "iOS")
				BuildPracticeTouchButtons(hud);

			// Atualiza hints quando joystick conecta/desconecta
			Input.JoyConnectionChanged += (device, connected) => UpdatePracticeLabel();
		}

		// 9. Star Power gauge HUD
		if (hud != null)
		{
			var spContainer = new HBoxContainer();
			spContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
			spContainer.OffsetLeft   = -260;
			spContainer.OffsetRight  = -10;
			spContainer.OffsetTop    = -40;
			spContainer.OffsetBottom = -10;
			spContainer.AddThemeConstantOverride("separation", 8);

			_spLabel = new Label
			{
				Text = Locale.Tr("STAR_POWER"),
				VerticalAlignment = VerticalAlignment.Center,
			};
			_spLabel.AddThemeFontSizeOverride("font_size", 14);
			_spLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));
			spContainer.AddChild(_spLabel);

			_spGauge = new ProgressBar
			{
				CustomMinimumSize = new Vector2(140, 20),
				MinValue = 0,
				MaxValue = 100,
				Value = 0,
				ShowPercentage = false,
			};
			spContainer.AddChild(_spGauge);
			hud.AddChild(spContainer);
		}

		// Registra input action para Star Power
		const string spAction = "star_power";
		if (!InputMap.HasAction(spAction))
			InputMap.AddAction(spAction);
		if (!InputMap.ActionHasEvent(spAction, new InputEventKey { Keycode = Key.Space }))
			InputMap.ActionAddEvent(spAction, new InputEventKey { Keycode = Key.Space });
		var spGamepad = new InputEventJoypadButton { ButtonIndex = JoyButton.Back };
		if (!InputMap.ActionHasEvent(spAction, spGamepad))
			InputMap.ActionAddEvent(spAction, spGamepad);

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

		float speed = _practice?.SpeedMultiplier ?? 1f;
		_clock.Update(delta * speed, _audio, AudioLatencyOffset);
		GameData.SongTime = _clock.SongTime;

		// Star Power update
		if (_starPower != null)
		{
			bool wasActive = _starPower.IsActive;
			_starPower.Update((float)delta);
			_scoring.BonusMultiplier = _starPower.GetBonusMultiplier();
			if (wasActive && !_starPower.IsActive)
				UpdateStarPowerGlow(false);
		}

		// Practice loop check
		if (_practice != null && _practice.ShouldLoop(_clock.SongTime))
			PerformPracticeLoop();

		SpawnNotes();
		CheckHOPOAutoHit();
		UpdateHUD();
	}

	// ── Practice ───────────────────────────────────────────────────────────

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_songEnded) return;

		// Star Power activation
		if (@event.IsActionPressed("star_power") && _starPower != null)
		{
			if (_starPower.TryActivate())
			{
				ShowFeedback(Locale.Tr("SP_ACTIVE"), new Color(0.4f, 0.8f, 1f));
				UpdateStarPowerGlow(true);
			}
			return;
		}

		if (_practice == null) return;

		// Teclado
		if (@event is InputEventKey ek && ek.Pressed && !ek.Echo)
		{
			switch (ek.Keycode)
			{
				case Key.Key1: OnPracticeCycleSpeed(); break;
				case Key.Key2: OnPracticeLoopStart();  break;
				case Key.Key3: OnPracticeLoopEnd();    break;
				case Key.Key4: OnPracticeClearLoop();  break;
			}
		}

		// Gamepad D-Pad
		if (@event is InputEventJoypadButton jb && jb.Pressed)
		{
			switch (jb.ButtonIndex)
			{
				case JoyButton.DpadUp:    OnPracticeCycleSpeed(); break;
				case JoyButton.DpadLeft:  OnPracticeLoopStart();  break;
				case JoyButton.DpadRight: OnPracticeLoopEnd();    break;
				case JoyButton.DpadDown:  OnPracticeClearLoop();  break;
			}
		}
	}

	private void BuildPracticeTouchButtons(CanvasLayer hud)
	{
		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		vbox.OffsetLeft   = -80;
		vbox.OffsetRight  = -10;
		vbox.OffsetTop    =  10;
		vbox.OffsetBottom = 280;
		vbox.AddThemeConstantOverride("separation", 6);

		var btnSpeed = MakePracticeTouchButton("\u23e9", OnPracticeCycleSpeed);   // ⏩
		var btnStart = MakePracticeTouchButton("[A",     OnPracticeLoopStart);
		var btnEnd   = MakePracticeTouchButton("B]",     OnPracticeLoopEnd);
		var btnClear = MakePracticeTouchButton("\u2716",  OnPracticeClearLoop);   // ✖

		vbox.AddChild(btnSpeed);
		vbox.AddChild(btnStart);
		vbox.AddChild(btnEnd);
		vbox.AddChild(btnClear);
		hud.AddChild(vbox);
	}

	private static Button MakePracticeTouchButton(string text, System.Action callback)
	{
		var btn = new Button
		{
			Text              = text,
			CustomMinimumSize = new Vector2(64, 56),
			FocusMode         = Control.FocusModeEnum.None,
		};
		btn.AddThemeFontSizeOverride("font_size", 22);
		btn.Pressed += callback;
		return btn;
	}

	private void OnPracticeCycleSpeed()
	{
		if (_practice == null) return;
		_practice.CycleSpeed();
		if (_audio != null) _audio.PitchScale = _practice.SpeedMultiplier;
		UpdatePracticeLabel();
	}

	private void OnPracticeLoopStart()
	{
		if (_practice == null) return;
		_practice.SetLoopStart(_clock.SongTime);
		UpdatePracticeLabel();
	}

	private void OnPracticeLoopEnd()
	{
		if (_practice == null) return;
		_practice.SetLoopEnd(_clock.SongTime);
		UpdatePracticeLabel();
	}

	private void OnPracticeClearLoop()
	{
		if (_practice == null) return;
		_practice.ClearLoop();
		UpdatePracticeLabel();
	}

	private void PerformPracticeLoop()
	{
		// Limpa notas ativas das lanes
		foreach (var lane in _lanes)
		{
			if (lane == null) continue;
			// As notas serão refeitas pelo SpawnNotes
		}

		// Reposiciona o note index para o início do loop
		double loopStart = _practice.LoopStart;
		_nextNoteIndex = 0;
		for (int i = 0; i < _noteList.Count; i++)
		{
			if (_noteList[i].Time >= loopStart - TravelTime)
			{
				_nextNoteIndex = i;
				break;
			}
		}

		// Seek audio
		if (_audio != null && _audio.Playing)
			_audio.Seek((float)Mathf.Max(loopStart, 0));

		_clock.SeekTo(loopStart);
		GameData.SongTime = _clock.SongTime;
	}

	private void UpdatePracticeLabel()
	{
		if (_practiceLabel != null)
			_practiceLabel.Text = BuildPracticeHudText();
	}

	private string BuildPracticeHudText()
	{
		if (_practice == null) return "";
		string speed = Locale.Tr("PRACTICE_SPEED", $"{_practice.SpeedMultiplier * 100:F0}");
		string loop = _practice.IsLooping
			? $"Loop: {_practice.LoopStart:F1}s - {_practice.LoopEnd:F1}s"
			: "Loop: --";
		string status = $"{Locale.Tr("PRACTICE_MODE")} | {speed} | {loop}";

		string os = OS.GetName();
		bool isMobile = os == "Android" || os == "iOS";

		// Mobile: sem hints de tecla (usa botões touch)
		if (isMobile)
			return status;

		// Desktop: mostra hints de teclado ou gamepad
		bool hasGamepad = Input.GetConnectedJoypads().Count > 0;
		string hints = hasGamepad
			? "[D-Pad \u2191] Speed  [D-Pad \u2190] Loop A  [D-Pad \u2192] Loop B  [D-Pad \u2193] Clear"
			: "[1] Speed  [2] Loop Start  [3] Loop End  [4] Clear";

		return $"{status}\n{hints}";
	}

	private void CheckHOPOAutoHit()
	{
		if (_lastHitLane < 0) return;

		for (int i = 0; i < _lanes.Length; i++)
		{
			if (_lanes[i] == null || i == _lastHitLane) continue;

			var hopoNote = _lanes[i].GetFirstHOPOInWindow();
			if (hopoNote == null) continue;

			// Verifica se o último hit foi recente o suficiente (dentro de 1 segundo)
			if (_clock.SongTime - _lastHitTime > 1.0) continue;

			hopoNote.Hit();
			// O signal NoteHit vai disparar OnNoteHit via o RegisterNote
		}
	}

	private void UpdateStarPowerGlow(bool active)
	{
		foreach (var lane in _lanes)
		{
			if (lane == null) continue;
			lane.SetStarPowerGlow(active);
		}
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
			Lane       = data.Lane,
			Speed      = NoteSpeed,
			BeatTime   = data.Time,
			IsLong     = data.IsLong,
			Duration   = data.Duration,
			IsStarPower = data.IsStarPower,
			IsHOPO     = data.IsHOPO,
			Position   = new Vector3(LaneConfig.LaneX[data.Lane], 0f, SpawnZ)
		};

		note.SetupVisuals(LaneConfig.LaneColors[data.Lane]);
		AddChild(note);

		_lanes[data.Lane]?.RegisterNote(note);
	}

	// ── Eventos de acerto ──────────────────────────────────────────────────
	private void OnNoteHit(int lane, Note note)
	{
		if (_songEnded) return;

		var (_, label, color, wasHit) = _scoring.ProcessHit(note, GameData.SelectedDifficulty, NoteSpeed);

		// Star Power: nota SP acertada preenche o gauge
		if (wasHit && note.IsStarPower && _starPower != null)
			_starPower.OnStarPowerNoteHit();

		// Rastreia último hit para HOPO chain
		if (wasHit)
		{
			_lastHitLane = lane;
			_lastHitTime = _clock.SongTime;
		}

		ShowFeedback(label, color);

		// Timing feedback (practice mode)
		if (_practice != null && wasHit && _timingLabel != null)
		{
			float signedDist = note.GlobalPosition.Z - Note.HitLineZ;
			float perfectThreshold = 0.025f * NoteSpeed;
			string timing = PracticeManager.GetTimingFeedback(signedDist, perfectThreshold);
			if (!string.IsNullOrEmpty(timing))
			{
				_timingLabel.Text = Locale.Tr(timing);
				_timingLabel.AddThemeColorOverride("font_color",
					timing == "EARLY" ? Colors.Yellow : Colors.Orange);
				var tw = CreateTween();
				tw.TweenProperty(_timingLabel, "modulate:a", 0f, 0.5f).SetDelay(0.3f);
				_timingLabel.Modulate = Colors.White;
			}
			else
			{
				_timingLabel.Text = "";
			}
		}

		if (wasHit)
		{
			_particles.SpawnHitEffect(lane);
			if (note.IsLong) _particles.StartHoldFire(lane);
		}
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
		if (_scoreLabel != null) _scoreLabel.Text = Locale.Tr("SCORE_FMT", _scoring.Score.ToString("N0"));
		if (_comboLabel != null) _comboLabel.Text = _scoring.Combo > 1 ? Locale.Tr("COMBO_FMT", _scoring.Combo) : "";
		if (_multLabel  != null) _multLabel.Text  = _scoring.Multiplier > 1 ? $"{_scoring.Multiplier}x" : "";

		// Star Power gauge
		if (_spGauge != null && _starPower != null)
		{
			_spGauge.Value = _starPower.Gauge * 100f;
			if (_spLabel != null)
			{
				_spLabel.AddThemeColorOverride("font_color",
					_starPower.IsActive ? Colors.White :
					_starPower.CanActivate() ? Colors.Yellow :
					new Color(0.4f, 0.8f, 1f));
			}
		}

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
