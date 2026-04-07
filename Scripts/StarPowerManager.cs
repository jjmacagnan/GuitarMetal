using Godot;

/// <summary>
/// Gerencia o sistema de Star Power / Overdrive.
/// Notas marcadas como Star Power preenchem o gauge.
/// Quando ativado, o score multiplier é dobrado.
/// Classe pura (não-Node) — instanciada pelo GameManager.
/// </summary>
public class StarPowerManager
{
	public float Gauge { get; private set; }
	public bool IsActive { get; private set; }

	public const float MaxGauge            = 1.0f;
	public const float FillPerNote         = 0.04f;        // ~25 notas SP para gauge cheio
	public const float ActivationThreshold = 0.5f;         // precisa 50% para ativar
	public const float DrainRatePerSecond  = 0.125f;       // 8 segundos de duração ativa

	/// <summary>Chamado quando uma nota marcada como Star Power é acertada.</summary>
	public void OnStarPowerNoteHit()
	{
		Gauge = Mathf.Min(Gauge + FillPerNote, MaxGauge);
	}

	/// <summary>Verifica se pode ativar (gauge >= 50% e não está ativo).</summary>
	public bool CanActivate() => !IsActive && Gauge >= ActivationThreshold;

	/// <summary>Tenta ativar o Star Power. Retorna true se ativou.</summary>
	public bool TryActivate()
	{
		if (!CanActivate()) return false;
		IsActive = true;
		return true;
	}

	/// <summary>Atualiza o gauge a cada frame (drena quando ativo).</summary>
	public void Update(float delta)
	{
		if (!IsActive) return;

		Gauge -= DrainRatePerSecond * delta;
		if (Gauge <= 0f)
		{
			Gauge = 0f;
			IsActive = false;
		}
	}

	/// <summary>Retorna o multiplicador de bônus (2 se ativo, 1 se não).</summary>
	public int GetBonusMultiplier() => IsActive ? 2 : 1;
}
