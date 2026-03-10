using Godot;

/// <summary>
/// Nota individual. Suporta notas normais (tap) e notas longas (hold).
/// Move-se ao longo do eixo Z (de longe → hitline em Z=0).
/// </summary>
public partial class Note : Node3D
{
    [Export] public int    Lane     { get; set; } = 0;
    [Export] public float  Speed    { get; set; } = 12f;
    [Export] public double BeatTime { get; set; } = 0;
    [Export] public bool   IsLong   { get; set; } = false;
    [Export] public float  Duration { get; set; } = 0f;

    public bool WasHit { get; private set; }
    public bool Missed { get; private set; }

    // Hitline em Z=0, notas vêm de Z negativo em direção a Z=0
    public const float HitLineZ  = 0f;
    public const float HitWindow = 1.5f;
    private const float MissZ    = 2f;   // passou da hitline → miss

    private MeshInstance3D     _headMesh;
    private MeshInstance3D     _tailMesh;
    private StandardMaterial3D _tailMat;
    private float              _tailLen;
    private float              _originalZ;        // Z inicial (spawn)
    private float _holdTimer   = 0f;
    private bool  _isBeingHeld = false;

    [Signal] public delegate void NoteHitEventHandler(Note note);
    [Signal] public delegate void NoteMissedEventHandler(Note note);
    [Signal] public delegate void HoldCompleteEventHandler(Note note);

    /// <summary>
    /// Cria os meshes da nota. Deve ser chamado antes de AddChild(note).
    /// </summary>
    public void SetupVisuals(Color color)
    {
        _originalZ = Position.Z;
        
        // Cabeça (sempre presente)
        _headMesh = new MeshInstance3D { Name = "HeadMesh" };
        _headMesh.Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.3f, 1.4f) };
        _headMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor              = color,
            EmissionEnabled          = true,
            Emission                 = color * 1.5f,
            EmissionEnergyMultiplier = 1.5f
        };
        AddChild(_headMesh);

        // Cauda (apenas para hold notes) — estende-se para trás na pista (-Z)
        if (IsLong && Duration > 0f)
        {
            _tailLen  = Duration * Speed;
            _tailMesh = new MeshInstance3D { Name = "TailMesh" };
            _tailMesh.Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.2f, _tailLen) };
            _tailMat = new StandardMaterial3D
            {
                AlbedoColor              = color * 0.65f,
                EmissionEnabled          = true,
                Emission                 = color * 0.4f,
                EmissionEnergyMultiplier = 0.6f,
                Transparency             = BaseMaterial3D.TransparencyEnum.Alpha
            };
            _tailMesh.MaterialOverride = _tailMat;
            // Cauda atrás da cabeça (direção -Z = de onde a nota veio)
            _tailMesh.Position = new Vector3(0f, 0f, -(_tailLen * 0.5f));
            AddChild(_tailMesh);
        }
    }

    public override void _Process(double delta)
    {
        if (WasHit && IsLong)
        {
            ProcessHold((float)delta);
            return;
        }

        if (WasHit || Missed) return;

        // Move ao longo do eixo Z em direção à hitline (Z=0)
        Position += new Vector3(0f, 0f, Speed * (float)delta);

        if (Position.Z > MissZ)
        {
            Missed = true;
            EmitSignal(SignalName.NoteMissed, this);
            QueueFree();
        }
    }

    private void ProcessHold(float delta)
    {
        if (!_isBeingHeld)
        {
            // Tecla solta antes do fim — miss
            Missed = true;
            EmitSignal(SignalName.NoteMissed, this);
            QueueFree();
            return;
        }

        _holdTimer += delta;
        UpdateTailVisual();

        // Pulso de emissão no tail enquanto segurado
        if (_tailMat != null)
        {
            float pulse = (Mathf.Sin(_holdTimer * Mathf.Pi * 5f) + 1f) * 0.5f;
            _tailMat.EmissionEnergyMultiplier = Mathf.Lerp(0.6f, 2.5f, pulse);
            // Fade suave na cauda conforme é consumida
            float alphaFade = Mathf.Clamp(1f - _holdTimer / Duration * 0.3f, 0.5f, 1f);
            _tailMat.AlbedoColor = _tailMat.AlbedoColor with { A = alphaFade };
        }

        if (_holdTimer >= Duration)
        {
            EmitSignal(SignalName.HoldComplete, this);
            QueueFree();
        }
    }

    private void UpdateTailVisual()
    {
        if (_tailMesh == null || Duration <= 0f) return;
        
        // Progress do hold: 0 = início, 1 = fim
        float progress = Mathf.Clamp(_holdTimer / Duration, 0f, 1f);
        
        // Cauda encolhe conforme hold progride
        float remainingProgress = 1f - progress;
        _tailMesh.Scale = new Vector3(1f, 1f, remainingProgress);
        
        // Cauda vai de spawn (-_tailLen atrás) até hitline conforme consumida
        // Head fica fixo em Z=0, cauda atrás dele
        float consumedDistance = _tailLen * progress;
        _tailMesh.Position = new Vector3(0f, 0f, -((_tailLen - consumedDistance) * 0.5f));
    }

    public bool IsInHitWindow()
        => !WasHit && !Missed
           && Position.Z >= HitLineZ - HitWindow
           && Position.Z <= HitLineZ + HitWindow;

    public void Hit()
    {
        if (WasHit || Missed) return;
        WasHit = true;
        EmitSignal(SignalName.NoteHit, this);

        if (!IsLong)
        {
            var tw = CreateTween();
            tw.TweenProperty(this, "scale", Vector3.Zero, 0.1f);
            tw.TweenCallback(Callable.From(QueueFree));
        }
        else
        {
            _isBeingHeld = true;
            if (_headMesh != null) _headMesh.Visible = false;
        }
    }

    public void ReleaseHold()
    {
        if (IsLong && WasHit) _isBeingHeld = false;
    }
}
