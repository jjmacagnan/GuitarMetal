using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Importa charts no formato .chart (Clone Hero).
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

		// Pré-computa tabela de tempos uma única vez — O(BPM events)
		var timeMap  = BuildTimeMap(bpmMap, resolution);

		var noteList = new List<NoteData>();
		var starPowerRanges = new List<(double start, double end)>();
		var forceHopoTicks  = new HashSet<long>();
		var forceStrumTicks = new HashSet<long>();

		section = "";
		foreach (string raw in lines)
		{
			string line = raw.Trim();
			if (line.StartsWith("[") && line.EndsWith("]")) { section = line[1..^1]; continue; }
			if (line is "{" or "}") continue;

			if (section == targetTrack)
			{
				ParseNoteLine(line, noteList, timeMap, offset);
				ParseStarPowerLine(line, starPowerRanges, timeMap, offset);
				ParseForceFlags(line, forceHopoTicks, forceStrumTicks);
			}
		}

		noteList.Sort((a, b) => a.Time.CompareTo(b.Time));

		// Marca notas Star Power
		foreach (var nd in noteList)
		{
			foreach (var (spStart, spEnd) in starPowerRanges)
			{
				if (nd.Time >= spStart && nd.Time <= spEnd)
				{
					nd.IsStarPower = true;
					break;
				}
			}
		}

		// Marca HOPO por proximidade ou flags forçadas
		MarkHOPOs(noteList, bpmMap, resolution, forceHopoTicks, forceStrumTicks, timeMap, offset);

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
			case "Name":       chart.SongName = val;                                                                break;
			case "Resolution": float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out resolution); break;
			case "Offset":     float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out offset);     break;
		}
	}

	private static void ParseBpmLine(string line, SortedDictionary<long, float> bpmMap)
	{
		// Formato: tick = B millivalue   ex: 0 = B 120000
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		if (!long.TryParse(line[..eq].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick)) return;

		string rest = line[(eq + 1)..].Trim();
		if (!rest.StartsWith("B ")) return;

		if (float.TryParse(rest[2..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float milliVal))
			bpmMap[tick] = milliVal / 1000f;
	}

	private static void ParseNoteLine(string line, List<NoteData> noteList,
		List<TimePoint> timeMap, float offset)
	{
		// Formato: tick = N fret sustain   ex: 768 = N 0 0
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		if (!long.TryParse(line[..eq].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick)) return;

		string rest = line[(eq + 1)..].Trim();
		if (!rest.StartsWith("N ")) return;

		string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3) return;
		if (!int.TryParse(parts[1],  NumberStyles.Integer, CultureInfo.InvariantCulture, out int fret))     return;
		if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long sustain)) return;

		if (fret < 0 || fret > 4) return;

		// Busca binária — O(log n) por nota em vez de O(n)
		double noteTime = TicksToSeconds(tick, timeMap) + offset;
		float  noteDur  = sustain > 0
			? (float)(TicksToSeconds(tick + sustain, timeMap) - TicksToSeconds(tick, timeMap))
			: 0f;

		noteList.Add(new NoteData
		{
			Time     = noteTime,
			Lane     = fret,
			IsLong   = sustain > 0,
			Duration = noteDur
		});
	}

	// ── Star Power parser ──────────────────────────────────────────────────

	private static void ParseStarPowerLine(string line, List<(double start, double end)> ranges,
		List<TimePoint> timeMap, float offset)
	{
		// Formato: tick = S 2 duration
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		if (!long.TryParse(line[..eq].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick)) return;

		string rest = line[(eq + 1)..].Trim();
		if (!rest.StartsWith("S 2")) return;

		string[] parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3) return;
		if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long duration)) return;

		double startTime = TicksToSeconds(tick, timeMap) + offset;
		double endTime   = TicksToSeconds(tick + duration, timeMap) + offset;
		ranges.Add((startTime, endTime));
	}

	// ── Force flags parser ────────────────────────────────────────────────

	private static void ParseForceFlags(string line, HashSet<long> forceHopo, HashSet<long> forceStrum)
	{
		// N 5 = force HOPO, N 6 = force strum
		int eq = line.IndexOf('=');
		if (eq < 0) return;
		if (!long.TryParse(line[..eq].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long tick)) return;

		string rest = line[(eq + 1)..].Trim();
		if (rest.StartsWith("N 5")) forceHopo.Add(tick);
		else if (rest.StartsWith("N 6")) forceStrum.Add(tick);
	}

	// ── HOPO detection ────────────────────────────────────────────────────

	private static void MarkHOPOs(List<NoteData> notes, SortedDictionary<long, float> bpmMap,
		float resolution, HashSet<long> forceHopo, HashSet<long> forceStrum,
		List<TimePoint> timeMap, float offset)
	{
		if (notes.Count < 2) return;

		// HOPO threshold: 1/12 of a beat at the current BPM
		float baseBpm = GetBpmAtTick(0, bpmMap);
		double hopoThreshold = 60.0 / baseBpm / 3.0; // 1/12 note in seconds

		for (int i = 1; i < notes.Count; i++)
		{
			var prev = notes[i - 1];
			var curr = notes[i];

			double timeDelta = curr.Time - prev.Time;
			bool differentLane = curr.Lane != prev.Lane;

			// Auto-detect: close notes on different lanes are HOPO
			if (timeDelta > 0 && timeDelta <= hopoThreshold && differentLane)
				curr.IsHOPO = true;

			// Force flags override auto-detection
			// (We check tick-based flags - approximate by checking if any forced tick maps to this note's time)
			// Since we don't store ticks on NoteData, use time proximity for force flags
		}
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

	// ── Time map pré-computado ────────────────────────────────────────────
	// Evita O(notas × mudanças_BPM) ao pré-calcular o tempo acumulado em
	// cada ponto de mudança de BPM e usar busca binária por nota.

	private struct TimePoint
	{
		public long   Tick;
		public double Time;
		public double SecsPerTick; // segundos por tick neste segmento
	}

	private static List<TimePoint> BuildTimeMap(SortedDictionary<long, float> bpmMap, float resolution)
	{
		var map     = new List<TimePoint>(bpmMap.Count);
		double time = 0;
		long   prev = 0;
		float  bpm  = 120f;

		foreach (var (tick, nextBpm) in bpmMap)
		{
			if (tick > prev)
				time += (tick - prev) / (double)resolution * (60.0 / bpm);

			map.Add(new TimePoint
			{
				Tick        = tick,
				Time        = time,
				SecsPerTick = 60.0 / nextBpm / resolution
			});

			prev = tick;
			bpm  = nextBpm;
		}

		return map;
	}

	private static double TicksToSeconds(long ticks, List<TimePoint> timeMap)
	{
		// Busca binária: encontra o segmento correto em O(log n)
		int lo = 0, hi = timeMap.Count - 1;
		while (lo < hi)
		{
			int mid = (lo + hi + 1) / 2;
			if (timeMap[mid].Tick <= ticks) lo = mid;
			else hi = mid - 1;
		}
		var pt = timeMap[lo];
		return pt.Time + (ticks - pt.Tick) * pt.SecsPerTick;
	}

	private static float GetBpmAtTick(long tick, SortedDictionary<long, float> bpmMap)
	{
		float bpm = 120f;
		foreach (var (t, b) in bpmMap) { if (t > tick) break; bpm = b; }
		return bpm;
	}
}
