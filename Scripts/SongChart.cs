using Godot;
using System.Collections.Generic;

/// <summary>
/// Dados de uma música (chart).
/// Pode ser gerado proceduralmente ou carregado de um arquivo JSON.
/// </summary>
[GlobalClass]
public partial class SongChart : Resource
{
	[Export] public string SongName    { get; set; } = "Demo Song";
	[Export] public string AudioPath   { get; set; } = "res://Audio/02 Master of Puppets.ogg";
	[Export] public float  BPM         { get; set; } = 128f;
	[Export] public float  StartOffset { get; set; } = 0f;

	[Export] public Godot.Collections.Array<NoteData> Notes { get; set; } = new();

	// ── Geração procedural ─────────────────────────────────────────────────

	/// <summary>
	/// Gera um chart de demonstração com notas normais e hold notes.
	/// </summary>
	/// <summary>
	/// Gera notas procedurais para a duração total da música.
	/// beatCount = número de beats a cobrir (calculado a partir do áudio).
	/// </summary>
	public void GenerateDemoChart(int beatCount = 64)
	{
		Notes.Clear();
		float beat    = 60f / BPM;
		int[] pattern = { 0, 2, 1, 3, 0, 4, 2, 1, 3, 4, 0, 2, 1, 4, 3 };

		for (int b = 0; b < beatCount; b++)
		{
			int lane = pattern[b % pattern.Length];
			float time = StartOffset + b * beat;

			// A cada 16 beats, adiciona uma hold note
			bool isHold = b % 16 == 15;

			Notes.Add(new NoteData
			{
				Time     = time,
				Lane     = lane,
				IsLong   = isHold,
				Duration = isHold ? beat * 1.5f : 0f
			});

			// Double notes a cada 8 beats (exceto onde já é hold)
			if (b % 8 == 7 && !isHold)
			{
				Notes.Add(new NoteData
				{
					Time = time,
					Lane = (lane + 2) % 5
				});
			}
		}
	}

}

// ── NoteData ───────────────────────────────────────────────────────────────

/// <summary>
/// Dados de uma nota individual no chart.
/// </summary>
[GlobalClass]
public partial class NoteData : Resource
{
	[Export] public double Time     { get; set; } = 0;
	[Export] public int    Lane     { get; set; } = 0;
	[Export] public bool   IsLong   { get; set; } = false;
	[Export] public float  Duration { get; set; } = 0f;
}
