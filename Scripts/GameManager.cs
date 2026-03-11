using Godot;
using System.Collections.Generic;

/// <summary>
/// Controlador principal do jogo.
/// Usa SongChart para geração do chart, suporta hold notes e detecta fim de jogo.
/// </summary>
public partial class GameManager : Node3D
{
	[Export] public float NoteSpeed { get; set; } = GameData.DefaultNoteSpeed;
	[Export] public float BPM       { get; set; } = 128f;

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

	private double        _songTime;
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
		{ Key.A, Key.S, Key.D, Key.F, Key.Space };

	// Nomes das ações no InputMap (podem ser reconfigurados em Project → Input Map)
	public static readonly string[] LaneActions =
		{ "lane_0", "lane_1", "lane_2", "lane_3", "lane_4" };

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

		// Usa BPM do GameData se disponível (lido do JSON da música)
		if (GameData.LoadedBPM > 0) BPM = GameData.LoadedBPM;

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

		// 4. Tempo inicial: áudio começa após 0.5s de buffer
		//    _songTime acompanha o tempo do áudio:
		//      _songTime = 0  → áudio está em t=0
		//      _songTime < 0  → antes do áudio começar
		const double AudioDelay = 0.5;
		_songTime = -TravelTime - AudioDelay;

		// 5. Toca música com delay sincronizado com _songTime
		if (_audio?.Stream != null)
		{
			double delay = TravelTime + AudioDelay;
			var t = GetTree().CreateTimer(delay);
			t.Timeout += () => { _audio.Play(); };
			GD.Print($"[GameManager] Música toca em {delay:F2}s (TravelTime={TravelTime:F2}s)");
		}

		InitParticlePool();
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
			{
				InputMap.AddAction(action);
				var ev = new InputEventKey { Keycode = LaneKeys[i] };
				InputMap.ActionAddEvent(action, ev);
				GD.Print($"[GameManager] InputMap: '{action}' → {LaneKeys[i]}");
			}
		}
	}

	// ── _Process ───────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		if (_songEnded) return;

		_songTime += delta;
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
	}

	private void SpawnNote(NoteData data)
	{
		var note = new Note
		{
			Lane     = data.Lane,
			Speed    = NoteSpeed,
			BeatTime = data.Time,
			IsLong   = data.IsLong,
			Duration = (float)data.Duration,
			Position = new Vector3(LaneX[data.Lane], 0f, SpawnZ)
		};

		note.SetupVisuals(LaneColors[data.Lane]);
		AddChild(note);

		_lanes[data.Lane]?.RegisterNote(note);
	}

	// ── Eventos de acerto ──────────────────────────────────────────────────
	private void OnNoteHit(int lane, Note note)
	{
		// Apenas notas tap chegam aqui (hold heads são filtradas na Lane)
		_combo++;
		_multiplier = _combo switch { >= 30 => 8, >= 20 => 4, >= 10 => 2, _ => 1 };

		float dist = Mathf.Abs(note.GlobalPosition.Z - Note.HitLineZ);
		int baseScore;
		string label;
		Color  color;

		if      (dist < 0.4f) { baseScore = 100; label = "PERFECT!"; color = Colors.Cyan;   }
		else if (dist < 1.0f) { baseScore =  75; label = "GREAT";    color = Colors.Yellow; }
		else                  { baseScore =  50; label = "GOOD";     color = Colors.White;  }

		_score += baseScore * _multiplier;
		GameData.NotesHit++;
		_resolvedNotes++;

		ShowFeedback(label, color);
		SpawnFireEffect(lane);
		CheckSongEnd();
	}

	private void OnHoldComplete(int lane, Note note)
	{
		_combo++;
		_multiplier = _combo switch { >= 30 => 8, >= 20 => 4, >= 10 => 2, _ => 1 };
		_score += 150 * _multiplier;

		GameData.NotesHit++;
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
