using Godot;
using System.Collections.Generic;

public partial class Lane : Node3D
{
	[Export] public int   LaneIndex { get; set; } = 0;
	[Export] public Key   InputKey  { get; set; } = Key.A;
	[Export] public Color LaneColor { get; set; } = Colors.Green;

	// ── Meshes da pista ────────────────────────────────────────────────────
	private MeshInstance3D     _trackMesh;
	private StandardMaterial3D _trackMat;

	// ── Botão: anel externo (base) + dome (cap estilo GH) ─────────────────
	private MeshInstance3D     _buttonBaseMesh;   // anel externo, plano
	private StandardMaterial3D _buttonBaseMat;
	private MeshInstance3D     _buttonCapMesh;    // dome esférico central
	private StandardMaterial3D _buttonCapMat;

	// ── Indicador de hitline ───────────────────────────────────────────────
	private MeshInstance3D     _hitZoneMesh;
	private StandardMaterial3D _hitZoneMat;

	// ── SFX ────────────────────────────────────────────────────────────
	private AudioStreamPlayer _missSfx;

	// ── Notas ──────────────────────────────────────────────────────────────
	private readonly List<Note> _activeNotes     = new();
	private readonly List<Note> _noteSnapshot    = new();  // cópia reutilizável para iteração segura
	private Note                _currentHoldNote = null;
	private bool                _wasHolding;
	private bool                _wasNoteInWindow;

	// ── Touch zones (calculadas a partir da projeção 3D→2D da câmera) ──────
	// Armazena os limites em pixels da zona de toque desta lane.
	// Calculados uma vez na primeira interação (câmera já ativa na cena).
	private float _touchLeftBound   = 0f;
	private float _touchRightBound  = float.MaxValue;
	private bool  _touchBoundsReady = false;
	private int   _holdTouchIndex   = -1;  // dedo que iniciou o hold (multi-touch safety)

	[Signal] public delegate void NoteHitInLaneEventHandler(int lane, Note note);
	[Signal] public delegate void HoldCompleteInLaneEventHandler(int lane, Note note);
	[Signal] public delegate void NoteMissedInLaneEventHandler(int lane);

	public override void _Ready()
	{
		// 1. Pista (Track) — cobre de Z=+10 (câmera) até Z=-60 (spawn)
		_trackMesh = GetNodeOrNull<MeshInstance3D>("Track");
		if (_trackMesh == null)
		{
			_trackMesh = new MeshInstance3D { Name = "Track" };
			_trackMesh.Mesh     = new BoxMesh { Size = new Vector3(1.8f, 0.05f, 70f) };
			_trackMesh.Position = new Vector3(0f, -0.05f, -25f);
			_trackMat = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
			_trackMesh.MaterialOverride = _trackMat;
			AddChild(_trackMesh);
		}
		else { _trackMat = _trackMesh.MaterialOverride as StandardMaterial3D; }

		// 2. Base do botão — disco largo e plano (anel externo visível de cima)
		_buttonBaseMesh = GetNodeOrNull<MeshInstance3D>("ButtonBase");
		if (_buttonBaseMesh == null)
		{
			_buttonBaseMesh = new MeshInstance3D { Name = "ButtonBase" };
			_buttonBaseMesh.Mesh = new CylinderMesh
			{
				TopRadius    = 0.88f,
				BottomRadius = 0.88f,
				Height       = 0.08f,
				RadialSegments = 24
			};
			_buttonBaseMesh.Position = new Vector3(0f, 0.04f, 0f);
			AddChild(_buttonBaseMesh);
		}
		_buttonBaseMat = new StandardMaterial3D
		{
			AlbedoColor              = Colors.Gray,
			EmissionEnabled          = true,
			Emission                 = Colors.Gray,
			EmissionEnergyMultiplier = 0.5f
		};
		_buttonBaseMesh.MaterialOverride = _buttonBaseMat;

		// 3. Cap do botão — dome esférico estilo Guitar Metal
		_buttonCapMesh = GetNodeOrNull<MeshInstance3D>("ButtonCap");
		if (_buttonCapMesh == null)
		{
			_buttonCapMesh = new MeshInstance3D { Name = "ButtonCap" };
			_buttonCapMesh.Mesh = new SphereMesh
			{
				Radius         = 0.52f,
				Height         = 1.04f,
				RadialSegments = 24,
				Rings          = 12
			};
			_buttonCapMesh.Position = new Vector3(0f, 0.10f, 0f);
			AddChild(_buttonCapMesh);
		}
		_buttonCapMat = new StandardMaterial3D
		{
			AlbedoColor              = Colors.White,
			EmissionEnabled          = true,
			Emission                 = Colors.White,
			EmissionEnergyMultiplier = 1.0f
		};
		_buttonCapMesh.MaterialOverride = _buttonCapMat;

		// 4. HitZone marker — barra brilhante na hitline
		_hitZoneMesh = GetNodeOrNull<MeshInstance3D>("HitZoneMarker");
		if (_hitZoneMesh == null)
		{
			_hitZoneMesh = new MeshInstance3D { Name = "HitZoneMarker" };
			_hitZoneMesh.Mesh     = new BoxMesh { Size = new Vector3(1.85f, 0.06f, 0.28f) };
			_hitZoneMesh.Position = new Vector3(0f, 0.03f, -0.8f);
			_hitZoneMat = new StandardMaterial3D
			{
				AlbedoColor              = Colors.Cyan,
				EmissionEnabled          = true,
				Emission                 = Colors.Cyan,
				EmissionEnergyMultiplier = 2.0f
			};
			_hitZoneMesh.MaterialOverride = _hitZoneMat;
			AddChild(_hitZoneMesh);
		}
		else { _hitZoneMat = _hitZoneMesh.MaterialOverride as StandardMaterial3D; }

		// 5. SFX de miss (tecla sem nota)
		_missSfx = new AudioStreamPlayer { Name = "MissSfx" };
		var sfxStream = GD.Load<AudioStream>("res://SFX/Caixa 1.mp3");
		if (sfxStream != null)
		{
			_missSfx.Stream = sfxStream;
			_missSfx.VolumeDb = -6f;
		}
		AddChild(_missSfx);

		ApplyColor();
	}

