using Godot;

public partial class ResultsScreen : Control
{
    public override void _Ready()
    {
        SetLabel("VBox/GradeLabel",  GameData.Grade);
        SetLabel("VBox/ScoreLabel",  $"Score: {GameData.Score:N0}");
        SetLabel("VBox/AccLabel",    $"Precisão: {GameData.Accuracy:F1}%");
        SetLabel("VBox/HitLabel",    $"Acertos: {GameData.NotesHit} / {GameData.TotalNotes}");
        SetLabel("VBox/MissLabel",   $"Erros: {GameData.NotesMissed}");
        SetLabel("VBox/HoldsLabel",  $"Holds completos: {GameData.HoldsComplete}");

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

        if (playAgain != null) playAgain.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn");
        else GD.PushError("[ResultsScreen] PlayAgainButton não encontrado!");

        if (menu != null) menu.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        else GD.PushError("[ResultsScreen] MenuButton não encontrado!");
    }

    private void SetLabel(string path, string text)
    {
        var label = GetNodeOrNull<Label>(path);
        if (label != null) label.Text = text;
    }
}
