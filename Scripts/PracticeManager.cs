using Godot;

/// <summary>
/// Gerencia o estado do modo prática: velocidade, loop de seção, no-fail e feedback de timing.
/// Classe pura (não-Node) — instanciada pelo GameManager quando IsPracticeMode = true.
/// </summary>
public class PracticeManager
{
	private static readonly float[] SpeedOptions = { 1.0f, 0.75f, 0.5f };
	private int _speedIndex;

	public float SpeedMultiplier => SpeedOptions[_speedIndex];
	public bool NoFail => true;

	public double LoopStart { get; set; } = -1;
	public double LoopEnd   { get; set; } = -1;
	public bool IsLooping => LoopStart >= 0 && LoopEnd > LoopStart;

	/// <summary>Avança para o próximo valor de velocidade no ciclo (1.0 → 0.75 → 0.5 → 1.0).</summary>
	public void CycleSpeed()
	{
		_speedIndex = (_speedIndex + 1) % SpeedOptions.Length;
	}

	/// <summary>Verifica se o tempo atual ultrapassou o fim do loop.</summary>
	public bool ShouldLoop(double songTime) => IsLooping && songTime >= LoopEnd;

	/// <summary>Define o ponto de início do loop no tempo atual da música.</summary>
	public void SetLoopStart(double songTime)
	{
		LoopStart = songTime;
		if (LoopEnd >= 0 && LoopEnd <= LoopStart)
			LoopEnd = -1;
	}

	/// <summary>Define o ponto final do loop no tempo atual da música.</summary>
	public void SetLoopEnd(double songTime)
	{
		if (songTime > LoopStart)
			LoopEnd = songTime;
	}

	/// <summary>Limpa os pontos de loop.</summary>
	public void ClearLoop()
	{
		LoopStart = -1;
		LoopEnd   = -1;
	}

	/// <summary>
	/// Retorna o label de timing baseado na distância assinada da hitline.
	/// Negativo (Z &lt; hitline) = cedo, positivo = tarde.
	/// </summary>
	public static string GetTimingFeedback(float signedDistance, float perfectThreshold)
	{
		if (Mathf.Abs(signedDistance) <= perfectThreshold)
			return "";
		return signedDistance < 0 ? "EARLY" : "LATE";
	}
}
