using Godot;

/// <summary>
/// Tela de entrada de nome do jogador com teclado virtual navegável por joystick.
/// </summary>
public partial class NameInput : Control
{
	private Label    _titleLabel;
	private LineEdit _nameEdit;
	private Button   _confirmButton;

	// Teclado virtual
	private GridContainer _keyboardGrid;
	private const int Columns = 10;

	// Layout QWERTY + números + ações
	private static readonly string[][] KeyRows = new[]
	{
		new[] { "Q","W","E","R","T","Y","U","I","O","P" },
		new[] { "A","S","D","F","G","H","J","K","L","_" },
		new[] { "Z","X","C","V","B","N","M",".","-","'" },
		new[] { "1","2","3","4","5","6","7","8","9","0" },
	};

	public override void _Ready()
	{
		_titleLabel    = GetNodeOrNull<Label>("VBox/TitleLabel");
		_nameEdit      = GetNodeOrNull<LineEdit>("VBox/NameEdit");
		_confirmButton = GetNodeOrNull<Button>("VBox/ConfirmButton");

		if (_titleLabel    != null) _titleLabel.Text    = Locale.Tr("ENTER_NAME");
		if (_confirmButton != null)
		{
			_confirmButton.Text = Locale.Tr("CONFIRM");
			_confirmButton.Pressed += OnConfirm;
		}

		if (_nameEdit != null)
		{
			_nameEdit.PlaceholderText = Locale.Tr("NAME_PLACEHOLDER");
			_nameEdit.MaxLength       = 20;
			_nameEdit.Editable        = false; // input vem do teclado virtual

			string lastName = !string.IsNullOrEmpty(GameData.PlayerName)
				? GameData.PlayerName
				: ScoreStorage.LoadLastPlayerName();
			if (!string.IsNullOrEmpty(lastName))
				_nameEdit.Text = lastName;

			_nameEdit.TextSubmitted += (_) => OnConfirm();
		}

		BuildVirtualKeyboard();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildVirtualKeyboard()
	{
		var vbox = GetNodeOrNull<VBoxContainer>("VBox");
		if (vbox == null) return;

		// Container principal do teclado
		var kbContainer = new VBoxContainer();
		kbContainer.Name = "KeyboardContainer";
		kbContainer.AddThemeConstantOverride("separation", 4);

		// Move o botão Confirmar para depois do teclado
		// (remove e re-adiciona no final)
		if (_confirmButton != null) vbox.RemoveChild(_confirmButton);

		Button firstKey = null;

		// Linhas de letras/números
		foreach (var row in KeyRows)
		{
			var hbox = new HBoxContainer();
			hbox.AddThemeConstantOverride("separation", 4);
			hbox.Alignment = BoxContainer.AlignmentMode.Center;

			foreach (string key in row)
			{
				var btn = CreateKeyButton(key, 48, 48);
				btn.Pressed += () => TypeChar(key);
				hbox.AddChild(btn);
				firstKey ??= btn;
			}
			kbContainer.AddChild(hbox);
		}

		// Linha de ações: ESPAÇO, APAGAR, LIMPAR
		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", 4);
		actionRow.Alignment = BoxContainer.AlignmentMode.Center;

		var spaceBtn = CreateKeyButton(Locale.Tr("KB_SPACE"), 200, 48);
		spaceBtn.Pressed += () => TypeChar(" ");

		var backBtn = CreateKeyButton(Locale.Tr("KB_BACKSPACE"), 120, 48);
		backBtn.Pressed += OnBackspace;

		var clearBtn = CreateKeyButton(Locale.Tr("KB_CLEAR"), 100, 48);
		clearBtn.Pressed += OnClear;

		actionRow.AddChild(spaceBtn);
		actionRow.AddChild(backBtn);
		actionRow.AddChild(clearBtn);
		kbContainer.AddChild(actionRow);

		vbox.AddChild(kbContainer);

		// Re-adiciona Confirmar no final
		if (_confirmButton != null) vbox.AddChild(_confirmButton);

		// Foca a primeira tecla para navegação por joystick
		firstKey?.CallDeferred(Control.MethodName.GrabFocus);
	}

	private static Button CreateKeyButton(string text, int width, int height)
	{
		var btn = new Button
		{
			Text              = text,
			CustomMinimumSize = new Vector2(width, height),
			FocusMode         = FocusModeEnum.All,
		};
		btn.AddThemeFontSizeOverride("font_size", 20);
		return btn;
	}

	private void TypeChar(string ch)
	{
		if (_nameEdit == null) return;
		if (_nameEdit.Text.Length >= _nameEdit.MaxLength) return;
		_nameEdit.Text += ch;
	}

	private void OnBackspace()
	{
		if (_nameEdit == null || _nameEdit.Text.Length == 0) return;
		_nameEdit.Text = _nameEdit.Text[..^1];
	}

	private void OnClear()
	{
		if (_nameEdit != null) _nameEdit.Text = "";
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
