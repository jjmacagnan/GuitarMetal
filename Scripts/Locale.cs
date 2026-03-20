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
        ["TITLE"]           = new() { [Language.PT] = "GUITAR METAL",         [Language.EN] = "GUITAR METAL" },
        ["PLAY"]            = new() { [Language.PT] = "Jogar",                [Language.EN] = "Play" },
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
        ["SELECT_SONG"]     = new() { [Language.PT] = "Selecionar Música",    [Language.EN] = "Select Song" },
        ["BACK"]            = new() { [Language.PT] = "← Voltar",             [Language.EN] = "← Back" },
        ["MISS_SFX_OPTION"] = new() { [Language.PT] = "Som de erro (tecla sem nota)", [Language.EN] = "Error sound (key without note)" },
        ["NO_SONGS"]        = new() { [Language.PT] = "Nenhuma música encontrada em res://Audio/", [Language.EN] = "No songs found in res://Audio/" },

        // ── Dificuldade ────────────────────────────────────────────────
        ["DIFFICULTY"]      = new() { [Language.PT] = "Dificuldade",          [Language.EN] = "Difficulty" },
        ["EASY"]            = new() { [Language.PT] = "Fácil",                [Language.EN] = "Easy" },
        ["MEDIUM"]          = new() { [Language.PT] = "Médio",                [Language.EN] = "Medium" },
        ["HARD"]            = new() { [Language.PT] = "Difícil",              [Language.EN] = "Hard" },
        ["EXPERT"]          = new() { [Language.PT] = "Expert",               [Language.EN] = "Expert" },
        ["NO_DIFFICULTY"]   = new() { [Language.PT] = "Nenhuma dificuldade encontrada neste chart.", [Language.EN] = "No difficulty found in this chart." },

        // ── Loading ────────────────────────────────────────────────────
        ["LOADING"]         = new() { [Language.PT] = "Carregando",           [Language.EN] = "Loading" },
        ["LOADING_INIT"]    = new() { [Language.PT] = "Inicializando...",     [Language.EN] = "Initializing..." },
        ["LOADING_AUDIO"]   = new() { [Language.PT] = "Lendo arquivo de áudio...", [Language.EN] = "Reading audio file..." },
        ["LOADING_META"]    = new() { [Language.PT] = "Lendo metadados da música...", [Language.EN] = "Reading song metadata..." },
        ["LOADING_NOTES_FMT"] = new() { [Language.PT] = "Gerando notas (BPM {0}, {1} beats)...", [Language.EN] = "Generating notes (BPM {0}, {1} beats)..." },
        ["LOADING_READY"]   = new() { [Language.PT] = "Pronto!",              [Language.EN] = "Ready!" },
        ["ERR_NOT_FOUND"]   = new() { [Language.PT] = "arquivo não encontrado", [Language.EN] = "file not found" },
        ["ERR_NOT_IMPORTED"] = new() { [Language.PT] = "não importado (abra o editor Godot para importar)", [Language.EN] = "not imported (open Godot editor to import)" },
        ["ERR_UNSUPPORTED"] = new() { [Language.PT] = "formato não suportado (.opus não é aceito — use .ogg/.mp3/.wav)", [Language.EN] = "unsupported format (.opus not accepted — use .ogg/.mp3/.wav)" },

        // ── Jogo (Pause / HUD) ─────────────────────────────────────────
        ["PAUSED"]          = new() { [Language.PT] = "Pausado",              [Language.EN] = "Paused" },
        ["RESUME"]          = new() { [Language.PT] = "Continuar",            [Language.EN] = "Resume" },
        ["RESTART"]         = new() { [Language.PT] = "Recomeçar",            [Language.EN] = "Restart" },
        ["QUIT_TO_MENU"]    = new() { [Language.PT] = "Sair para Menu",       [Language.EN] = "Quit to Menu" },

        // ── Feedback de gameplay ───────────────────────────────────────
        ["PERFECT"]         = new() { [Language.PT] = "Perfect!",             [Language.EN] = "Perfect!" },
        ["GREAT"]           = new() { [Language.PT] = "Great",                [Language.EN] = "Great" },
        ["GOOD"]            = new() { [Language.PT] = "Good",                 [Language.EN] = "Good" },
        ["HOLD"]            = new() { [Language.PT] = "Hold!",                [Language.EN] = "Hold!" },
        ["MISS"]            = new() { [Language.PT] = "Miss",                 [Language.EN] = "Miss" },
        ["COMBO_FMT"]       = new() { [Language.PT] = "x{0} Combo",           [Language.EN] = "x{0} Combo" },

        // ── Resultados ─────────────────────────────────────────────────
        ["RESULT"]          = new() { [Language.PT] = "Resultado",            [Language.EN] = "Results" },
        ["SCORE_FMT"]       = new() { [Language.PT] = "Score: {0}",           [Language.EN] = "Score: {0}" },
        ["ACCURACY_FMT"]    = new() { [Language.PT] = "Precisão: {0}%",       [Language.EN] = "Accuracy: {0}%" },
        ["HITS_FMT"]        = new() { [Language.PT] = "Acertos: {0} / {1}",   [Language.EN] = "Hits: {0} / {1}" },
        ["MISSES_FMT"]      = new() { [Language.PT] = "Erros: {0}",           [Language.EN] = "Misses: {0}" },
        ["HOLDS_FMT"]       = new() { [Language.PT] = "Holds completos: {0}", [Language.EN] = "Holds completed: {0}" },
        ["MAX_COMBO_FMT"]   = new() { [Language.PT] = "Max Combo: {0}x",      [Language.EN] = "Max Combo: {0}x" },
        ["PLAY_AGAIN"]        = new() { [Language.PT] = "Jogar Novamente",    [Language.EN] = "Play Again" },
        ["VIEW_LEADERBOARD"]  = new() { [Language.PT] = "Ver Placar",         [Language.EN] = "View Leaderboard" },
        ["MAIN_MENU"]         = new() { [Language.PT] = "Menu Principal",     [Language.EN] = "Main Menu" },

        // ── Nome do jogador ────────────────────────────────────────────
        ["ENTER_NAME"]      = new() { [Language.PT] = "Digite Seu Nome",     [Language.EN] = "Enter Your Name" },
        ["CONFIRM"]         = new() { [Language.PT] = "Confirmar",           [Language.EN] = "Confirm" },
        ["NAME_PLACEHOLDER"]= new() { [Language.PT] = "Seu nome...",          [Language.EN] = "Your name..." },
        ["PLAYER_FMT"]      = new() { [Language.PT] = "Jogador: {0}",        [Language.EN] = "Player: {0}" },

        // ── Leaderboard ────────────────────────────────────────────────
        ["LEADERBOARD"]     = new() { [Language.PT] = "Ranking",             [Language.EN] = "Leaderboard" },
        ["RANK"]            = new() { [Language.PT] = "#",                   [Language.EN] = "#" },
        ["PLAYER"]          = new() { [Language.PT] = "Jogador",             [Language.EN] = "Player" },
        ["NO_SCORES"]       = new() { [Language.PT] = "Nenhum score registrado.", [Language.EN] = "No scores recorded." },
        ["SELECT_SONG_LB"]  = new() { [Language.PT] = "Selecione uma música para ver o ranking", [Language.EN] = "Select a song to view the ranking" },

        // ── Teclado virtual ──────────────────────────────────────────
        ["KB_SPACE"]        = new() { [Language.PT] = "Espaço",              [Language.EN] = "Space" },
        ["KB_BACKSPACE"]    = new() { [Language.PT] = "Apagar",              [Language.EN] = "Delete" },
        ["KB_CLEAR"]        = new() { [Language.PT] = "Limpar",              [Language.EN] = "Clear" },
        ["CLEAR_SCORES"]    = new() { [Language.PT] = "Limpar Scores",       [Language.EN] = "Clear Scores" },

        // ── Hint de teclas (gerado dinamicamente com as teclas mapeadas) ──
        ["LANE_HINT_FMT"]         = new() { [Language.PT] = "[{0}] {1}",        [Language.EN] = "[{0}] {1}" },
        ["GAMEPAD_LANE_HINT_FMT"] = new() { [Language.PT] = "({0}) {1}",       [Language.EN] = "({0}) {1}" },
        ["KEYBOARD_PREFIX"]       = new() { [Language.PT] = "Teclado:",         [Language.EN] = "Keyboard:" },
        ["GAMEPAD_PREFIX"]        = new() { [Language.PT] = "Controle:",        [Language.EN] = "Gamepad:" },
        ["PAUSE_HINT"]            = new() { [Language.PT] = "[ESC] Pausar",     [Language.EN] = "[ESC] Pause" },
        ["GAMEPAD_PAUSE_HINT"]    = new() { [Language.PT] = "[+] Pausar",       [Language.EN] = "[+] Pause" },

        // ── Configurações / Remapeamento ───────────────────────────────
        ["SETTINGS"]         = new() { [Language.PT] = "Configurações",      [Language.EN] = "Settings" },
        ["SETTINGS_TITLE"]   = new() { [Language.PT] = "Configurações",      [Language.EN] = "Settings" },
        ["KEYBOARD_TAB"]     = new() { [Language.PT] = "Teclado",            [Language.EN] = "Keyboard" },
        ["GAMEPAD_TAB"]      = new() { [Language.PT] = "Controle",           [Language.EN] = "Gamepad" },
        ["LANE_GREEN"]       = new() { [Language.PT] = "Verde",              [Language.EN] = "Green" },
        ["LANE_RED"]         = new() { [Language.PT] = "Vermelho",           [Language.EN] = "Red" },
        ["LANE_YELLOW"]      = new() { [Language.PT] = "Amarelo",            [Language.EN] = "Yellow" },
        ["LANE_BLUE"]        = new() { [Language.PT] = "Azul",               [Language.EN] = "Blue" },
        ["LANE_ORANGE"]      = new() { [Language.PT] = "Laranja",            [Language.EN] = "Orange" },
        ["PRESS_KEY"]        = new() { [Language.PT] = "[ Pressione uma tecla ]",  [Language.EN] = "[ Press a key ]" },
        ["PRESS_BUTTON"]     = new() { [Language.PT] = "[ Pressione um botão ]",   [Language.EN] = "[ Press a button ]" },
        ["RESET_DEFAULTS"]   = new() { [Language.PT] = "Restaurar Padrões",        [Language.EN] = "Reset to Defaults" },
        ["SETTINGS_SAVED"]   = new() { [Language.PT] = "Salvo!",                   [Language.EN] = "Saved!" },

        // ── Créditos e Licença ─────────────────────────────────────────
        ["CREDITS"]                  = new() { [Language.PT] = "Créditos",                        [Language.EN] = "Credits" },
        ["CREDITS_TITLE"]            = new() { [Language.PT] = "Créditos e Licença",              [Language.EN] = "Credits & License" },
        ["CREDITS_ABOUT_TITLE"]      = new() { [Language.PT] = "Sobre o Projeto",                 [Language.EN] = "About the Project" },
        ["CREDITS_ABOUT_TEXT"]       = new()
        {
            [Language.PT] = "Projeto desenvolvido para a disciplina de Desenvolvimento de Jogos para Smartphones, integrante da Especialização em Programação para Dispositivos Móveis oferecida pela UTFPR — Universidade Tecnológica Federal do Paraná.",
            [Language.EN] = "Project developed for the Mobile Game Development course, part of the Mobile Devices Programming Specialization offered by UTFPR — Federal University of Technology of Paraná, Brazil."
        },
        ["CREDITS_DEDICATION_TITLE"] = new() { [Language.PT] = "Dedicatória",                     [Language.EN] = "Dedication" },
        ["CREDITS_DEDICATION_TEXT"]  = new()
        {
            [Language.PT] = "Dedicado à minha esposa Natalí, que ama música e inspira cada nota deste jogo. ♪",
            [Language.EN] = "Dedicated to my wife Natalí, who loves music and inspires every note in this game. ♪"
        },
        ["CREDITS_LICENSE_TITLE"]    = new() { [Language.PT] = "Licença",                         [Language.EN] = "License" },
        ["CREDITS_DEVELOPER"]        = new() { [Language.PT] = "Desenvolvedor",                   [Language.EN] = "Developer" },
        ["CREDITS_GITHUB_PROFILE"]   = new() { [Language.PT] = "GitHub (perfil):",                [Language.EN] = "GitHub (profile):" },
        ["CREDITS_GITHUB_PROJECT"]   = new() { [Language.PT] = "GitHub (projeto):",               [Language.EN] = "GitHub (project):" },
        ["CREDITS_LINKEDIN"]         = new() { [Language.PT] = "LinkedIn:",                       [Language.EN] = "LinkedIn:" },
        ["CREDITS_TECH"]             = new() { [Language.PT] = "Tecnologia",                      [Language.EN] = "Technology" },
        ["CREDITS_TECH_TEXT"]        = new() { [Language.PT] = "Godot 4.6  •  C#  •  .NET 8",    [Language.EN] = "Godot 4.6  •  C#  •  .NET 8" },
    };
}
