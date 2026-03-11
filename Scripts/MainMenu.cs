using Godot;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		var play = GetNodeOrNull<Button>("VBox/PlayButton");
		var quit = GetNodeOrNull<Button>("VBox/QuitButton");

		if (play != null) play.Pressed += OnPlayPressed;
		else GD.PushError("[MainMenu] PlayButton não encontrado!");

		if (quit != null) quit.Pressed += OnQuitPressed;
		else GD.PushError("[MainMenu] QuitButton não encontrado!");

		// Foca Play ao entrar para navegação por controle
		play?.CallDeferred(Control.MethodName.GrabFocus);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// B (ui_cancel) no menu principal → foca o botão Quit
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetNodeOrNull<Button>("VBox/QuitButton")?.GrabFocus();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnPlayPressed() => GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn");
	private void OnQuitPressed() => GetTree().Quit();
}
