using Godot;

/// <summary>
/// Pool de partículas para efeitos de hit e hold fire nas lanes.
/// Gerencia criação, reutilização e controle de emissão.
/// </summary>
public partial class HitParticlePool : Node3D
{
	private GpuParticles3D[] _hitParticles;
	private GpuParticles3D[] _holdFireParticles;
	private bool[]           _laneHolding;

	/// <summary>
	/// Inicializa os pools de partículas para todas as lanes.
	/// </summary>
	public void Initialize()
	{
		InitHitParticles();
		InitHoldFireParticles();
	}

	/// <summary>Dispara o efeito burst de hit na lane.</summary>
	public void SpawnHitEffect(int lane)
	{
		if (_hitParticles == null || lane < 0 || lane >= _hitParticles.Length) return;
		if (!IsInstanceValid(_hitParticles[lane])) return;
		_hitParticles[lane].Restart();
	}

	/// <summary>Liga o fogo contínuo de uma lane enquanto o hold está ativo.</summary>
	public void StartHoldFire(int lane)
	{
		if (_holdFireParticles == null || lane < 0 || lane >= _holdFireParticles.Length) return;
		if (!IsInstanceValid(_holdFireParticles[lane])) return;
		_laneHolding[lane]                = true;
		_holdFireParticles[lane].Emitting = true;
	}

	/// <summary>Desliga o fogo contínuo de uma lane.</summary>
	public void StopHoldFire(int lane)
	{
		if (_holdFireParticles == null || lane < 0 || lane >= _holdFireParticles.Length) return;
		if (!IsInstanceValid(_holdFireParticles[lane])) return;
		_laneHolding[lane]                = false;
		_holdFireParticles[lane].Emitting = false;
	}

	/// <summary>Para todos os efeitos de hold ativos.</summary>
	public void StopAllHoldFire()
	{
		for (int i = 0; i < LaneConfig.LaneCount; i++)
			StopHoldFire(i);
	}

	// ── Inicialização interna ──────────────────────────────────────────────

	private void InitHitParticles()
	{
		_hitParticles = new GpuParticles3D[LaneConfig.LaneCount];
		for (int i = 0; i < LaneConfig.LaneCount; i++)
		{
			_hitParticles[i] = BuildFireParticle(LaneConfig.LaneColors[i]);
			_hitParticles[i].Position = new Vector3(LaneConfig.LaneX[i], 0.4f, Note.HitLineZ);
			_hitParticles[i].Emitting = false;
			AddChild(_hitParticles[i]);
		}
	}

	private void InitHoldFireParticles()
	{
		_holdFireParticles = new GpuParticles3D[LaneConfig.LaneCount];
		_laneHolding       = new bool[LaneConfig.LaneCount];
		for (int i = 0; i < LaneConfig.LaneCount; i++)
		{
			_holdFireParticles[i]          = BuildContinuousFireParticle(LaneConfig.LaneColors[i]);
			_holdFireParticles[i].Position = new Vector3(LaneConfig.LaneX[i], 0.4f, Note.HitLineZ);
			_holdFireParticles[i].Emitting = false;
			AddChild(_holdFireParticles[i]);
		}
	}

	// ── Builders de partículas ─────────────────────────────────────────────

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

	private static GpuParticles3D BuildContinuousFireParticle(Color color)
	{
		var mat = new ParticleProcessMaterial();
		mat.Direction          = new Vector3(0f, 1f, 0f);
		mat.Spread             = 30f;
		mat.InitialVelocityMin = 2f;
		mat.InitialVelocityMax = 5f;
		mat.Gravity            = new Vector3(0f, -1.5f, 0f);
		mat.ScaleMin           = 0.10f;
		mat.ScaleMax           = 0.30f;

		var gradient = new Gradient();
		gradient.Colors  = new Color[]
		{
			color.Lightened(0.5f),
			new Color(Mathf.Max(color.R, 0.9f), color.G * 0.2f, 0f, 0.8f),
			new Color(0.3f, 0f, 0f, 0f)
		};
		gradient.Offsets = new float[] { 0f, 0.5f, 1f };
		mat.ColorRamp = new GradientTexture1D { Gradient = gradient };

		var quadMesh = new QuadMesh { Size = new Vector2(0.35f, 0.35f) };
		quadMesh.Material = new StandardMaterial3D
		{
			ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode          = BaseMaterial3D.BillboardModeEnum.Enabled,
		};

		return new GpuParticles3D
		{
			OneShot         = false,
			Explosiveness   = 0f,
			Amount          = 24,
			Lifetime        = 0.55f,
			ProcessMaterial = mat,
			DrawPass1       = quadMesh,
		};
	}
}
