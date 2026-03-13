using Godot;

/// <summary>
/// Tela de entrada de nome do jogador.
/// Pré-preenche com o último nome usado (persistido em user://player.cfg).
/// </summary>
public partial class NameInput : Control
{
	private Label    _titleLabel;
	private LineEdit _nameEdit;
	private Button   _confirmButton;

	public override void _Ready()
	{
		_titleLabel    = GetNodeOrNull<Label>("VBox/TitleLabel");
		_nameEdit      = GetNodeOrNull<LineEdit>("VBox/NameEdit");
		_confirmButton = GetNodeOrNull<Button>("VBox/ConfirmButton");

		if (_titleLabel    != null) _titleLabel.Text    = Locale.Tr("ENTER_NAME");
		if (_confirmButton != null) _confirmButton.Text = Locale.Tr("CONFIRM");

		if (_nameEdit != null)
		{
			_nameEdit.PlaceholderText = Locale.Tr("NAME_PLACEHOLDER");
			_nameEdit.MaxLength       = 20;

			// Pré-preenche com último nome usado ou nome atual da sessão
			string lastName = !string.IsNullOrEmpty(GameData.PlayerName)
				? GameData.PlayerName
				: ScoreStorage.LoadLastPlayerName();
			if (!string.IsNullOrEmpty(lastName))
				_nameEdit.Text = lastName;

			_nameEdit.TextSubmitted += (_) => OnConfirm();
			_nameEdit.CallDeferred(Control.MethodName.GrabFocus);
		}

		if (_confirmButton != null) _confirmButton.Pressed += OnConfirm;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnConfirm()
	{
		string name = _nameEdit?.Text?.Trim() ?? "";
		if (string.IsNullOrEmpty(name)) return;

		GameData.PlayerName = name;
		ScoreStorage.SavePlayerName(name);
		GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn");
	}
}
