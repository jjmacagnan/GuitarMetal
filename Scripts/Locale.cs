using System.Collections.Generic;

/// <summary>
/// Sistema de localização simples para PT/EN.
/// Uso: Locale.Tr("PLAY") → "JOGAR" ou "PLAY" conforme idioma ativo.
/// </summary>
public static class Locale
{
    public enum Language { PT, EN }

    public static Language Current { get; set; } = Language.PT;

    /// <summary>Retorna a string traduzida para o idioma ativo.</summary>
    public static string Tr(string key)
    {
        if (Strings.TryGetValue(key, out var entry))
            return entry.TryGetValue(Current, out var val) ? val : key;
        return key;
    }

    /// <summary>Retorna a string formatada com args (usa string.Format).</summary>
    public static string Tr(string key, params object[] args)
    {
        string fmt = Tr(key);
        return string.Format(fmt, args);
    }

    private static readonly Dictionary<string, Dictionary<Language, string>> Strings = new()
    {
        // ── Menu Principal ─────────────────────────────────────────────
        ["TITLE"]           = new() { [Language.PT] = "GUITAR HERO 3D",       [Language.EN] = "GUITAR HERO 3D" },
        ["PLAY"]            = new() { [Language.PT] = "JOGAR",                [Language.EN] = "PLAY" },
        ["QUIT"]            = new() { [Language.PT] = "Sair",                 [Language.EN] = "Quit" },
        ["LANGUAGE"]        = new() { [Language.PT] = "Idioma: Português",    [Language.EN] = "Language: English" },

        // ── Controles ──────────────────────────────────────────────────
        ["CONTROLS_HINT"]      = new()
        {
            [Language.PT] = "[A] Verde   [S] Vermelho   [J] Amarelo   [K] Azul   [L] Laranja",
            [Language.EN] = "[A] Green   [S] Red   [J] Yellow   [K] Blue   [L] Orange"
        },
        ["CONTROLS_HINT_GAME"] = new()
        {
            [Language.PT] = "[A] Verde   [S] Vermelho   [J] Amarelo   [K] Azul   [L] Laranja   |   [ESC] Pausar",
            [Language.EN] = "[A] Green   [S] Red   [J] Yellow   [K] Blue   [L] Orange   |   [ESC] Pause"
        },

        // ── Seleção de Música ──────────────────────────────────────────
        ["SELECT_SONG"]     = new() { [Language.PT] = "SELECIONAR MÚSICA",    [Language.EN] = "SELECT SONG" },
        ["BACK"]            = new() { [Language.PT] = "← Voltar",             [Language.EN] = "← Back" },
        ["MISS_SFX_OPTION"] = new() { [Language.PT] = "Som de erro (tecla sem nota)", [Language.EN] = "Error sound (key without note)" },
        ["NO_SONGS"]        = new() { [Language.PT] = "Nenhuma música encontrada em res://Audio/", [Language.EN] = "No songs found in res://Audio/" },

        // ── Dificuldade ────────────────────────────────────────────────
        ["DIFFICULTY"]      = new() { [Language.PT] = "DIFICULDADE",          [Language.EN] = "DIFFICULTY" },
        ["EASY"]            = new() { [Language.PT] = "FÁCIL",                [Language.EN] = "EASY" },
        ["MEDIUM"]          = new() { [Language.PT] = "MÉDIO",                [Language.EN] = "MEDIUM" },
        ["HARD"]            = new() { [Language.PT] = "DIFÍCIL",              [Language.EN] = "HARD" },
        ["EXPERT"]          = new() { [Language.PT] = "EXPERT",               [Language.EN] = "EXPERT" },
        ["NO_DIFFICULTY"]   = new() { [Language.PT] = "Nenhuma dificuldade encontrada neste chart.", [Language.EN] = "No difficulty found in this chart." },

        // ── Loading ────────────────────────────────────────────────────
        ["LOADING"]         = new() { [Language.PT] = "CARREGANDO",           [Language.EN] = "LOADING" },
        ["LOADING_INIT"]    = new() { [Language.PT] = "Inicializando...",     [Language.EN] = "Initializing..." },
        ["LOADING_AUDIO"]   = new() { [Language.PT] = "Lendo arquivo de áudio...", [Language.EN] = "Reading audio file..." },
        ["LOADING_META"]    = new() { [Language.PT] = "Lendo metadados da música...", [Language.EN] = "Reading song metadata..." },
        ["LOADING_NOTES_FMT"] = new() { [Language.PT] = "Gerando notas (BPM {0}, {1} beats)...", [Language.EN] = "Generating notes (BPM {0}, {1} beats)..." },
        ["LOADING_READY"]   = new() { [Language.PT] = "Pronto!",              [Language.EN] = "Ready!" },
        ["ERR_NOT_FOUND"]   = new() { [Language.PT] = "arquivo não encontrado", [Language.EN] = "file not found" },
        ["ERR_NOT_IMPORTED"] = new() { [Language.PT] = "não importado (abra o editor Godot para importar)", [Language.EN] = "not imported (open Godot editor to import)" },
        ["ERR_UNSUPPORTED"] = new() { [Language.PT] = "formato não suportado (.opus não é aceito — use .ogg/.mp3/.wav)", [Language.EN] = "unsupported format (.opus not accepted — use .ogg/.mp3/.wav)" },

        // ── Jogo (Pause / HUD) ─────────────────────────────────────────
        ["PAUSED"]          = new() { [Language.PT] = "PAUSADO",              [Language.EN] = "PAUSED" },
        ["RESUME"]          = new() { [Language.PT] = "Continuar",            [Language.EN] = "Resume" },
        ["RESTART"]         = new() { [Language.PT] = "Recomeçar",            [Language.EN] = "Restart" },
        ["QUIT_TO_MENU"]    = new() { [Language.PT] = "Sair para Menu",       [Language.EN] = "Quit to Menu" },

        // ── Feedback de gameplay ───────────────────────────────────────
        ["PERFECT"]         = new() { [Language.PT] = "PERFECT!",             [Language.EN] = "PERFECT!" },
        ["GREAT"]           = new() { [Language.PT] = "GREAT",                [Language.EN] = "GREAT" },
        ["GOOD"]            = new() { [Language.PT] = "GOOD",                 [Language.EN] = "GOOD" },
        ["HOLD"]            = new() { [Language.PT] = "HOLD!",                [Language.EN] = "HOLD!" },
        ["MISS"]            = new() { [Language.PT] = "MISS",                 [Language.EN] = "MISS" },
        ["COMBO_FMT"]       = new() { [Language.PT] = "x{0} Combo",           [Language.EN] = "x{0} Combo" },

        // ── Resultados ─────────────────────────────────────────────────
        ["RESULT"]          = new() { [Language.PT] = "RESULTADO",            [Language.EN] = "RESULTS" },
        ["SCORE_FMT"]       = new() { [Language.PT] = "Score: {0}",           [Language.EN] = "Score: {0}" },
        ["ACCURACY_FMT"]    = new() { [Language.PT] = "Precisão: {0}%",       [Language.EN] = "Accuracy: {0}%" },
        ["HITS_FMT"]        = new() { [Language.PT] = "Acertos: {0} / {1}",   [Language.EN] = "Hits: {0} / {1}" },
        ["MISSES_FMT"]      = new() { [Language.PT] = "Erros: {0}",           [Language.EN] = "Misses: {0}" },
        ["HOLDS_FMT"]       = new() { [Language.PT] = "Holds completos: {0}", [Language.EN] = "Holds completed: {0}" },
        ["MAX_COMBO_FMT"]   = new() { [Language.PT] = "Max Combo: {0}x",      [Language.EN] = "Max Combo: {0}x" },
        ["PLAY_AGAIN"]      = new() { [Language.PT] = "JOGAR NOVAMENTE",      [Language.EN] = "PLAY AGAIN" },
        ["MAIN_MENU"]       = new() { [Language.PT] = "Menu Principal",       [Language.EN] = "Main Menu" },
    };
}
