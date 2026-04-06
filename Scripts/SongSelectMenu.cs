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
    private CheckBox        _practiceCheck;
    private Label           _titleLabel;
    private Button          _backButton;
    private AudioStreamPlayer _previewPlayer;
    private Button _playingButton;

    private static readonly Color PlayingColor = new(0.2f, 0.9f, 1f);

    private readonly List<Button> _songButtons = new();

    private static readonly string[] FolderAudioCandidates =
        { "song.ogg", "guitar.ogg", "backing.ogg", "song.mp3", "song.wav" };

    private static readonly string[] LooseAudioExtensions =
        { ".ogg", ".mp3", ".wav" };

    public override void _Ready()
    {
        _songList        = GetNodeOrNull<VBoxContainer>("VBox/ScrollContainer/SongList");
        _scrollContainer = GetNodeOrNull<ScrollContainer>("VBox/ScrollContainer");
        _previewLabel    = GetNodeOrNull<Label>("VBox/PreviewLabel");
        _titleLabel      = GetNodeOrNull<Label>("VBox/TitleLabel");
        _backButton      = GetNodeOrNull<Button>("VBox/BackButton");

        if (_backButton != null) _backButton.Pressed += () => GetTree().ChangeSceneToFile(ScenePaths.MainMenu);

        _previewPlayer = new AudioStreamPlayer { VolumeDb = -8f };
        AddChild(_previewPlayer);

        _missSfxCheck = GetNodeOrNull<CheckBox>("VBox/MissSfxCheck");
        if (_missSfxCheck != null)
        {
            _missSfxCheck.ButtonPressed = GameData.MissSfxEnabled;
            _missSfxCheck.Toggled += (on) => GameData.MissSfxEnabled = on;
        }

        // Practice mode checkbox (criado programaticamente)
        _practiceCheck = new CheckBox
        {
            Text = Locale.Tr("PRACTICE_MODE"),
            ButtonPressed = GameData.IsPracticeMode,
            FocusMode = FocusModeEnum.All,
        };
        _practiceCheck.AddThemeFontSizeOverride("font_size", 18);
        _practiceCheck.Toggled += (on) => GameData.IsPracticeMode = on;
        var vbox = GetNodeOrNull<VBoxContainer>("VBox");
        if (vbox != null && _missSfxCheck != null)
            vbox.AddChild(_practiceCheck);
        else
            vbox?.AddChild(_practiceCheck);

        ApplyLocale();
        PopulateSongs();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile(ScenePaths.MainMenu);
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

        var songs = FindAllSongs("res://Audio");

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

            btn.FocusEntered += () =>
            {
                _scrollContainer?.EnsureControlVisible(btn);
                SetPlayingButton(btn);
                PlayPreview(capturedPath);
            };

            _songList.AddChild(btn);
            _songButtons.Add(btn);
        }

        CallDeferred(MethodName.SetupFocusNeighbors);

        if (_songButtons.Count > 0)
            _songButtons[0].CallDeferred(Control.MethodName.GrabFocus);
    }

    private void SetupFocusNeighbors()
    {
        for (int i = 0; i < _songButtons.Count; i++)
        {
            var btn = _songButtons[i];
            if (i > 0)
                btn.FocusNeighborTop = _songButtons[i - 1].GetPath();
            if (i < _songButtons.Count - 1)
                btn.FocusNeighborBottom = _songButtons[i + 1].GetPath();
        }

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

        string chartPath = FileAccess.FileExists(dir + "notes.chart")
            ? dir + "notes.chart"
            : basePath + ".chart";

        var difficulties = ChartImporter.ScanDifficulties(chartPath);

        if (difficulties.Count == 0)
        {
            string midiPath = dir + "notes.mid";
            difficulties = MidiImporter.ScanDifficulties(midiPath);
        }

        if (difficulties.Count > 1)
        {
            GameData.AvailableDifficulties = difficulties;
            GetTree().ChangeSceneToFile(ScenePaths.DifficultySelect);
        }
        else
        {
            if (difficulties.Count == 1)
            {
                GameData.SelectedDifficulty = difficulties[0];
                GetTree().ChangeSceneToFile(ScenePaths.Loading);
            }
            // If difficulties.Count is 0, stay on song select screen
        }
    }

    // ── Preview de áudio ───────────────────────────────────────────────────

    private void SetPlayingButton(Button btn)
    {
        if (_playingButton != null && IsInstanceValid(_playingButton))
        {
            _playingButton.RemoveThemeColorOverride("font_color");
            _playingButton.Text = _playingButton.Text.TrimStart('♪', ' ');
        }

        _playingButton = btn;
        btn.AddThemeColorOverride("font_color", PlayingColor);
        if (!btn.Text.StartsWith("♪"))
            btn.Text = "♪ " + btn.Text;
    }

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
        var result = new List<(string audioPath, string displayName)>();

        string[] dirs  = DirAccess.GetDirectoriesAt(baseDir);
        string[] files = DirAccess.GetFilesAt(baseDir);

        GD.Print($"[SongSelect] {baseDir} → dirs={dirs.Length} files={files.Length}");

        foreach (string dir in dirs)
        {
            string full = baseDir.TrimEnd('/') + "/" + dir + "/";
            var song = TryScanSongFolder(full);
            if (song.HasValue) result.Add(song.Value);
        }

        foreach (string file in files)
        {
            string lower = file.ToLower();
            foreach (var ext in LooseAudioExtensions)
            {
                if (lower.EndsWith(ext))
                {
                    result.Add((baseDir.TrimEnd('/') + "/" + file, CleanName(file)));
                    break;
                }
            }
        }

        return result;
    }

    private static (string audioPath, string displayName)? TryScanSongFolder(string dir)
    {
        string audioPath = null;

        // 1ª passagem: candidatos padrão via ResourceLoader (funciona no PCK do Android)
        foreach (var candidate in FolderAudioCandidates)
        {
            string p = dir + candidate;
            if (ResourceLoader.Exists(p)) { audioPath = p; break; }
        }

        // 2ª passagem: qualquer áudio na pasta
        if (audioPath == null)
        {
            foreach (string entry in DirAccess.GetFilesAt(dir.TrimEnd('/')))
            {
                string lower = entry.ToLower();
                foreach (var ext in LooseAudioExtensions)
                {
                    if (lower.EndsWith(ext)) { audioPath = dir + entry; break; }
                }
                if (audioPath != null) break;
            }
        }

        if (audioPath == null) return null;

        string folderName = dir.TrimEnd('/');
        folderName = folderName[(folderName.LastIndexOf('/') + 1)..];

        string iniPath = dir + "song.ini";
        if (FileAccess.FileExists(iniPath) || ResourceLoader.Exists(iniPath))
        {
            var info       = SongIniReader.Read(iniPath);
            string fromIni = SongIniReader.BuildDisplayName(info, folderName);
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
