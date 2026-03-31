using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Carrega charts de diferentes formatos (.chart, .json, .mid).
/// Extraído do LoadingScreen para separar lógica de dados da UI.
/// </summary>
public static class ChartLoader
{
	public class ChartResult
	{
		public List<NoteData> Notes;
		public float BPM;
		public float StartOffset;
		public string SongName;
	}

	/// <summary>
	/// Tenta carregar um chart a partir do caminho do áudio.
	/// Retorna null se nenhum chart foi encontrado (usar fallback procedural).
	/// </summary>
	public static ChartResult TryLoadChart(string audioPath, string selectedDifficulty)
	{
		int    lastDot  = audioPath.LastIndexOf('.');
		string basePath = lastDot >= 0 ? audioPath[..lastDot] : audioPath;
		string dir      = audioPath[..(audioPath.LastIndexOf('/') + 1)];

		// Lê song.ini da pasta (se existir) para nome e delay de áudio
		float iniDelayMs = 0f;
		string iniPath = dir + "song.ini";
		if (FileAccess.FileExists(iniPath))
		{
			var info = SongIniReader.Read(iniPath);
			iniDelayMs = info.DelayMs;
			string displayName = SongIniReader.BuildDisplayName(info, GameData.SelectedSongName);
			if (!string.IsNullOrEmpty(displayName))
				GameData.SelectedSongName = displayName;
		}

		// Prioridade: notes.chart → [nome].chart → .json → notes.mid → null (procedural)
		ChartResult result;

		result = TryLoadDotChart(dir + "notes.chart", selectedDifficulty, iniDelayMs);
		if (result != null) return result;

		result = TryLoadDotChart(basePath + ".chart", selectedDifficulty, iniDelayMs);
		if (result != null) return result;

		result = TryLoadJson(basePath + ".json");
		if (result != null) return result;

		result = TryLoadMidi(dir + "notes.mid", selectedDifficulty, iniDelayMs);
		return result;
	}

	/// <summary>
	/// Gera um chart procedural como fallback.
	/// </summary>
	public static List<NoteData> GenerateProceduralChart(float bpm, float startOffset, int beatCount)
	{
		var chart = new SongChart
		{
			BPM         = bpm,
			StartOffset = startOffset
		};
		chart.GenerateDemoChart(beatCount);

		var notes = new List<NoteData>();
		foreach (var nd in chart.Notes) notes.Add(nd);
		notes.Sort((a, b) => a.Time.CompareTo(b.Time));
		return notes;
	}

	// ── Loaders privados ───────────────────────────────────────────────────

	private static ChartResult TryLoadDotChart(string chartPath, string selectedDifficulty, float iniDelayMs)
	{
		if (!FileAccess.FileExists(chartPath)) return null;

		var imported = ChartImporter.Import(chartPath, selectedDifficulty);
		if (imported == null) return null;

		float startOffset = imported.StartOffset + iniDelayMs / 1000f;

		if (!string.IsNullOrEmpty(imported.SongName) && string.IsNullOrEmpty(GameData.SelectedSongName))
			GameData.SelectedSongName = imported.SongName;

		if (imported.Notes.Count == 0) return null;

		var notes = new List<NoteData>();
		float chartOffset      = imported.StartOffset;
		float offsetDifference = startOffset - chartOffset;

		foreach (var nd in imported.Notes)
		{
			notes.Add(new NoteData
			{
				Time     = nd.Time + offsetDifference,
				Lane     = nd.Lane,
				IsLong   = nd.IsLong,
				Duration = nd.Duration
			});
		}

		GD.Print($"[ChartLoader] .chart carregado: {notes.Count} notas, BPM={imported.BPM}, offset={startOffset:F3}s");

		return new ChartResult
		{
			Notes       = notes,
			BPM         = imported.BPM,
			StartOffset = startOffset,
			SongName    = imported.SongName
		};
	}

	private static ChartResult TryLoadJson(string jsonPath)
	{
		if (!FileAccess.FileExists(jsonPath))
		{
			GD.Print($"[ChartLoader] Sem chart JSON: '{jsonPath}'");
			return null;
		}

		using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
		string json = file.GetAsText();

		try
		{
			var doc  = JsonDocument.Parse(json);
			var root = doc.RootElement;

			float bpm         = 128f;
			float startOffset = 0f;
			string songName   = null;

			if (root.TryGetProperty("bpm",         out var bv)) bpm         = bv.GetSingle();
			if (root.TryGetProperty("startOffset", out var so)) startOffset = so.GetSingle();
			if (root.TryGetProperty("songName",    out var sn) && !string.IsNullOrEmpty(sn.GetString()))
				songName = sn.GetString();

			List<NoteData> notes = null;
			if (root.TryGetProperty("notes", out var notesEl) && notesEl.GetArrayLength() > 0)
			{
				notes = new List<NoteData>();
				foreach (var n in notesEl.EnumerateArray())
				{
					var nd = new NoteData();
					if (n.TryGetProperty("time",     out var t))  nd.Time     = t.GetDouble();
					if (n.TryGetProperty("lane",     out var l))  nd.Lane     = l.GetInt32();
					if (n.TryGetProperty("isLong",   out var il)) nd.IsLong   = il.GetBoolean();
					if (n.TryGetProperty("duration", out var d))  nd.Duration = d.GetSingle();
					notes.Add(nd);
				}
				notes.Sort((a, b) => a.Time.CompareTo(b.Time));
			}

			GD.Print($"[ChartLoader] JSON lido: BPM={bpm}, offset={startOffset}s" +
					 (notes != null ? $", {notes.Count} notas" : ", procedural"));

			if (notes == null) return null;

			if (!string.IsNullOrEmpty(songName) && string.IsNullOrEmpty(GameData.SelectedSongName))
				GameData.SelectedSongName = songName;

			return new ChartResult
			{
				Notes       = notes,
				BPM         = bpm,
				StartOffset = startOffset,
				SongName    = songName
			};
		}
		catch (System.Exception ex)
		{
			GD.PushError($"[ChartLoader] Erro ao ler JSON: {ex.Message}");
			return null;
		}
	}

	private static ChartResult TryLoadMidi(string midiPath, string selectedDifficulty, float iniDelayMs)
	{
		if (!FileAccess.FileExists(midiPath))
		{
			GD.Print($"[ChartLoader] Sem MIDI: '{midiPath}' — usando procedural");
			return null;
		}

		var imported = MidiImporter.Import(midiPath, selectedDifficulty);
		if (imported == null || imported.Notes.Count == 0)
		{
			GD.PushWarning($"[ChartLoader] MIDI sem notas jogáveis: {midiPath}");
			return null;
		}

		float startOffset = imported.StartOffset + iniDelayMs / 1000f;

		if (!string.IsNullOrEmpty(imported.SongName) && imported.SongName != "thefinalcountdown"
		    && string.IsNullOrEmpty(GameData.SelectedSongName))
			GameData.SelectedSongName = imported.SongName;

		var notes = new List<NoteData>();

		foreach (var nd in imported.Notes)
		{
			notes.Add(new NoteData
			{
				Time     = nd.Time + imported.StartOffset + iniDelayMs / 1000f,
				Lane     = nd.Lane,
				IsLong   = nd.IsLong,
				Duration = nd.Duration
			});
		}

		GD.Print($"[ChartLoader] MIDI carregado: {notes.Count} notas, BPM={imported.BPM:F1}, offset={startOffset:F3}s");

		return new ChartResult
		{
			Notes       = notes,
			BPM         = imported.BPM,
			StartOffset = startOffset,
			SongName    = imported.SongName
		};
	}
}
