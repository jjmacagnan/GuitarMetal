using System;
using Godot;

/// <summary>
/// Gerencia o clock de tempo da música com sincronização e correção de drift.
/// </summary>
public class SongTimeClock
{
	public double SongTime { get; private set; }

	private double _lastRawAudioTime = -1.0;

	/// <summary>
	/// Inicializa o clock com o tempo inicial calculado.
	/// </summary>
	/// <param name="travelTime">Tempo de viagem da nota (spawn → hitline).</param>
	/// <param name="audioLatencyOffset">Compensação manual de latência.</param>
	public SongTimeClock(float travelTime, float audioLatencyOffset)
	{
		double outputLatency = AudioServer.GetOutputLatency() + audioLatencyOffset;
		const double AudioDelay = 0.3;
		SongTime = -travelTime - AudioDelay - outputLatency;
	}

	/// <summary>
	/// Retorna o delay inicial antes de começar a tocar o áudio.
	/// </summary>
	public double GetAudioStartDelay(float travelTime)
	{
		const double AudioDelay = 0.3;
		return travelTime + AudioDelay;
	}

	/// <summary>
	/// Atualiza o tempo a cada frame com correção de drift do áudio.
	/// </summary>
	public void Update(double delta, AudioStreamPlayer audio, float audioLatencyOffset)
	{
		SongTime += delta;

		if (audio != null && audio.Playing)
		{
			double rawTime = audio.GetPlaybackPosition() - (AudioServer.GetOutputLatency() + audioLatencyOffset);

			if (Math.Abs(rawTime - _lastRawAudioTime) > 0.0001)
			{
				_lastRawAudioTime = rawTime;
				double drift = rawTime - SongTime;

				if (Math.Abs(drift) > 0.05d)
					SongTime = rawTime;                                      // drift > 50ms → snap
				else
					SongTime += drift * Math.Min(1.0, delta * 4.0);          // correção suave
			}
		}
	}
}
