using Godot;

/// <summary>
/// Nota individual. Suporta notas normais (tap) e notas longas (hold).
/// Move-se ao longo do eixo Z (de longe → hitline em Z=0).
/// </summary>
public partial class Note : Node3D
{
    [Export] public int    Lane       { get; set; } = 0;
    [Export] public float  Speed      { get; set; } = 12f;
    [Export] public double BeatTime   { get; set; } = 0;
    [Export] public bool   IsLong     { get; set; } = false;
    [Export] public float  Duration   { get; set; } = 0f;
    [Export] public bool   IsStarPower { get; set; } = false;
    [Export] public bool   IsHOPO     { get; set; } = false;

    public bool WasHit { get; private set; }
    public bool Missed { get; private set; }

    // Hitline em Z=0, notas vêm de Z negativo em direção a Z=0
    public const float HitLineZ  = 0f;

    // Janela de acerto (ajustada dinamicamente pela velocidade da nota)
    // As janelas são definidas em tempo (segundos): PERFECT=25ms, GREAT=60ms, GOOD=90ms
    // Convertidas para unidades de espaço: window_units = time_seconds * Speed (unidades/s)
    public float GetPerfectWindow() => 0.025f * Speed;
    public float GetGreatWindow()   => 0.06f * Speed;
    public float GetGoodWindow()    => 0.09f * Speed;
    private float GetMissZ() => HitLineZ + GetGoodWindow() + 0.06f;

    private MeshInstance3D     _headMesh;
    private MeshInstance3D     _tailMesh;
    private StandardMaterial3D _tailMat;
    private float              _tailLen;
    private float              _originalZ;        // Z inicial (spawn)
    private float _holdTimer    = 0f;
    private bool  _isBeingHeld  = false;
    private bool  _holdResolved = false;  // guard contra emissão dupla de sinais

    [Signal] public delegate void NoteHitEventHandler(Note note);
    [Signal] public delegate void NoteMissedEventHandler(Note note);
    [Signal] public delegate void HoldCompleteEventHandler(Note note);

    /// <summary>
    /// Cria os meshes da nota. Deve ser chamado antes de AddChild(note).
    /// </summary>
    public void SetupVisuals(Color color)
    {
        _originalZ = Position.Z;

        // HOPO: cabeça esférica semi-transparente; SP: aura azul/branca brilhante
        Mesh headShape;
        Color headColor = color;
        float emissionMult = 1.5f;
        float alpha = 1f;

        if (IsHOPO)
        {
            headShape = new SphereMesh { Radius = 0.7f, Height = 1.4f, RadialSegments = 16, Rings = 8 };
            alpha = 0.85f;
            emissionMult = 2.5f;
        }
        else
        {
            headShape = new BoxMesh { Size = new Vector3(1.4f, 0.3f, 1.4f) };
        }

        if (IsStarPower)
        {
            headColor = new Color(0.6f, 0.85f, 1f); // azul claro brilhante
            emissionMult = 3.5f;
        }

        // Cabeça (sempre presente)
        _headMesh = new MeshInstance3D { Name = "HeadMesh" };
        _headMesh.Mesh = headShape;
        _headMesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor              = new Color(headColor.R, headColor.G, headColor.B, alpha),
            EmissionEnabled          = true,
            Emission                 = (IsStarPower ? headColor : color) * 1.5f,
            EmissionEnergyMultiplier = emissionMult,
            Transparency             = alpha < 1f ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
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

        // Posição Z calculada diretamente a partir do tempo da música.
        // Garante que a nota esteja SEMPRE sincronizada com o áudio,
        // independente de variações de frame rate.
        // Quando songTime == BeatTime → Z = 0 (hitline).
        float idealZ = -(float)(BeatTime - GameData.SongTime) * Speed;
        Position = new Vector3(Position.X, Position.Y, idealZ);

        if (Position.Z > GetMissZ())
        {
            Missed = true;
            EmitSignal(SignalName.NoteMissed, this);
            QueueFree();
        }
    }

    private void ProcessHold(float delta)
    {
        if (_holdResolved) return;

        _holdTimer += delta;

        // FIX M1: Verifica conclusão ANTES de checar release.
        // Se o timer atingir Duration no mesmo frame em que o jogador solta a tecla,
        // o hold é contado como completo e não como miss (ordem justa).
        if (_holdTimer >= Duration)
        {
            _holdResolved = true;
            UpdateTailVisual();
            EmitSignal(SignalName.HoldComplete, this);
            QueueFree();
            return;
        }

        if (!_isBeingHeld)
        {
            // Tecla solta antes do fim — miss
            _holdResolved = true;
            Missed = true;
            EmitSignal(SignalName.NoteMissed, this);
            QueueFree();
            return;
        }

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
    {
        float hw = GetGoodWindow();
        return !WasHit && !Missed
               && Position.Z >= HitLineZ - hw
               && Position.Z <= HitLineZ + hw;
    }

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
            // Snap para a hitline para que a cauda visual fique alinhada corretamente.
            // Sem isso, se a nota for acertada fora de Z=0 a cauda fica deslocada.
            Position = new Vector3(Position.X, Position.Y, HitLineZ);
        }
    }

    public void ReleaseHold()
    {
        // FIX H4: Guarda contra _holdResolved para evitar transição de estado indevida
        // caso ReleaseHold seja chamado após o hold já ter sido completado ou perdido.
        if (IsLong && WasHit && !_holdResolved) _isBeingHeld = false;
    }
}
