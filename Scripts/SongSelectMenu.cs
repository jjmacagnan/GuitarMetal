using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de seleção de música.
///
/// Escaneia res://Audio/ de duas formas:
///   1. Arquivos de áudio diretamente na raiz (formato antigo)
///   2. Subpastas contendo song.ini + notes.mid/notes.chart (formato Clone Hero)
/// </summary>
public partial class SongSelectMenu : Control
{
    private VBoxContainer _songList;
    private Label         _previewLabel;

    private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };

    // Áudios possíveis dentro de uma pasta Clone Hero, em ordem de prioridade
    private static readonly string[] FolderAudioCandidates =
        { "song.ogg", "guitar.ogg", "backing.ogg", "song.mp3", "guitar.mp3", "song.wav" };

    public override void _Ready()
    {
        _songList     = GetNodeOrNull<VBoxContainer>("VBox/ScrollContainer/SongList");
        _previewLabel = GetNodeOrNull<Label>("VBox/PreviewLabel");

        GetNodeOrNull<Button>("VBox/BackButton")
            ?.Connect("pressed", Callable.From(() => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn")));

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

    // ── População da lista ─────────────────────────────────────────────────

    private void PopulateSongs()
    {
        if (_songList == null) return;

        var songs = FindAllSongs("res://Audio/");

        if (songs.Count == 0)
        {
            var empty = new Label { Text = "Nenhuma música encontrada em res://Audio/" };
            _songList.AddChild(empty);
            return;
        }

        Button firstBtn = null;
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

            _songList.AddChild(btn);
            firstBtn ??= btn;
        }

        firstBtn?.CallDeferred(Control.MethodName.GrabFocus);
    }

    // ── Seleção de música ──────────────────────────────────────────────────

    private void OnSongSelected(string audioPath, string name)
    {
        GameData.SelectedSongPath      = audioPath;
        GameData.SelectedSongName      = name;
        GameData.LoadedBPM             = 128f;
        GameData.LoadedStream          = null;
        GameData.PreparedNotes         = null;
        GameData.SelectedDifficulty    = null;
        GameData.AvailableDifficulties = null;

        // Deriva a pasta e caminhos de chart possíveis
        string dir      = GetDir(audioPath);
        string basePath = StripExtension(audioPath);

        List<string> difficulties = ScanAvailableDifficulties(basePath, dir);

        if (difficulties.Count > 1)
        {
            GameData.AvailableDifficulties = difficulties;
            GetTree().ChangeSceneToFile("res://Scenes/DifficultySelect.tscn");
        }
        else
        {
            if (difficulties.Count == 1)
                GameData.SelectedDifficulty = difficulties[0];
            GetTree().ChangeSceneToFile("res://Scenes/Loading.tscn");
        }
    }

    /// <summary>
    /// Tenta encontrar dificuldades em: [basePath].chart → dir/notes.chart → dir/notes.mid
    /// </summary>
    private static List<string> ScanAvailableDifficulties(string basePath, string dir)
    {
        // 1. Mesmo nome que o áudio: "song.chart" ao lado de "song.ogg"
        if (FileAccess.FileExists(basePath + ".chart"))
            return ChartImporter.ScanDifficulties(basePath + ".chart");

        // 2. Clone Hero: notes.chart na mesma pasta
        string notesChart = dir + "notes.chart";
        if (FileAccess.FileExists(notesChart))
            return ChartImporter.ScanDifficulties(notesChart);

        // 3. Clone Hero: notes.mid na mesma pasta
        string notesMid = dir + "notes.mid";
        if (FileAccess.FileExists(notesMid))
            return MidiImporter.ScanDifficulties(notesMid);

        return new List<string>();
    }

    // ── Escaneamento de arquivos ───────────────────────────────────────────

    private static List<(string audioPath, string displayName)> FindAllSongs(string baseDir)
    {
        var result = new List<(string, string)>();
        var access = DirAccess.Open(baseDir);
        if (access == null) return result;

        access.ListDirBegin();
        string entry = access.GetNext();
        while (entry != "")
        {
            string fullPath = baseDir.TrimEnd('/') + "/" + entry;

            if (access.CurrentIsDir())
            {
                // Tenta ler como pasta no formato Clone Hero
                var folderSong = TryScanCloneHeroFolder(fullPath + "/");
                if (folderSong.HasValue) result.Add(folderSong.Value);
            }
            else
            {
                // Arquivo de áudio direto na raiz
                foreach (var ext in AudioExtensions)
                {
                    if (entry.ToLower().EndsWith(ext))
                    {
                        result.Add((fullPath, CleanName(entry)));
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
    /// Verifica se uma subpasta contém um chart válido (notes.mid, notes.chart ou áudio) e retorna
    /// o caminho de áudio + nome para exibição. Retorna null se a pasta não for reconhecida.
    /// </summary>
    private static (string audioPath, string displayName)? TryScanCloneHeroFolder(string dir)
    {
        // Precisa ter pelo menos um arquivo de áudio reconhecido
        string audioPath = null;
        foreach (var candidate in FolderAudioCandidates)
        {
            if (FileAccess.FileExists(dir + candidate)) { audioPath = dir + candidate; break; }
        }
        if (audioPath == null) return null;

        // Nome padrão = nome da pasta
        string folderName = dir.TrimEnd('/');
        folderName = folderName[(folderName.LastIndexOf('/') + 1)..];

        // Sobrescreve com song.ini se disponível
        string iniPath = dir + "song.ini";
        if (FileAccess.FileExists(iniPath))
        {
            var info = SongIniReader.Read(iniPath);
            string fromIni = SongIniReader.BuildDisplayName(info, folderName);
            if (!string.IsNullOrEmpty(fromIni)) folderName = fromIni;
        }

        return (audioPath, folderName);
    }

    // ── Utilitários ────────────────────────────────────────────────────────

    private static string GetDir(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path[..(slash + 1)] : "";
    }

    private static string StripExtension(string path)
    {
        int dot = path.LastIndexOf('.');
        return dot >= 0 ? path[..dot] : path;
    }

    /// <summary>Remove extensão e numeração inicial: "02 Master of Puppets.ogg" → "Master of Puppets"</summary>
    private static string CleanName(string fileName)
    {
        int dot  = fileName.LastIndexOf('.');
        string name = dot >= 0 ? fileName[..dot] : fileName;
        int i = 0;
        while (i < name.Length && (char.IsDigit(name[i]) || name[i] == '.' || name[i] == ' '))
            i++;
        return i > 0 && i < name.Length ? name[i..].Trim() : name.Trim();
    }
}
