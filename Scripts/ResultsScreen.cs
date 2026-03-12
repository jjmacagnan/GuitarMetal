using Godot;

public partial class ResultsScreen : Control
{
    public override void _Ready()
    {
        // Título
        SetLabel("VBox/TitleLabel", Locale.Tr("RESULT"));

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

        var playAgain = GetNodeOrNull<Button>("VBox/PlayAgainButton");
        var menu      = GetNodeOrNull<Button>("VBox/MenuButton");

        if (playAgain != null)
        {
            playAgain.Text = Locale.Tr("PLAY_AGAIN");
            playAgain.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn");
        }
        else GD.PushError("[ResultsScreen] PlayAgainButton não encontrado!");

        if (menu != null)
        {
            menu.Text = Locale.Tr("MAIN_MENU");
            menu.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        }
        else GD.PushError("[ResultsScreen] MenuButton não encontrado!");

        // Foca o botão "Jogar Novamente" para navegação por controle
        playAgain?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // B (ui_cancel) → volta ao menu principal
        if (@event.IsActionPressed("ui_cancel"))
        {
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetLabel(string path, string text)
    {
        var label = GetNodeOrNull<Label>(path);
        if (label != null) label.Text = text;
    }
}
