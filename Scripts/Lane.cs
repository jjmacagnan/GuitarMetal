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

	// ── Botão: base larga (disco) + cap elevado ────────────────────────────
	private MeshInstance3D     _buttonBaseMesh;   // disco externo, plano
	private StandardMaterial3D _buttonBaseMat;
	private MeshInstance3D     _buttonCapMesh;    // cilindro central, mais alto
	private StandardMaterial3D _buttonCapMat;

	// ── Indicador de hitline ───────────────────────────────────────────────
	private MeshInstance3D     _hitZoneMesh;
	private StandardMaterial3D _hitZoneMat;

	// ── Label 3D com tecla acima do botão ─────────────────────────────────
	private Label3D _keyLabel;

	// ── Notas ──────────────────────────────────────────────────────────────
	private readonly List<Note> _activeNotes     = new();
	private Note                _currentHoldNote = null;
	private bool                _wasHolding;
	private bool                _wasNoteInWindow;

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
		_buttonBaseMat = new StandardMaterial3D();
		_buttonBaseMesh.MaterialOverride = _buttonBaseMat;

		// 3. Cap do botão — cilindro central mais alto (dá profundidade de botão)
		_buttonCapMesh = GetNodeOrNull<MeshInstance3D>("ButtonCap");
		if (_buttonCapMesh == null)
		{
			_buttonCapMesh = new MeshInstance3D { Name = "ButtonCap" };
			_buttonCapMesh.Mesh = new CylinderMesh
			{
				TopRadius    = 0.60f,
				BottomRadius = 0.62f,
				Height       = 0.26f,
				RadialSegments = 24
			};
			_buttonCapMesh.Position = new Vector3(0f, 0.17f, 0f);
			AddChild(_buttonCapMesh);
		}
		_buttonCapMat = new StandardMaterial3D();
		_buttonCapMesh.MaterialOverride = _buttonCapMat;

		// 4. HitZone marker — barra brilhante na hitline
		_hitZoneMesh = GetNodeOrNull<MeshInstance3D>("HitZoneMarker");
		if (_hitZoneMesh == null)
		{
			_hitZoneMesh = new MeshInstance3D { Name = "HitZoneMarker" };
			_hitZoneMesh.Mesh     = new BoxMesh { Size = new Vector3(1.85f, 0.06f, 0.28f) };
			_hitZoneMesh.Position = new Vector3(0f, 0.03f, -0.8f);
			_hitZoneMat = new StandardMaterial3D();
			_hitZoneMesh.MaterialOverride = _hitZoneMat;
			AddChild(_hitZoneMesh);
		}
		else { _hitZoneMat = _hitZoneMesh.MaterialOverride as StandardMaterial3D; }

		// 5. Label 3D com a tecla acima do botão
		_keyLabel = GetNodeOrNull<Label3D>("KeyLabel");
		if (_keyLabel == null)
		{
			_keyLabel = new Label3D
			{
				Name                = "KeyLabel",
				PixelSize           = 0.012f,
				FontSize            = 52,
				Position            = new Vector3(0f, 0.78f, 0f),
				Billboard           = BaseMaterial3D.BillboardModeEnum.Enabled,
				HorizontalAlignment = HorizontalAlignment.Center,
				OutlineSize         = 6,
			};
			AddChild(_keyLabel);
		}

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

		if (_keyLabel != null)
		{
			_keyLabel.Text     = InputKey == Key.Space ? "SPC" : InputKey.ToString();
			_keyLabel.Modulate = LaneColor;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsEcho()) return;

		string action = $"lane_{LaneIndex}";
		if (!InputMap.HasAction(action)) return;

		if (@event.IsActionPressed(action))
			OnKeyPressed();
		else if (@event.IsActionReleased(action))
			OnKeyReleased();
	}

	private void OnKeyPressed()
	{
		// Pulso de brilho nos dois elementos do botão
		if (_buttonCapMat != null)
		{
			_buttonCapMat.Emission = LaneColor * 4f;
			var t = GetTree().CreateTimer(0.12f);
			t.Timeout += () => { if (_buttonCapMat != null) _buttonCapMat.Emission = LaneColor * 1.0f; };
		}
		if (_buttonBaseMat != null)
		{
			_buttonBaseMat.Emission = LaneColor * 2.5f;
			var t = GetTree().CreateTimer(0.12f);
			t.Timeout += () => { if (_buttonBaseMat != null) _buttonBaseMat.Emission = LaneColor * 0.5f; };
		}
		if (_hitZoneMat != null)
		{
			_hitZoneMat.Emission = LaneColor * 6f;
			var t = GetTree().CreateTimer(0.12f);
			t.Timeout += () => { if (_hitZoneMat != null) _hitZoneMat.Emission = LaneColor * 2.5f; };
		}

		// Hit detection
		Note closest    = null;
		float closestDist = float.MaxValue;
		foreach (var n in _activeNotes)
		{
			if (!n.IsInHitWindow()) continue;
			float d = Mathf.Abs(n.Position.Z - Note.HitLineZ);
			if (d < closestDist) { closestDist = d; closest = n; }
		}

		if (closest != null)
		{
			closest.Hit();
			if (!closest.IsLong)
				EmitSignal(SignalName.NoteHitInLane, LaneIndex, closest);
		}
	}

	private void OnKeyReleased()
	{
		if (_currentHoldNote != null && IsInstanceValid(_currentHoldNote))
			_currentHoldNote.ReleaseHold();
		_currentHoldNote = null;
	}

	public override void _Process(double delta)
	{
		bool isHolding = _currentHoldNote != null && IsInstanceValid(_currentHoldNote);

		// Verifica se alguma nota está na janela de acerto
		bool noteInWindow = false;
		if (!isHolding)
			foreach (var n in _activeNotes)
				if (n.IsInHitWindow()) { noteInWindow = true; break; }

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

		note.NoteHit += (n) =>
		{
			_activeNotes.Remove(n);
			if (n.IsLong) _currentHoldNote = n;
		};

		note.NoteMissed += (n) =>
		{
			_activeNotes.Remove(n);
			if (_currentHoldNote == n)
			{
				_currentHoldNote = null;
				ShowReleasePenalty();
			}
			EmitSignal(SignalName.NoteMissedInLane, LaneIndex);
		};

		note.HoldComplete += (n) =>
		{
			if (_currentHoldNote == n) _currentHoldNote = null;
			EmitSignal(SignalName.HoldCompleteInLane, LaneIndex, n);
		};
	}

	private void ShowReleasePenalty()
	{
		if (_hitZoneMat == null) return;
		var originalMat = _hitZoneMat.EmissionEnergyMultiplier;
		_hitZoneMat.Emission = Colors.Red * 2f;
		_hitZoneMat.EmissionEnergyMultiplier = 8f;
		var t = GetTree().CreateTimer(0.15f);
		t.Timeout += () =>
		{
			if (_hitZoneMat != null)
			{
				_hitZoneMat.Emission = LaneColor * 2.5f;
				_hitZoneMat.EmissionEnergyMultiplier = originalMat;
			}
		};
	}
}
