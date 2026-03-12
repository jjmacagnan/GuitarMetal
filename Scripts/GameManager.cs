using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Controlador principal do jogo.
/// Usa SongChart para geração do chart, suporta hold notes e detecta fim de jogo.
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

	// Notas surgem em Z=-NoteSpawnDistance e se movem em +Z até a hitline em Z=0
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

	// ── Pool de partículas (uma por lane, reutilizável) ───────────────────
	private GpuParticles3D[] _hitParticles;

	// ── Estado ─────────────────────────────────────────────────────────────
	private int    _score;
	private int    _combo;
	private int    _multiplier   = 1;
	private int    _resolvedNotes;
	private bool   _songEnded;
	private bool   _paused;

	// ── Pause UI ───────────────────────────────────────────────────────
	private Control _pauseOverlay;
	private Button  _pauseResumeButton;

	private double        _songTime;
	private double        _lastRawAudioTime = -1.0; // último valor de GetPlaybackPosition - latência (-1 = não inicializado)
	private int           _nextNoteIndex;
	private List<NoteData> _noteList;
	private int           _totalNotes;
	private SongChart     _chart;

	// ── Cores / Posições (estático) ────────────────────────────────────────
	private static readonly float[] LaneX = { -4f, -2f, 0f, 2f, 4f };

	private static readonly Color[] LaneColors =
	{
		new Color(0.1f,  0.95f, 0.2f),   // Verde
		new Color(0.95f, 0.1f,  0.1f),   // Vermelho
		new Color(1.0f,  0.85f, 0.0f),   // Amarelo
		new Color(0.2f,  0.4f,  1.0f),   // Azul
		new Color(1.0f,  0.5f,  0.0f),   // Laranja
	};

	private static readonly Key[] LaneKeys =
		{ Key.A, Key.S, Key.J, Key.K, Key.L };

	// Nomes das ações no InputMap (podem ser reconfigurados em Project → Input Map)
	public static readonly string[] LaneActions =
		{ "lane_0", "lane_1", "lane_2", "lane_3", "lane_4" };

	// Mapeamento de gamepad por lane
	// Verde=L2, Vermelho=L1, Amarelo=R1, Azul=R2, Laranja=X
	// L2/R2 são eixos (triggers), os demais são botões digitais

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

		// Inicializa lanes
		_lanes = new Lane[5];
		for (int i = 0; i < 5; i++)
		{
			_lanes[i] = GetNodeOrNull<Lane>($"Lanes/Lane{i}");
			if (_lanes[i] == null) { GD.PushError($"Lane{i} não encontrada!"); continue; }

			int captured = i;
			_lanes[i].LaneIndex = i;
			_lanes[i].InputKey  = LaneKeys[i];
			_lanes[i].LaneColor = LaneColors[i];
			_lanes[i].ApplyColor();

			_lanes[i].NoteHitInLane      += OnNoteHit;
			_lanes[i].HoldCompleteInLane += OnHoldComplete;
			_lanes[i].NoteMissedInLane   += (_) => OnNoteMiss(captured);
		}

		// FIX H3: BPM é resolvido ANTES de publicar NoteSpeed no GameData,
		// garantindo que qualquer cálculo baseado em TravelTime use os valores finais.
		if (GameData.LoadedBPM > 0) BPM = GameData.LoadedBPM;

		// Publica a velocidade ativa para que GameData.TravelTime seja consistente
		// com qualquer valor exportado via Inspector (fonte única da verdade).
		GameData.NoteSpeed = NoteSpeed;

		// 1. Usa notas e áudio pré-carregados pelo LoadingScreen
		//    Fallback: gera tudo aqui caso venha direto da cena (debug)
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
			// Offset = TravelTime: primeira nota aparece no topo quando áudio começa
			_chart.StartOffset = TravelTime;
			_chart.GenerateDemoChart(beats);
			_noteList = new List<NoteData>();
			foreach (var nd in _chart.Notes) _noteList.Add(nd);
			_noteList.Sort((a, b) => a.Time.CompareTo(b.Time));
		}

		// 2. Aplica stream pré-carregado no AudioPlayer
		if (_audio != null && GameData.LoadedStream != null && _audio.Stream == null)
			_audio.Stream = GameData.LoadedStream;

		_totalNotes = _noteList.Count;
		GD.Print($"[GameManager] {_totalNotes} notas, BPM={BPM}");

		// 3. Prepara GameData para o run
		GameData.ResetRun();
		GameData.TotalNotes = _totalNotes;

		// 4. Tempo inicial.
		//    _songTime = 0  → áudio está em t=0 (o que o jogador ouve agora)
		//    _songTime < 0  → antes do áudio começar
		//
		//    AudioDelay: buffer mínimo antes do Play() para evitar clique de início.
		//    outputLatency: latência real do driver de áudio (compensa o buffer de saída).
		double outputLatency = AudioServer.GetOutputLatency() + AudioLatencyOffset;
		const double AudioDelay = 0.3;
		_songTime = -TravelTime - AudioDelay - outputLatency;
		GameData.SongTime = _songTime;

		// 5. Toca música com delay sincronizado com _songTime
		if (_audio?.Stream != null)
		{
			double delay = TravelTime + AudioDelay + outputLatency;
			var t = GetTree().CreateTimer(delay);
			t.Timeout += () => { _audio.Play(); };
			GD.Print($"[GameManager] Música em {delay:F2}s | TravelTime={TravelTime:F2}s | Latência={outputLatency*1000:F1}ms (offset={AudioLatencyOffset*1000:F1}ms)");
		}

		InitParticlePool();
		BuildPauseOverlay();
		UpdateHUD();
	}

	// ── Input Map ──────────────────────────────────────────────────────────
	/// <summary>
	/// Registra as ações de lane no InputMap se ainda não existirem.
	/// Ações pré-existentes (configuradas em Project → Input Map) são preservadas.
	/// </summary>
	private void SetupInputMap()
	{
		for (int i = 0; i < 5; i++)
		{
			string action = LaneActions[i];
			if (!InputMap.HasAction(action))
				InputMap.AddAction(action);

			// Teclado — adiciona somente se ainda não mapeado
			var evKey = new InputEventKey { Keycode = LaneKeys[i] };
			if (!InputMap.ActionHasEvent(action, evKey))
			{
				InputMap.ActionAddEvent(action, evKey);
				GD.Print($"[GameManager] InputMap: '{action}' → tecla {LaneKeys[i]}");
			}

				// Gamepad — sempre garante que o evento esteja mapeado
			InputEvent evJoy = i switch
			{
				0 => new InputEventJoypadMotion  { Axis = JoyAxis.TriggerLeft,  AxisValue =  1f }, // L2
				1 => new InputEventJoypadButton  { ButtonIndex = JoyButton.LeftShoulder },          // L1
				2 => new InputEventJoypadButton  { ButtonIndex = JoyButton.RightShoulder },         // R1
				3 => new InputEventJoypadMotion  { Axis = JoyAxis.TriggerRight, AxisValue =  1f }, // R2
				_ => new InputEventJoypadButton  { ButtonIndex = JoyButton.Y },                    // X físico (Switch) = JoyButton.Y no Godot
			};
			if (!InputMap.ActionHasEvent(action, evJoy))
			{
				InputMap.ActionAddEvent(action, evJoy);
				GD.Print($"[GameManager] InputMap: '{action}' → gamepad {evJoy}");
			}
		}

		// Start / Menu / + → pause (adiciona ao ui_cancel existente)
		const string pauseAction = "ui_cancel";
		if (!InputMap.HasAction(pauseAction))
			InputMap.AddAction(pauseAction);

		var evStart = new InputEventJoypadButton { ButtonIndex = JoyButton.Start };
		if (!InputMap.ActionHasEvent(pauseAction, evStart))
			InputMap.ActionAddEvent(pauseAction, evStart);
	}

	// ── Pause ──────────────────────────────────────────────────────────────
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel")) // ESC
		{
			if (_songEnded) return;
			TogglePause();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TogglePause()
	{
		_paused = !_paused;
		GetTree().Paused = _paused;

		if (_paused)
		{
			if (_audio != null) _audio.StreamPaused = true;
			_pauseOverlay?.Show();
			// Foca o botão Continuar para navegação por controle
			_pauseResumeButton?.CallDeferred(Control.MethodName.GrabFocus);
		}
		else
		{
			if (_audio != null) _audio.StreamPaused = false;
			_pauseOverlay?.Hide();
		}
	}

	private void ResumeGame()  => TogglePause();
	private void RestartSong() { GetTree().Paused = false; GetTree().ReloadCurrentScene(); }
	private void QuitToMenu()  { GetTree().Paused = false; GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn"); }

	private void BuildPauseOverlay()
	{
		var hud = GetNodeOrNull<CanvasLayer>("HUD");
		if (hud == null) return;

		_pauseOverlay = new Control();
		_pauseOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_pauseOverlay.ProcessMode = ProcessModeEnum.Always; // funciona mesmo com tree pausada

		// Fundo escuro semi-transparente
		var bg = new ColorRect();
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color(0f, 0f, 0f, 0.75f);
		_pauseOverlay.AddChild(bg);

		// Container central
		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		vbox.OffsetLeft   = -160;
		vbox.OffsetRight  =  160;
		vbox.OffsetTop    = -130;
		vbox.OffsetBottom =  130;
		vbox.AddThemeConstantOverride("separation", 16);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;

		// Título
		var title = new Label { Text = "PAUSADO", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 42);
		title.AddThemeColorOverride("font_color", new Color(0.2f, 0.9f, 1f));
		vbox.AddChild(title);

		// Botões
		var btnResume  = MakePauseButton("Continuar",       ResumeGame);
		var btnRestart = MakePauseButton("Recomeçar",       RestartSong);
		var btnQuit    = MakePauseButton("Sair para Menu",  QuitToMenu);

		vbox.AddChild(btnResume);
		vbox.AddChild(btnRestart);
		vbox.AddChild(btnQuit);

		_pauseResumeButton = btnResume;

		_pauseOverlay.AddChild(vbox);
		_pauseOverlay.Hide();
		hud.AddChild(_pauseOverlay);
	}

	private static Button MakePauseButton(string text, Action callback)
	{
		var btn = new Button
		{
			Text              = text,
			CustomMinimumSize = new Vector2(280, 52),
			ProcessMode       = ProcessModeEnum.Always,
			FocusMode         = Control.FocusModeEnum.All,
		};
		btn.AddThemeFontSizeOverride("font_size", 22);
		btn.Pressed += callback;
		return btn;
	}

	// ── _Process ───────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		if (_songEnded || _paused) return;

		// Avança _songTime por delta a cada frame (suave, sem saltos).
		// Corrige gradualmente em direção ao tempo real do áudio para evitar deriva.
		// Se a diferença for > 100ms, faz snap imediato.
		_songTime += delta;
		if (_audio != null && _audio.Playing)
		{
			double rawTime = _audio.GetPlaybackPosition() - (AudioServer.GetOutputLatency() + AudioLatencyOffset);
			// Ignora leituras idênticas consecutivas (buffer de áudio ainda não atualizou)
			if (Math.Abs(rawTime - _lastRawAudioTime) > 0.0001)
			{
				_lastRawAudioTime = rawTime;
				double drift = rawTime - _songTime;
				// FIX C3: Sufixo 'd' explícito garante comparação double×double sem conversão implícita.
				if (Math.Abs(drift) > 0.05d)
					_songTime = rawTime;                   // drift > 50ms → snap
				else
					_songTime += drift * Math.Min(1.0, delta * 4.0); // correção suave (mais lenta para evitar saltos)
			}
		}

		// Publica o tempo para que as notas calculem sua posição Z
		// diretamente a partir do clock do áudio (sem acúmulo de delta).
		GameData.SongTime = _songTime;

		SpawnNotes();
		UpdateHUD();
	}

	// ── Spawn ──────────────────────────────────────────────────────────────
	private void SpawnNotes()
	{
		while (_nextNoteIndex < _noteList.Count)
		{
			var nd = _noteList[_nextNoteIndex];
			if (_songTime + TravelTime >= nd.Time)
			{
				SpawnNote(nd);
				_nextNoteIndex++;
			}
			else break;
		}

		// Todos os spawns feitos + áudio encerrou → fim de música.
		// FIX C2: Adiciona buffer mínimo de 0.5s após o BeatTime da última nota
		// antes de encerrar, evitando que notas no final da música sejam
		// ignoradas por race condition com o fim do áudio.
		double lastNoteCutoff = _noteList.Count > 0 ? _noteList[^1].Time + 0.5 : 0;
		if (!_songEnded
			&& _nextNoteIndex >= _noteList.Count
			&& _songTime > lastNoteCutoff
			&& (_audio == null || !_audio.Playing))
		{
			CallDeferred(nameof(EndSong));
		}
	}

	private void SpawnNote(NoteData data)
	{
		var note = new Note
		{
			Lane     = data.Lane,
			Speed    = NoteSpeed,
			BeatTime = data.Time,
			IsLong   = data.IsLong,
			Duration = data.Duration,
			Position = new Vector3(LaneX[data.Lane], 0f, SpawnZ)
		};

		note.SetupVisuals(LaneColors[data.Lane]);
		AddChild(note);

		_lanes[data.Lane]?.RegisterNote(note);
	}

	// ── Eventos de acerto ──────────────────────────────────────────────────
	private void OnNoteHit(int lane, Note note)
	{
		if (_songEnded) return;
		_combo++;
		_multiplier = _combo switch { >= 30 => 8, >= 20 => 4, >= 10 => 2, _ => 1 };

		float dist = Mathf.Abs(note.GlobalPosition.Z - Note.HitLineZ);
		int baseScore;
		string label;
		Color  color;

		// Timing windows (in seconds): PERFECT=25ms, GREAT=60ms, GOOD=90ms
		float perfectThreshold = 0.025f * note.Speed;
		float greatThreshold   = 0.06f  * note.Speed;
		// goodThreshold is implicit; anything <= goodThreshold is GOOD

		if      (dist < perfectThreshold) { baseScore = 100; label = "PERFECT!"; color = Colors.Cyan;   }
		else if (dist < greatThreshold)   { baseScore =  75; label = "GREAT";    color = Colors.Yellow; }
		else                               { baseScore =  50; label = "GOOD";     color = Colors.White;  }

		_score += baseScore * _multiplier;
		GameData.NotesHit++;

		// Hold notes: não conta como resolvida agora — será resolvida no HoldComplete ou miss
		if (!note.IsLong)
			_resolvedNotes++;

		ShowFeedback(label, color);
		SpawnFireEffect(lane);
		CheckSongEnd();
	}

	private void OnHoldComplete(int lane, Note note)
	{
		if (_songEnded) return;
		_combo++;
		_multiplier = _combo switch { >= 30 => 8, >= 20 => 4, >= 10 => 2, _ => 1 };
		_score += 150 * _multiplier;

		// NotesHit NÃO é incrementado aqui — já foi contado no OnNoteHit (tap).
		// Incrementar novamente faria accuracy > 100%.
		GameData.HoldsComplete++;
		_resolvedNotes++;

		ShowFeedback("HOLD!", new Color(0.8f, 0.4f, 1f));
		SpawnFireEffect(lane);
		CheckSongEnd();
	}

	// ── Pool de partículas ─────────────────────────────────────────────────
	/// <summary>Cria um GpuParticles3D por lane. Chamado uma vez no _Ready.</summary>
	private void InitParticlePool()
	{
		_hitParticles = new GpuParticles3D[5];
		for (int i = 0; i < 5; i++)
		{
			_hitParticles[i] = BuildFireParticle(LaneColors[i]);
			_hitParticles[i].Position = new Vector3(LaneX[i], 0.4f, Note.HitLineZ);
			_hitParticles[i].Emitting = false;
			AddChild(_hitParticles[i]);
		}
	}

	/// <summary>Dispara o efeito reutilizável da lane (sem alocação por hit).</summary>
	private void SpawnFireEffect(int lane)
	{
		if (_hitParticles == null || lane < 0 || lane >= _hitParticles.Length) return;
		// FIX H2: Verifica IsInstanceValid antes de acessar o nó de partículas,
		// evitando NullReferenceException caso o nó tenha sido liberado.
		if (!IsInstanceValid(_hitParticles[lane])) return;
		_hitParticles[lane].Restart();
	}

	/// <summary>Constrói um GpuParticles3D configurado para a cor da lane.</summary>
	private static GpuParticles3D BuildFireParticle(Color color)
	{
		var mat = new ParticleProcessMaterial();
		mat.Direction          = new Vector3(0f, 1f, 0f);
		mat.Spread             = 45f;
		mat.InitialVelocityMin = 3f;
		mat.InitialVelocityMax = 8f;
		mat.Gravity            = new Vector3(0f, -2f, 0f);
		mat.ScaleMin           = 0.15f;
		mat.ScaleMax           = 0.45f;

		var gradient = new Gradient();
		gradient.Colors  = new Color[]
		{
			color.Lightened(0.4f),
			new Color(Mathf.Max(color.R, 0.9f), color.G * 0.25f, 0f, 0.85f),
			new Color(0.4f, 0f, 0f, 0f)
		};
		gradient.Offsets = new float[] { 0f, 0.5f, 1f };
		mat.ColorRamp = new GradientTexture1D { Gradient = gradient };

		var quadMesh = new QuadMesh { Size = new Vector2(0.45f, 0.45f) };
		quadMesh.Material = new StandardMaterial3D
		{
			ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode          = BaseMaterial3D.BillboardModeEnum.Enabled,
		};

		return new GpuParticles3D
		{
			OneShot          = true,
			Explosiveness    = 0.5f,
			Amount           = 60,
			Lifetime         = 0.8f,
			ProcessMaterial  = mat,
			DrawPass1        = quadMesh,
		};
	}

	private void OnNoteMiss(int lane)
	{
		if (_songEnded) return;
		_combo      = 0;
		_multiplier = 1;
		GameData.NotesMissed++;
		_resolvedNotes++;

		ShowFeedback("MISS", Colors.Red);
		CheckSongEnd();
	}

	// ── Fim de jogo ────────────────────────────────────────────────────────
	private void CheckSongEnd()
	{
		if (_resolvedNotes >= _totalNotes)
			CallDeferred(nameof(EndSong));
	}

	private void EndSong()
	{
		if (_songEnded) return;
		_songEnded = true;

		// Conta notas não-resolvidas como miss para que a acurácia final seja consistente.
		// Isso acontece quando o áudio termina antes de todas as notas passarem pela hitline.
		int unresolved = _totalNotes - _resolvedNotes;
		if (unresolved > 0)
		{
			GameData.NotesMissed += unresolved;
			_resolvedNotes = _totalNotes;
			GD.Print($"[GameManager] {unresolved} notas não-resolvidas contadas como miss");
		}

		GameData.Score = _score;
		GD.Print($"[GameManager] Fim! Score={_score}, Acc={GameData.Accuracy:F1}%, Grade={GameData.Grade}");

		_audio?.Stop();

		// Aguarda 1s e vai para tela de resultados
		var t = GetTree().CreateTimer(1f);
		t.Timeout += () => GetTree().ChangeSceneToFile("res://Scenes/Results.tscn");
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
		if (_scoreLabel != null) _scoreLabel.Text = $"Score: {_score:N0}";
		if (_comboLabel != null) _comboLabel.Text = _combo > 1 ? $"x{_combo} Combo" : "";
		if (_multLabel  != null) _multLabel.Text  = _multiplier > 1 ? $"{_multiplier}x" : "";

		if (_accuracyLabel != null)
		{
			if (_resolvedNotes > 0)
			{
				float acc = (float)GameData.NotesHit / _resolvedNotes * 100f;
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
