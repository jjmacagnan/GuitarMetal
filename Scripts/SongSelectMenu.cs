using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de seleção de música.
///
/// Escaneia res://Audio/ de duas formas:
///   1. Subpastas no formato Clone Hero / Enchor:
///         AudioName/song.ogg (ou guitar.ogg / backing.ogg / song.mp3 / song.wav)
///         + notes.chart  +  song.ini  (+ album.jpg opcional)
///   2. Arquivos de áudio soltos na raiz (formato legado)
///
/// Nota: Godot 4 não importa .opus — converta para .ogg com:
///   ffmpeg -i song.opus song.ogg
/// </summary>
public partial class SongSelectMenu : Control
{
    private VBoxContainer   _songList;
    private ScrollContainer _scrollContainer;
    private Label           _previewLabel;
    private CheckBox        _missSfxCheck;
    private Label           _titleLabel;
    private Button          _backButton;
    private AudioStreamPlayer _previewPlayer;

    private readonly List<Button> _songButtons = new();

    // Extensões de áudio reconhecidas (em ordem de prioridade dentro de uma pasta)
    // .opus NÃO está na lista — Godot 4 não suporta o formato Opus.
    private static readonly string[] FolderAudioCandidates =
        { "song.ogg", "guitar.ogg", "backing.ogg", "song.mp3", "song.wav" };

    // Extensões para varredura de arquivos soltos
    private static readonly string[] LooseAudioExtensions =
        { ".ogg", ".mp3", ".wav" };

