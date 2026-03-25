using Godot;

public partial class MainMenu : Control
{
	private Label  _titleLabel;
	private Button _playButton;
	private Button _quitButton;
	private Button _langButton;
	private Button _leaderboardButton;
	private Button _creditsButton;
	private Button _settingsButton;
	private Label  _controlsLabel;

	public override void _Ready()
	{
		_titleLabel        = GetNodeOrNull<Label>("VBox/TitleLabel");
		_playButton        = GetNodeOrNull<Button>("VBox/PlayButton");
		_quitButton        = GetNodeOrNull<Button>("VBox/QuitButton");
		_langButton        = GetNodeOrNull<Button>("VBox/LanguageButton");
		_leaderboardButton = GetNodeOrNull<Button>("VBox/LeaderboardButton");
		_creditsButton     = GetNodeOrNull<Button>("VBox/CreditsButton");
		_settingsButton    = GetNodeOrNull<Button>("VBox/SettingsButton");
		_controlsLabel     = GetNodeOrNull<Label>("ControlsLabel");

		if (_playButton != null) _playButton.Pressed += OnPlayPressed;
		else GD.PushError("[MainMenu] PlayButton não encontrado!");

		if (_quitButton != null) _quitButton.Pressed += OnQuitPressed;
		else GD.PushError("[MainMenu] QuitButton não encontrado!");

		if (_langButton != null) _langButton.Pressed += OnLanguageToggle;
		if (_leaderboardButton != null) _leaderboardButton.Pressed += OnLeaderboardPressed;
		if (_creditsButton     != null) _creditsButton.Pressed     += OnCreditsPressed;
		if (_settingsButton    != null) _settingsButton.Pressed    += OnSettingsPressed;

		// Aplica bindings salvos ao InputMap assim que o menu carrega
		KeybindingStorage.ApplyToInputMap();

		// Foca Play ao entrar para navegação por controle
		_playButton?.CallDeferred(Control.MethodName.GrabFocus);

		ApplyLocale();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// B (ui_cancel) no menu principal → foca o botão Quit
		if (@event.IsActionPressed("ui_cancel"))
		{
			_quitButton?.GrabFocus();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnPlayPressed()         => GetTree().ChangeSceneToFile(ScenePaths.NameInput);
	private void OnQuitPressed()         => GetTree().Quit();
	private void OnLeaderboardPressed()  => GetTree().ChangeSceneToFile(ScenePaths.Leaderboard);
	private void OnCreditsPressed()      => GetTree().ChangeSceneToFile(ScenePaths.Credits);
	private void OnSettingsPressed()     => GetTree().ChangeSceneToFile(ScenePaths.Settings);

	private void OnLanguageToggle()
	{
		Locale.Current = Locale.Current == Locale.Language.PT
			? Locale.Language.EN
			: Locale.Language.PT;
		ApplyLocale();
	}

	private void ApplyLocale()
	{
		if (_titleLabel        != null) _titleLabel.Text        = Locale.Tr("TITLE");
		if (_playButton        != null) _playButton.Text        = Locale.Tr("PLAY");
		if (_quitButton        != null) _quitButton.Text        = Locale.Tr("QUIT");
		if (_langButton        != null) _langButton.Text        = Locale.Tr("LANGUAGE");
		if (_leaderboardButton != null) _leaderboardButton.Text = Locale.Tr("LEADERBOARD");
		if (_creditsButton     != null) _creditsButton.Text     = Locale.Tr("CREDITS");
		if (_settingsButton    != null) _settingsButton.Text    = Locale.Tr("SETTINGS");
		if (_controlsLabel     != null) _controlsLabel.Text     = KeybindingStorage.BuildControlsHint(includePauseHint: true);
	}
}
