using Godot;

public partial class ResultsScreen : Control
{
    public override void _Ready()
    {
        // Título
        SetLabel("VBox/TitleLabel", Locale.Tr("RESULT"));

        // Nome do jogador
        if (!string.IsNullOrEmpty(GameData.PlayerName))
            SetLabel("VBox/PlayerLabel", Locale.Tr("PLAYER_FMT", GameData.PlayerName));

        SetLabel("VBox/GradeLabel",    GameData.Grade);
        SetLabel("VBox/ScoreLabel",    Locale.Tr("SCORE_FMT", $"{GameData.Score:N0}"));
        SetLabel("VBox/AccLabel",      Locale.Tr("ACCURACY_FMT", $"{GameData.Accuracy:F1}"));
        SetLabel("VBox/HitLabel",      Locale.Tr("HITS_FMT", GameData.NotesHit, GameData.TotalNotes));
        SetLabel("VBox/MissLabel",     Locale.Tr("MISSES_FMT", GameData.NotesMissed));
        SetLabel("VBox/HoldsLabel",    Locale.Tr("HOLDS_FMT", GameData.HoldsComplete));
        // FIX M6: Exibe o maior combo alcançado. O nó MaxComboLabel deve existir na cena.
        SetLabel("VBox/MaxComboLabel", Locale.Tr("MAX_COMBO_FMT", GameData.MaxCombo));

        var gradeLabel = GetNodeOrNull<Label>("VBox/GradeLabel");
        if (gradeLabel != null)
        {
            Color c = GameData.Grade switch
            {
                "S" => new Color(0.2f, 1.0f, 1.0f),
                "A" => new Color(0.2f, 1.0f, 0.3f),
                "B" => new Color(1.0f, 0.85f, 0.0f),
                "C" => new Color(1.0f, 0.5f,  0.0f),
                _   => new Color(0.9f, 0.1f,  0.1f),
            };
            gradeLabel.AddThemeColorOverride("font_color", c);
        }

        var playAgain    = GetNodeOrNull<Button>("VBox/PlayAgainButton");
        var leaderboard  = GetNodeOrNull<Button>("VBox/LeaderboardButton");
        var menu         = GetNodeOrNull<Button>("VBox/MenuButton");

        if (playAgain != null)
        {
            playAgain.Text = Locale.Tr("PLAY_AGAIN");
            playAgain.Pressed += () => GetTree().ChangeSceneToFile(ScenePaths.SongSelect);
        }
        else GD.PushError("[ResultsScreen] PlayAgainButton não encontrado!");

        if (leaderboard != null)
        {
            leaderboard.Text = Locale.Tr("VIEW_LEADERBOARD");
            leaderboard.Pressed += () => GetTree().ChangeSceneToFile(ScenePaths.Leaderboard);
        }
        else GD.PushError("[ResultsScreen] LeaderboardButton não encontrado!");

        if (menu != null)
        {
            menu.Text = Locale.Tr("MAIN_MENU");
            menu.Pressed += () => GetTree().ChangeSceneToFile(ScenePaths.MainMenu);
        }
        else GD.PushError("[ResultsScreen] MenuButton não encontrado!");

        // Foca o botão "Jogar Novamente" para navegação por controle
        playAgain?.CallDeferred(Control.MethodName.GrabFocus);

        // Salva score no leaderboard
        SaveScore();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // B (ui_cancel) → volta ao menu principal
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile(ScenePaths.MainMenu);
            GetViewport().SetInputAsHandled();
        }
    }

    private void SaveScore()
    {
        if (string.IsNullOrEmpty(GameData.SelectedSongName)) return;

        var entry = new ScoreStorage.ScoreEntry
        {
            PlayerName = GameData.PlayerName,
            Score      = GameData.Score,
            Accuracy   = GameData.Accuracy,
            Grade      = GameData.Grade,
            MaxCombo   = GameData.MaxCombo,
            Difficulty = GameData.SelectedDifficulty ?? "",
            Date       = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };

        ScoreStorage.Save(GameData.SelectedSongName, entry);
        GD.Print($"[Results] Score salvo: {entry.PlayerName} - {entry.Score} ({entry.Grade})");
    }

    private void SetLabel(string path, string text)
    {
        var label = GetNodeOrNull<Label>(path);
        if (label != null) label.Text = text;
    }
}
