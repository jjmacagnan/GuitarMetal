using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de seleção de música.
/// Escaneia res://Audio/ em busca de arquivos de áudio e exibe um botão por música.
/// </summary>
public partial class SongSelectMenu : Control
{
    private VBoxContainer _songList;
    private Label         _previewLabel;

    private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };

    public override void _Ready()
    {
        _songList     = GetNodeOrNull<VBoxContainer>("VBox/ScrollContainer/SongList");
        _previewLabel = GetNodeOrNull<Label>("VBox/PreviewLabel");

        GetNodeOrNull<Button>("VBox/BackButton")
            ?.Connect("pressed", Callable.From(() => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn")));

        PopulateSongs();
    }

    private void PopulateSongs()
    {
        if (_songList == null) return;

        var songs = FindAudioFiles("res://Audio/");

        if (songs.Count == 0)
        {
            var empty = new Label { Text = "Nenhuma música encontrada em res://Audio/" };
            _songList.AddChild(empty);
            return;
        }

        foreach (var (path, name) in songs)
        {
            string capturedPath = path;
            string capturedName = name;

            var btn = new Button
            {
                Text              = capturedName,
                CustomMinimumSize = new Vector2(0, 64)
            };
            btn.AddThemeFontSizeOverride("font_size", 22);
            btn.Pressed += () => OnSongSelected(capturedPath, capturedName);

            _songList.AddChild(btn);
        }
    }

    private void OnSongSelected(string path, string name)
    {
        GameData.SelectedSongPath      = path;
        GameData.SelectedSongName      = name;
        GameData.LoadedBPM             = 128f;
        GameData.LoadedStream          = null;
        GameData.PreparedNotes         = null;
        GameData.SelectedDifficulty    = null;
        GameData.AvailableDifficulties = null;

        // Verifica se existe .chart com múltiplas dificuldades
        int lastDot  = path.LastIndexOf('.');
        string basePath  = lastDot >= 0 ? path[..lastDot] : path;
        string chartPath = basePath + ".chart";

        var difficulties = ChartImporter.ScanDifficulties(chartPath);

        if (difficulties.Count > 1)
        {
            // Múltiplas dificuldades → tela de seleção
            GameData.AvailableDifficulties = difficulties;
            GetTree().ChangeSceneToFile("res://Scenes/DifficultySelect.tscn");
        }
        else
        {
            // 0 ou 1 dificuldade → pula direto para Loading
            if (difficulties.Count == 1)
                GameData.SelectedDifficulty = difficulties[0];
            GetTree().ChangeSceneToFile("res://Scenes/Loading.tscn");
        }
    }

    // ── Utilitários ────────────────────────────────────────────────────────

    private static List<(string path, string name)> FindAudioFiles(string dir)
    {
        var result = new List<(string, string)>();
        var access = DirAccess.Open(dir);
        if (access == null) return result;

        access.ListDirBegin();
        string entry = access.GetNext();
        while (entry != "")
        {
            if (!access.CurrentIsDir())
            {
                foreach (var ext in AudioExtensions)
                {
                    if (entry.ToLower().EndsWith(ext))
                    {
                        string fullPath  = dir.TrimEnd('/') + "/" + entry;
                        string cleanName = CleanName(entry);
                        result.Add((fullPath, cleanName));
                        break;
                    }
                }
            }
            entry = access.GetNext();
        }
        access.ListDirEnd();
        return result;
    }

    /// <summary>Remove extensão e numeração inicial: "02 Master of Puppets.ogg" → "Master of Puppets"</summary>
    private static string CleanName(string fileName)
    {
        // Remove extensão
        int dot = fileName.LastIndexOf('.');
        string name = dot >= 0 ? fileName[..dot] : fileName;

        // Remove prefixo numérico tipo "02 " ou "02. "
        int i = 0;
        while (i < name.Length && (char.IsDigit(name[i]) || name[i] == '.' || name[i] == ' '))
            i++;
        return i > 0 && i < name.Length ? name[i..].Trim() : name.Trim();
    }
}
