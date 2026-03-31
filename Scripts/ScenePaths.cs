/// <summary>
/// Caminhos centralizados das cenas do jogo.
/// Evita strings duplicadas espalhadas pelo projeto.
/// </summary>
public static class ScenePaths
{
	// IMPORTANT: This path is also hardcoded in `project.godot` as the main scene.
	// If you change this constant, you MUST update `project.godot` as well.
	public const string MainMenu         = "res://Scenes/MainMenu.tscn";
	public const string SongSelect       = "res://Scenes/SongSelect.tscn";
	public const string DifficultySelect = "res://Scenes/DifficultySelect.tscn";
	public const string Loading          = "res://Scenes/Loading.tscn";
	public const string Game             = "res://Scenes/Game.tscn";
	public const string Results          = "res://Scenes/Results.tscn";
	public const string Leaderboard      = "res://Scenes/Leaderboard.tscn";
	public const string NameInput        = "res://Scenes/NameInput.tscn";
	public const string Settings         = "res://Scenes/Settings.tscn";
	public const string Credits          = "res://Scenes/Credits.tscn";
}
