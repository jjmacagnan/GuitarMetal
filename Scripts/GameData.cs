using Godot;
using System.Collections.Generic;

/// <summary>
/// Dados estáticos compartilhados entre cenas.
/// </summary>
public static class GameData
{
    // ── Constantes de física (fonte única da verdade) ──────────────────────
    /// <summary>Velocidade padrão das notas (unidades/s).</summary>
    public const float DefaultNoteSpeed    = 36f;
    /// <summary>Distância de spawn (|Z|). Notas surgem em Z = -NoteSpawnDistance.</summary>
    public const float NoteSpawnDistance   = 60f;
    /// <summary>
    /// Velocidade ativa das notas. GameManager define isso em _Ready para que
    /// LoadingScreen e GameManager usem sempre o mesmo TravelTime.
    /// </summary>
    public static float NoteSpeed          { get; set; } = DefaultNoteSpeed;
    /// <summary>Tempo que uma nota leva do spawn até a hitline com a velocidade ativa.</summary>
    public static float TravelTime         => NoteSpawnDistance / NoteSpeed;

    // ── Tempo da música (atualizado pelo GameManager a cada frame) ─────────
    /// <summary>
    /// Tempo atual da música em segundos (referência do áudio).
    /// As notas usam este valor para calcular sua posição Z,
    /// garantindo sincronização perfeita com o áudio.
    /// </summary>
    public static double SongTime { get; set; }

    // ── Seleção de música ──────────────────────────────────────────────────
    public static string      SelectedSongPath { get; set; } = "";
    public static string      SelectedSongName { get; set; } = "";
    public static AudioStream LoadedStream     { get; set; } = null;
    public static float       LoadedBPM        { get; set; } = 128f;

    // ── Dificuldade ──────────────────────────────────────────────────────
    /// <summary>Track selecionada (ex: "ExpertSingle"). null = auto (maior disponível).</summary>
    public static string       SelectedDifficulty    { get; set; } = null;
    /// <summary>Dificuldades encontradas no .chart (preenchido por SongSelectMenu).</summary>
    public static List<string> AvailableDifficulties { get; set; } = null;

    // Notas pré-geradas pelo LoadingScreen, consumidas pelo GameManager
    public static List<NoteData> PreparedNotes { get; set; } = null;

    // ── Resultado da partida ───────────────────────────────────────────────
    public static int Score         { get; set; }
    public static int NotesHit      { get; set; }
    public static int NotesMissed   { get; set; }
    public static int HoldsComplete { get; set; }
    public static int TotalNotes    { get; set; }

    public static float Accuracy =>
        TotalNotes > 0 ? (float)NotesHit / TotalNotes * 100f : 0f;

    public static string Grade => Accuracy switch
    {
        >= 95f => "S",
        >= 85f => "A",
        >= 70f => "B",
        >= 55f => "C",
        _      => "D"
    };

    public static void ResetRun()
    {
        Score         = 0;
        NotesHit      = 0;
        NotesMissed   = 0;
        HoldsComplete = 0;
        TotalNotes    = 0;
    }

    public static void Reset()
    {
        ResetRun();
        SelectedSongPath      = "";
        SelectedSongName      = "";
        LoadedStream          = null;
        PreparedNotes         = null;
        SelectedDifficulty    = null;
        AvailableDifficulties = null;
    }
}
