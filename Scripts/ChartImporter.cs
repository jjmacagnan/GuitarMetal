using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Importa charts no formato .chart (Clone Hero / Guitar Hero).
///
/// Formato suportado:
///   [Song]       → metadados (Name, Resolution, Offset)
///   [SyncTrack]  → mapa de BPM (suporte a mudanças de BPM)
///   [ExpertSingle] / [HardSingle] / [MediumSingle] / [EasySingle]
///                → notas (N fret sustain)
///
/// Mapeamento de frets → lanes:
///   0 = Verde, 1 = Vermelho, 2 = Amarelo, 3 = Azul, 4 = Laranja
/// </summary>
public static class ChartImporter
{
	private static readonly string[] TrackPriority =
		{ "ExpertSingle", "HardSingle", "MediumSingle", "EasySingle" };

	/// <summary>
	/// Escaneia um .chart e retorna quais dificuldades estão presentes
	/// (ex: ["ExpertSingle", "HardSingle"]) sem parsear notas.
	/// </summary>
	public static List<string> ScanDifficulties(string chartPath)
	{
		var result = new List<string>();
		if (!FileAccess.FileExists(chartPath)) return result;

		using var file = FileAccess.Open(chartPath, FileAccess.ModeFlags.Read);
		string[] lines = file.GetAsText().Split('\n');

		var found = new HashSet<string>();
		foreach (string raw in lines)
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]"))
				found.Add(line[1..^1]);
		}

		// Retorna na ordem de prioridade (Expert primeiro)
		foreach (string t in TrackPriority)
			if (found.Contains(t)) result.Add(t);

		return result;
	}

	/// <summary>
	/// Importa um .chart. Se <paramref name="difficulty"/> for informado,
	/// usa essa track; caso contrário pega a mais alta disponível.
	/// </summary>
	public static SongChart Import(string chartPath, string difficulty = null)
	{
		if (!FileAccess.FileExists(chartPath))
		{
			GD.PushError($"[ChartImporter] Arquivo não encontrado: {chartPath}");
			return null;
		}

		using var file = FileAccess.Open(chartPath, FileAccess.ModeFlags.Read);
		string[] lines = file.GetAsText().Split('\n');

		var chart      = new SongChart();
		float resolution = 192f;
		float offset     = 0f;
		var bpmMap       = new SortedDictionary<long, float>(); // tick → BPM

		// ── Primeira passagem: Song + SyncTrack ────────────────────────────
		string section = "";
		foreach (string raw in lines)
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]")) { section = line[1..^1]; continue; }
			if (line is "{" or "}") continue;

			if (section == "Song")        ParseSongLine(line, chart, ref resolution, ref offset);
			else if (section == "SyncTrack") ParseBpmLine(line, bpmMap);
		}

		if (bpmMap.Count == 0) bpmMap[0] = 120f;
		chart.BPM         = GetBpmAtTick(0, bpmMap);
		chart.StartOffset = offset;

		// ── Segunda passagem: notas ─────────────────────────────────────────
		string targetTrack = difficulty != null && FindAllTracks(lines).Contains(difficulty)
			? difficulty
			: FindBestTrack(lines);
		if (targetTrack == null)
		{
			GD.PushWarning("[ChartImporter] Nenhuma track de notas encontrada.");
			return chart;
		}

		var noteList = new List<NoteData>();
		section = "";
		foreach (string raw in lines)
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]")) { section = line[1..^1]; continue; }
			if (line is "{" or "}") continue;

			if (section == targetTrack)
				ParseNoteLine(line, noteList, bpmMap, resolution, offset);
		}

		noteList.Sort((a, b) => a.Time.CompareTo(b.Time));
		foreach (var nd in noteList) chart.Notes.Add(nd);

		GD.Print($"[ChartImporter] '{chart.SongName}' — {chart.Notes.Count} notas ({targetTrack}), BPM={chart.BPM}");
		return chart;
	}

	// ── Parsers ────────────────────────────────────────────────────────────

	private static void ParseSongLine(string line, SongChart chart, ref float resolution, ref float offset)
	{
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		string key = line[..eq].Trim();
		string val = line[(eq + 1)..].Trim().Trim('"');

		switch (key)
		{
			case "Name":       chart.SongName = val;                  break;
			case "Resolution": float.TryParse(val, out resolution);   break;
			case "Offset":     float.TryParse(val, out offset);       break;
		}
	}

	private static void ParseBpmLine(string line, SortedDictionary<long, float> bpmMap)
	{
		// Formato: tick = BPM millivalue   ex: 0 = BPM 120000
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		if (!long.TryParse(line[..eq].Trim(), out long tick)) return;

		string rest = line[(eq + 1)..].Trim();
		if (!rest.StartsWith("BPM ")) return;

		if (float.TryParse(rest[4..].Trim(), out float milliVal))
			bpmMap[tick] = milliVal / 1000f;
	}

	private static void ParseNoteLine(string line, List<NoteData> noteList,
		SortedDictionary<long, float> bpmMap, float resolution, float offset)
	{
		// Formato: tick = N fret sustain   ex: 768 = N 0 0
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		if (!long.TryParse(line[..eq].Trim(), out long tick)) return;

		string rest = line[(eq + 1)..].Trim();
		if (!rest.StartsWith("N ")) return;

		string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3) return;
		if (!int.TryParse(parts[1], out int fret))    return;
		if (!long.TryParse(parts[2], out long sustain)) return;

		// Frets 0-4 = lanes jogáveis; 5+ são modificadores (força, tap, open)
		if (fret < 0 || fret > 4) return;

		double noteTime  = TicksToSeconds(tick, bpmMap, resolution) + offset;
		double noteDur   = sustain > 0
			? TicksToSeconds(tick + sustain, bpmMap, resolution) + offset - noteTime
			: 0;

		noteList.Add(new NoteData
		{
			Time     = noteTime,
			Lane     = fret,
			IsLong   = sustain > 0,
			Duration = (float)noteDur
		});
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private static HashSet<string> FindAllTracks(string[] lines)
	{
		var found = new HashSet<string>();
		foreach (string raw in lines)
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]"))
				found.Add(line[1..^1]);
		}
		return found;
	}

	private static string FindBestTrack(string[] lines)
	{
		var found = FindAllTracks(lines);
		foreach (string t in TrackPriority)
			if (found.Contains(t)) return t;
		return null;
	}

	/// <summary>Converte ticks em segundos respeitando mudanças de BPM.</summary>
	private static double TicksToSeconds(long ticks, SortedDictionary<long, float> bpmMap, float resolution)
	{
		double time    = 0;
		long   prevTick = 0;
		float  prevBpm  = GetBpmAtTick(0, bpmMap);

		foreach (var (mapTick, bpm) in bpmMap)
		{
			if (mapTick >= ticks) break;
			time    += (mapTick - prevTick) / (double)resolution * (60.0 / prevBpm);
			prevTick = mapTick;
			prevBpm  = bpm;
		}

		time += (ticks - prevTick) / (double)resolution * (60.0 / prevBpm);
		return time;
	}

	private static float GetBpmAtTick(long tick, SortedDictionary<long, float> bpmMap)
	{
		float bpm = 120f;
		foreach (var (t, b) in bpmMap) { if (t > tick) break; bpm = b; }
		return bpm;
	}
}
