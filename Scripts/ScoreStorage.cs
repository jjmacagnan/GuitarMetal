using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// Persistência de scores em user://scores.json e último nome em user://player.cfg.
/// </summary>
public static class ScoreStorage
{
    private const string ScoresPath = "user://scores.json";
    private const string PlayerPath = "user://player.cfg";

    // songName → lista de entradas
    private static Dictionary<string, List<ScoreEntry>> _scores;

    public class ScoreEntry
    {
        public string PlayerName { get; set; } = "";
        public int    Score      { get; set; }
        public float  Accuracy   { get; set; }
        public string Grade      { get; set; } = "D";
        public int    MaxCombo   { get; set; }
        public string Difficulty { get; set; } = "";
        public string Date       { get; set; } = "";
    }

    /// <summary>Salva um score para a música e persiste no disco.</summary>
    public static void Save(string songName, ScoreEntry entry)
    {
        EnsureLoaded();
        if (!_scores.ContainsKey(songName))
            _scores[songName] = new List<ScoreEntry>();
        _scores[songName].Add(entry);
        SaveAll();
    }

    /// <summary>Retorna os top N scores de uma música, ordenados por score desc.</summary>
    public static List<ScoreEntry> GetTopScores(string songName, int count = 10)
    {
        EnsureLoaded();
        if (!_scores.TryGetValue(songName, out var list)) return new List<ScoreEntry>();
        return list.OrderByDescending(e => e.Score).Take(count).ToList();
    }

    /// <summary>Retorna todas as músicas que têm scores salvos.</summary>
    public static List<string> GetSongsWithScores()
    {
        EnsureLoaded();
        return _scores.Keys.OrderBy(k => k).ToList();
    }

    // ── Persistência de nome ───────────────────────────────────────────

    public static string LoadLastPlayerName()
    {
        if (!FileAccess.FileExists(PlayerPath)) return "";
        using var file = FileAccess.Open(PlayerPath, FileAccess.ModeFlags.Read);
        return file?.GetAsText()?.Trim() ?? "";
    }

    public static void SavePlayerName(string name)
    {
        using var file = FileAccess.Open(PlayerPath, FileAccess.ModeFlags.Write);
        file?.StoreString(name);
    }

    // ── Serialização ───────────────────────────────────────────────────

    private static void EnsureLoaded()
    {
        if (_scores != null) return;
        _scores = new Dictionary<string, List<ScoreEntry>>();

        if (!FileAccess.FileExists(ScoresPath)) return;
        using var file = FileAccess.Open(ScoresPath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        string json = file.GetAsText();
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, List<ScoreEntry>>>(json);
            if (data != null) _scores = data;
        }
        catch (Exception ex)
        {
            GD.PushError($"[ScoreStorage] Erro ao ler scores: {ex.Message}");
        }
    }

    private static void SaveAll()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(_scores, options);
        using var file = FileAccess.Open(ScoresPath, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
    }
}