    public override void _Ready()
    {
        _songList        = GetNodeOrNull<VBoxContainer>("VBox/ScrollContainer/SongList");
        _scrollContainer = GetNodeOrNull<ScrollContainer>("VBox/ScrollContainer");
        _previewLabel    = GetNodeOrNull<Label>("VBox/PreviewLabel");
        _titleLabel      = GetNodeOrNull<Label>("VBox/TitleLabel");
        _backButton      = GetNodeOrNull<Button>("VBox/BackButton");

        _backButton?.Connect("pressed", Callable.From(() => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn")));

        _previewPlayer = new AudioStreamPlayer { VolumeDb = -8f };
        AddChild(_previewPlayer);

        _missSfxCheck = GetNodeOrNull<CheckBox>("VBox/MissSfxCheck");
        if (_missSfxCheck != null)
        {
            _missSfxCheck.ButtonPressed = GameData.MissSfxEnabled;
            _missSfxCheck.Toggled += (on) => GameData.MissSfxEnabled = on;
        }

        ApplyLocale();
        PopulateSongs();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyLocale()
    {
        if (_titleLabel   != null) _titleLabel.Text   = Locale.Tr("SELECT_SONG");
        if (_backButton   != null) _backButton.Text   = Locale.Tr("BACK");
        if (_missSfxCheck != null) _missSfxCheck.Text = Locale.Tr("MISS_SFX_OPTION");
    }

    // ── Lista de músicas ───────────────────────────────────────────────────

    private void PopulateSongs()
    {
        if (_songList == null) return;

        var songs = FindAllSongs("res://Audio/");

        if (songs.Count == 0)
        {
            var empty = new Label { Text = Locale.Tr("NO_SONGS") };
            _songList.AddChild(empty);
            return;
        }

        _songButtons.Clear();

        foreach (var (path, name) in songs)
        {
            string capturedPath = path;
            string capturedName = name;

            var btn = new Button
            {
                Text              = capturedName,
                CustomMinimumSize = new Vector2(0, 64),
                FocusMode         = Control.FocusModeEnum.All,
            };
            btn.AddThemeFontSizeOverride("font_size", 22);
            btn.Pressed += () => OnSongSelected(capturedPath, capturedName);

            // Auto-scroll e preview de áudio ao receber foco
            btn.FocusEntered += () =>
            {
                _scrollContainer?.EnsureControlVisible(btn);
                PlayPreview(capturedPath);
            };

            _songList.AddChild(btn);
            _songButtons.Add(btn);
        }

        // Configura vizinhos de foco para que a navegação percorra todos os
        // botões dentro do ScrollContainer antes de sair para MissSfxCheck/BackButton
        CallDeferred(MethodName.SetupFocusNeighbors);

        if (_songButtons.Count > 0)
            _songButtons[0].CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>
    /// Configura FocusNeighborBottom/Top de cada botão para que a navegação
    /// por D-pad/analógico percorra toda a lista antes de sair do ScrollContainer.
    /// O último botão aponta para MissSfxCheck; MissSfxCheck aponta de volta
    /// para o último botão ao subir.
    /// </summary>
    private void SetupFocusNeighbors()
    {
        for (int i = 0; i < _songButtons.Count; i++)
        {
            var btn = _songButtons[i];

            // Vizinho de cima: botão anterior (primeiro botão não define → mantém padrão)
            if (i > 0)
                btn.FocusNeighborTop = _songButtons[i - 1].GetPath();

            // Vizinho de baixo: próximo botão na lista
            if (i < _songButtons.Count - 1)
                btn.FocusNeighborBottom = _songButtons[i + 1].GetPath();
        }

        // Último botão → MissSfxCheck (se existir) ou BackButton
        if (_songButtons.Count > 0)
        {
            var lastBtn = _songButtons[^1];
            if (_missSfxCheck != null)
            {
                lastBtn.FocusNeighborBottom      = _missSfxCheck.GetPath();
                _missSfxCheck.FocusNeighborTop    = lastBtn.GetPath();
            }
            else if (_backButton != null)
            {
                lastBtn.FocusNeighborBottom = _backButton.GetPath();
            }
        }
    }

    // ── Seleção ────────────────────────────────────────────────────────────

    private void OnSongSelected(string audioPath, string name)
    {
        GameData.SelectedSongPath      = audioPath;
        GameData.SelectedSongName      = name;
        GameData.LoadedBPM             = 128f;
        GameData.LoadedStream          = null;
        GameData.PreparedNotes         = null;
        GameData.SelectedDifficulty    = null;
        GameData.AvailableDifficulties = null;

        string dir      = DirOf(audioPath);
        string basePath = StripExt(audioPath);

        // Procura chart: notes.chart → [nome].chart → notes.mid
        string chartPath = FileAccess.FileExists(dir + "notes.chart")
            ? dir + "notes.chart"
            : basePath + ".chart";

        var difficulties = ChartImporter.ScanDifficulties(chartPath);

        // Fallback: tenta notes.mid (formato Rock Band / Harmonix)
        if (difficulties.Count == 0)
        {
            string midiPath = dir + "notes.mid";
            difficulties = MidiImporter.ScanDifficulties(midiPath);
        }

        if (difficulties.Count > 1)
        {
            GameData.AvailableDifficulties = difficulties;
            GetTree().ChangeSceneToFile("res://Scenes/DifficultySelect.tscn");
        }
        else
        {
            if (difficulties.Count == 1) GameData.SelectedDifficulty = difficulties[0];
            GetTree().ChangeSceneToFile("res://Scenes/Loading.tscn");
        }
    }

    // ── Preview de áudio ───────────────────────────────────────────────────

    private void PlayPreview(string audioPath)
    {
        var stream = ResourceLoader.Load<AudioStream>(audioPath);
        if (stream == null) return;
        _previewPlayer.Stream = stream;
        _previewPlayer.Play();
    }

    // ── Scanner ────────────────────────────────────────────────────────────

    private static List<(string audioPath, string displayName)> FindAllSongs(string baseDir)
    {
        var result = new List<(string, string)>();
        var access = DirAccess.Open(baseDir);
        if (access == null) return result;

        access.ListDirBegin();
        string entry = access.GetNext();
        while (entry != "")
        {
            string full = baseDir.TrimEnd('/') + "/" + entry;

            if (access.CurrentIsDir())
            {
                // Pasta no formato Clone Hero / Enchor
                var song = TryScanSongFolder(full + "/");
                if (song.HasValue) result.Add(song.Value);
            }
            else
            {
                // Arquivo de áudio solto na raiz de res://Audio/
                foreach (var ext in LooseAudioExtensions)
                {
                    if (entry.ToLower().EndsWith(ext))
                    {
                        result.Add((full, CleanName(entry)));
                        break;
                    }
                }
            }

            entry = access.GetNext();
        }
        access.ListDirEnd();
        return result;
    }

    /// <summary>
    /// Verifica se a pasta tem pelo menos um arquivo de áudio reconhecido E importado.
    /// Prioridade: candidatos padrão com .import → qualquer áudio com .import → candidatos sem .import.
    /// Retorna o caminho do áudio e o nome para exibição (via song.ini ou nome da pasta).
    /// </summary>
    private static (string audioPath, string displayName)? TryScanSongFolder(string dir)
    {
        string audioPath = null;

        // 1ª passagem: candidato padrão que já foi importado pelo Godot (.import presente)
        foreach (var candidate in FolderAudioCandidates)
        {
            string p = dir + candidate;
            if (FileAccess.FileExists(p) && FileAccess.FileExists(p + ".import"))
            {
                audioPath = p;
                break;
            }
        }

        // 2ª passagem: qualquer arquivo de áudio na pasta que possua .import
        if (audioPath == null)
        {
            var access = DirAccess.Open(dir);
            if (access != null)
            {
                access.ListDirBegin();
                string entry = access.GetNext();
                while (entry != "" && audioPath == null)
                {
                    if (!access.CurrentIsDir())
                    {
                        string lower = entry.ToLower();
                        foreach (var ext in LooseAudioExtensions)
                        {
                            if (lower.EndsWith(ext) && FileAccess.FileExists(dir + entry + ".import"))
                            {
                                audioPath = dir + entry;
                                break;
                            }
                        }
                    }
                    entry = access.GetNext();
                }
                access.ListDirEnd();
            }
        }

        // 3ª passagem (fallback): candidato padrão mesmo sem .import
        // (permite carregar se o Godot importar em tempo de execução ou no próximo scan)
        if (audioPath == null)
        {
            foreach (var candidate in FolderAudioCandidates)
            {
                string p = dir + candidate;
                if (FileAccess.FileExists(p)) { audioPath = p; break; }
            }
        }

        if (audioPath == null) return null;

        // Nome padrão = nome da pasta
        string folderName = dir.TrimEnd('/');
        folderName = folderName[(folderName.LastIndexOf('/') + 1)..];

        // Sobrescreve com song.ini se disponível
        string iniPath = dir + "song.ini";
        if (FileAccess.FileExists(iniPath))
        {
            var info        = SongIniReader.Read(iniPath);
            string fromIni  = SongIniReader.BuildDisplayName(info, folderName);
            if (!string.IsNullOrEmpty(fromIni)) folderName = fromIni;
        }

        return (audioPath, folderName);
    }

    // ── Utilitários ────────────────────────────────────────────────────────

    private static string DirOf(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path[..(slash + 1)] : "";
    }

    private static string StripExt(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot >= 0 ? path[..dot] : path;
    }

    /// <summary>Remove extensão e numeração inicial: "02 Master of Puppets.ogg" → "Master of Puppets"</summary>
    private static string CleanName(string fileName)
    {
        int dot = fileName.LastIndexOf('.');
        string name = dot >= 0 ? fileName[..dot] : fileName;
        int i = 0;
        while (i < name.Length && (char.IsDigit(name[i]) || name[i] == '.' || name[i] == ' '))
            i++;
        return i > 0 && i < name.Length ? name[i..].Trim() : name.Trim();
    }
}