	public void ApplyColor()
	{
		// Base: cor um pouco mais escura, emisssão sutil
		if (_buttonBaseMat != null)
		{
			_buttonBaseMat.AlbedoColor              = LaneColor.Darkened(0.3f);
			_buttonBaseMat.EmissionEnabled          = true;
			_buttonBaseMat.Emission                 = LaneColor * 0.5f;
			_buttonBaseMat.EmissionEnergyMultiplier = 1.2f;
		}

		// Cap: cor viva + brilho forte (elemento principal do botão)
		if (_buttonCapMat != null)
		{
			_buttonCapMat.AlbedoColor              = LaneColor;
			_buttonCapMat.EmissionEnabled          = true;
			_buttonCapMat.Emission                 = LaneColor * 1.0f;
			_buttonCapMat.EmissionEnergyMultiplier = 2.0f;
		}

		if (_trackMat != null)
		{
			_trackMat.AlbedoColor  = new Color(LaneColor.R * 0.12f, LaneColor.G * 0.12f, LaneColor.B * 0.12f, 0.85f);
			_trackMat.EmissionEnabled = true;
			_trackMat.Emission     = LaneColor * 0.10f;
		}

		if (_hitZoneMat != null)
		{
			_hitZoneMat.AlbedoColor              = LaneColor;
			_hitZoneMat.EmissionEnabled          = true;
			_hitZoneMat.Emission                 = LaneColor * 2.5f;
			_hitZoneMat.EmissionEnergyMultiplier = 3.0f;
		}

	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsEcho()) return;

		// ── Touch input (Android / iOS) ───────────────────────────────────
		// As zonas de toque são calculadas projetando a posição 3D de cada
		// botão na tela via Camera3D.UnprojectPosition(), garantindo alinhamento
		// visual exato independente da perspectiva da câmera.
		if (@event is InputEventScreenTouch evTouch)
		{
			Vector2 viewSize = GetViewport().GetVisibleRect().Size;
			// Ignora toques na faixa superior do ecrã (HUD / botão de pausa)
			if (evTouch.Position.Y < viewSize.Y * 0.25f) return;

			// Calcula os limites de zona a partir da projeção da câmera (uma vez)
			EnsureTouchBounds(viewSize);

			float touchX = evTouch.Position.X;
			if (touchX < _touchLeftBound || touchX >= _touchRightBound) return;

			if (evTouch.Pressed)
			{
				if (_holdTouchIndex < 0) _holdTouchIndex = evTouch.Index;
				OnKeyPressed();
			}
			else
			{
				// Só libera hold se for o mesmo dedo que iniciou o toque
				if (evTouch.Index == _holdTouchIndex)
				{
					_holdTouchIndex = -1;
					OnKeyReleased();
				}
			}

			GetViewport().SetInputAsHandled();
			return;
		}

		// ── Teclado / Gamepad ─────────────────────────────────────────────
		string action = $"lane_{LaneIndex}";
		if (!InputMap.HasAction(action)) return;

		if (@event.IsActionPressed(action))
		{
			OnKeyPressed();
			// FIX H1: Marca o input como tratado para evitar que o mesmo keypress
			// seja processado por outras lanes (input bleeding entre pistas).
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionReleased(action))
		{
			OnKeyReleased();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnKeyPressed()
	{
		// Brilho instantâneo ao pressionar (apaga ao soltar em OnKeyReleased)
		if (_buttonCapMat != null)
		{
			_buttonCapMat.Emission = LaneColor * 6f;
			_buttonCapMat.EmissionEnergyMultiplier = 5.0f;
		}
		if (_buttonBaseMat != null)
		{
			_buttonBaseMat.Emission = LaneColor * 3.5f;
			_buttonBaseMat.EmissionEnergyMultiplier = 3.0f;
		}
		if (_hitZoneMat != null)
		{
			_hitZoneMat.Emission = LaneColor * 8f;
			_hitZoneMat.EmissionEnergyMultiplier = 7.0f;
		}

		// Hit detection — usa snapshot para evitar InvalidOperationException
		// caso sinais de hit/miss modifiquem _activeNotes durante a iteração.
		Note closest    = null;
		float closestDist = float.MaxValue;
		_noteSnapshot.Clear();
		_noteSnapshot.AddRange(_activeNotes);
		foreach (var n in _noteSnapshot)
		{
			if (!IsInstanceValid(n) || n.WasHit || n.Missed || !n.IsInHitWindow()) continue;
			float d = Mathf.Abs(n.GlobalPosition.Z - Note.HitLineZ);
			if (d < closestDist) { closestDist = d; closest = n; }
		}

		if (closest != null)
		{
			closest.Hit();
			// Emite para tap E hold — GameManager dá feedback visual (PERFECT/GREAT/GOOD)
			EmitSignal(SignalName.NoteHitInLane, LaneIndex, closest);
		}
		else
		{
			// Tecla pressionada sem nota na janela de acerto → toca SFX de erro
			if (GameData.MissSfxEnabled && _missSfx?.Stream != null) _missSfx.Play();
		}
	}

	private void OnKeyReleased()
	{
		if (_currentHoldNote != null && IsInstanceValid(_currentHoldNote))
			_currentHoldNote.ReleaseHold();
		_currentHoldNote = null;

		// Restaura emissão padrão (valores de ApplyColor)
		if (_buttonCapMat != null)
		{
			_buttonCapMat.Emission = LaneColor * 1.0f;
			_buttonCapMat.EmissionEnergyMultiplier = 2.0f;
		}
		if (_buttonBaseMat != null)
		{
			_buttonBaseMat.Emission = LaneColor * 0.5f;
			_buttonBaseMat.EmissionEnergyMultiplier = 1.2f;
		}
		if (_hitZoneMat != null)
		{
			_hitZoneMat.Emission = LaneColor * 2.5f;
			_hitZoneMat.EmissionEnergyMultiplier = 3.0f;
		}
	}

	public override void _Process(double delta)
	{
		bool isHolding = _currentHoldNote != null && IsInstanceValid(_currentHoldNote);

		// Verifica se alguma nota está na janela de acerto (iteração segura)
		bool noteInWindow = false;
		if (!isHolding)
		{
			for (int i = _activeNotes.Count - 1; i >= 0; i--)
			{
				var n = _activeNotes[i];
				if (IsInstanceValid(n) && n.IsInHitWindow()) { noteInWindow = true; break; }
			}
		}

		if (isHolding)
		{
			// Pulso lento e profundo de hold (visual contínuo forte)
			float holdPulse = (Mathf.Sin((float)Time.GetTicksMsec() * 0.001f * Mathf.Pi * 3f) + 1f) * 0.5f;
			if (_hitZoneMat   != null) _hitZoneMat.EmissionEnergyMultiplier   = Mathf.Lerp(3.5f, 9.0f, holdPulse);
			if (_buttonCapMat != null) _buttonCapMat.EmissionEnergyMultiplier = Mathf.Lerp(2.5f, 7.0f, holdPulse);
			if (_buttonBaseMat != null) _buttonBaseMat.EmissionEnergyMultiplier = Mathf.Lerp(1.5f, 4.0f, holdPulse);
		}
		else if (noteInWindow)
		{
			// Pulso rápido: "momento para clicar"
			float readyPulse = (Mathf.Sin((float)Time.GetTicksMsec() * 0.001f * Mathf.Pi * 8f) + 1f) * 0.5f;
			if (_hitZoneMat   != null) _hitZoneMat.EmissionEnergyMultiplier   = Mathf.Lerp(3.0f, 8.0f, readyPulse);
			if (_buttonCapMat != null) _buttonCapMat.EmissionEnergyMultiplier = Mathf.Lerp(2.0f, 6.0f, readyPulse);
		}
		else if (_wasHolding || _wasNoteInWindow)
		{
			// Restaura valores padrão definidos em ApplyColor
			if (_hitZoneMat   != null) _hitZoneMat.EmissionEnergyMultiplier   = 3.0f;
			if (_buttonCapMat != null) _buttonCapMat.EmissionEnergyMultiplier = 2.0f;
			if (_buttonBaseMat != null) _buttonBaseMat.EmissionEnergyMultiplier = 1.2f;
		}

		_wasHolding      = isHolding;
		_wasNoteInWindow = noteInWindow;
	}

	public void RegisterNote(Note note)
	{
		_activeNotes.Add(note);

		// FIX C1: Usa delegates nomeados para poder desinscrever após o primeiro disparo,
		// evitando memory leak causado por lambdas que nunca eram removidas dos eventos.
		Note.NoteHitEventHandler      onHit         = null;
		Note.NoteMissedEventHandler   onMissed      = null;
		Note.HoldCompleteEventHandler onHoldComplete = null;

		onHit = (n) =>
		{
			// Sempre desinscreve o próprio handler de hit (já disparou).
			note.NoteHit -= onHit;

			// Para notas normais (tap): desinscreve tudo — não haverá mais sinais.
			// Para hold notes: mantém NoteMissed e HoldComplete ativos, pois o hold
			// ainda precisa ser segurado até o fim (ou pode ser solto antes → miss).
			// REGRESSÃO C1: desinscr-ever HoldComplete aqui fazia holds nunca serem contados.
			if (!n.IsLong)
			{
				note.NoteMissed   -= onMissed;
				note.HoldComplete -= onHoldComplete;
			}

			_activeNotes.Remove(n);
			if (n.IsLong) _currentHoldNote = n;
		};

		onMissed = (n) =>
		{
			note.NoteHit          -= onHit;
			note.NoteMissed       -= onMissed;
			note.HoldComplete     -= onHoldComplete;
			_activeNotes.Remove(n);
			if (_currentHoldNote == n)
			{
				_currentHoldNote = null;
				_holdTouchIndex  = -1;
				ShowReleasePenalty();
			}
			EmitSignal(SignalName.NoteMissedInLane, LaneIndex);
		};

		onHoldComplete = (n) =>
		{
			note.NoteHit          -= onHit;
			note.NoteMissed       -= onMissed;
			note.HoldComplete     -= onHoldComplete;
			if (_currentHoldNote == n)
			{
				_currentHoldNote = null;
				_holdTouchIndex  = -1;
			}
			EmitSignal(SignalName.HoldCompleteInLane, LaneIndex, n);
		};

		note.NoteHit          += onHit;
		note.NoteMissed       += onMissed;
		note.HoldComplete     += onHoldComplete;
	}

	/// <summary>
	/// Calcula os limites em pixels (X) da zona de toque desta lane, projetando
	/// a posição 3D do botão na tela via Camera3D. Executado apenas uma vez;
	/// resultado cacheado em _touchLeftBound/_touchRightBound.
	/// </summary>
	private void EnsureTouchBounds(Vector2 viewSize)
	{
		if (_touchBoundsReady) return;

		var camera = GetViewport()?.GetCamera3D();
		if (camera == null) return;   // câmera ainda não ativa — tenta no próximo toque

		// Ponto de referência: cap do botão desta lane (coordenadas mundo)
		// GlobalPosition.X já é o X desta lane; Y/Z correspondem ao botão visível.
		Vector3 myBtn = GlobalPosition + new Vector3(0f, 0.10f, -0.8f);
		float   myX   = camera.UnprojectPosition(myBtn).X;

		// Espaçamento entre lanes no espaço mundo (LaneX = {-4,-2,0,2,4} → Δ = 2)
		const float LaneSpacing = 2f;

		_touchLeftBound  = 0f;
		_touchRightBound = viewSize.X;

		if (LaneIndex > 0)
		{
			float leftX = camera.UnprojectPosition(myBtn + new Vector3(-LaneSpacing, 0f, 0f)).X;
			_touchLeftBound = (myX + leftX) * 0.5f;
		}
		if (LaneIndex < 4)
		{
			float rightX = camera.UnprojectPosition(myBtn + new Vector3(LaneSpacing, 0f, 0f)).X;
			_touchRightBound = (myX + rightX) * 0.5f;
		}

		_touchBoundsReady = true;
		GD.Print($"[Lane{LaneIndex}] TouchZone: {_touchLeftBound:F0}–{_touchRightBound:F0}px (tela {viewSize.X:F0}px)");
	}

	/// <summary>Remove todas as notas ativas desta lane (usado no loop de prática).</summary>
	public void ClearActiveNotes()
	{
		foreach (var n in _activeNotes)
		{
			if (IsInstanceValid(n)) n.QueueFree();
		}
		_activeNotes.Clear();
		_currentHoldNote = null;
	}

	/// <summary>Retorna a primeira nota HOPO na janela de acerto, ou null.</summary>
	public Note GetFirstHOPOInWindow()
	{
		Note closest = null;
		float closestDist = float.MaxValue;
		for (int i = _activeNotes.Count - 1; i >= 0; i--)
		{
			var n = _activeNotes[i];
			if (!IsInstanceValid(n) || n.WasHit || n.Missed || !n.IsHOPO || !n.IsInHitWindow()) continue;
			float d = Mathf.Abs(n.GlobalPosition.Z - Note.HitLineZ);
			if (d < closestDist) { closestDist = d; closest = n; }
		}
		return closest;
	}

	/// <summary>Ativa/desativa o glow de Star Power na track e botões.</summary>
	public void SetStarPowerGlow(bool active)
	{
		if (active)
		{
			Color spColor = new Color(0.4f, 0.7f, 1f);
			if (_trackMat != null)
			{
				_trackMat.EmissionEnabled = true;
				_trackMat.Emission = spColor * 0.5f;
				_trackMat.EmissionEnergyMultiplier = 2.0f;
			}
			if (_hitZoneMat != null)
			{
				_hitZoneMat.Emission = spColor * 3f;
				_hitZoneMat.EmissionEnergyMultiplier = 5.0f;
			}
		}
		else
		{
			// Restaura cores normais
			ApplyColor();
		}
	}

	private void ShowReleasePenalty()
	{
		if (_hitZoneMat == null) return;
		var originalEnergy = _hitZoneMat.EmissionEnergyMultiplier;
		_hitZoneMat.Emission = Colors.Red * 2f;
		_hitZoneMat.EmissionEnergyMultiplier = 8f;
		var t = GetTree().CreateTimer(0.15f);
		t.Timeout += () =>
		{
			// Guard: verifica se o Lane ainda existe na árvore antes de acessar o material
			if (!IsInstanceValid(this) || _hitZoneMat == null) return;
			_hitZoneMat.Emission = LaneColor * 2.5f;
			_hitZoneMat.EmissionEnergyMultiplier = originalEnergy;
		};
	}
}
