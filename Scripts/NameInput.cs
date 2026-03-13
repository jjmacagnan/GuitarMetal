using Godot;
using System;

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
	private bool _capsOn = true;
	private readonly System.Collections.Generic.List<Button> _letterButtons = new();
	
	// Cursor para editar texto com teclado físico
	private int _cursorPos = 0;

	// Botão de caps do teclado virtual (para sincronização)
	private Button _capsButton;

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

			// Inicializa cursor no fim do texto existente
			_cursorPos = _nameEdit.Text?.Length ?? 0;
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
			return;
		}

		if (@event is InputEventKey ev)
		{
			// Key pressed (no repeats)
			if (ev.Pressed && !ev.Echo)
			{
				// Backspace
				if (ev.Keycode == Key.Backspace)
				{
					OnBackspace();
					GetViewport().SetInputAsHandled();
					return;
				}

				// Confirm
				if (ev.Keycode == Key.Enter)
				{
					OnConfirm();
					GetViewport().SetInputAsHandled();
					return;
				}


				// Delete
				if (ev.Keycode == Key.Delete)
				{
					OnDelete();
					GetViewport().SetInputAsHandled();
					return;
				}

				// Cursor movement
				if (ev.Keycode == Key.Left)
				{
					MoveCursor(-1);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (ev.Keycode == Key.Right)
				{
					MoveCursor(1);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (ev.Keycode == Key.Home)
				{
					SetCursor(0);
					GetViewport().SetInputAsHandled();
					return;
				}

				if (ev.Keycode == Key.End)
				{
					SetCursor(_nameEdit?.Text?.Length ?? 0);
					GetViewport().SetInputAsHandled();
					return;
				}

					// Printable characters: use Unicode and apply virtual CapsLock/Shift logic for letters
				if (ev.Unicode != 0)
				{
					string raw = char.ConvertFromUtf32((int)ev.Unicode);
					if (raw.Length == 1 && char.IsLetter(raw[0]))
					{
						// If user typed a letter without Shift, infer physical CapsLock state and sync visual if changed
						if (!ev.ShiftPressed)
						{
							bool inferredCaps = char.IsUpper(raw[0]);
							if (inferredCaps != _capsOn)
							{
								_capsOn = inferredCaps;
								UpdateCapsVisual();
							}
						}
						bool shift = ev.ShiftPressed;
						bool upper = _capsOn ^ shift; // XOR: CapsLock inverts Shift
						char baseLower = char.ToLower(raw[0]);
						string outCh = upper ? baseLower.ToString().ToUpper() : baseLower.ToString();
						TypeChar(outCh);
					}
					else
					{
						TypeChar(raw);
					}
					GetViewport().SetInputAsHandled();
					return;
				}
			}
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
				bool isLetter = char.IsLetter(key[0]);
				var btn = CreateKeyButton(key, 48, 48);
				string capturedKey = key;
				btn.Pressed += () => TypeChar(isLetter ? (_capsOn ? capturedKey : capturedKey.ToLower()) : capturedKey);
				hbox.AddChild(btn);
				firstKey ??= btn;
				if (isLetter) _letterButtons.Add(btn);
			}
			kbContainer.AddChild(hbox);
		}

		// Linha de ações: ESPAÇO, APAGAR, LIMPAR
		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", 4);
		actionRow.Alignment = BoxContainer.AlignmentMode.Center;

		var capsBtn = CreateKeyButton("⇧ Aa", 80, 48);
		capsBtn.Pressed += () => ToggleCaps(capsBtn);
		_capsButton = capsBtn;

		var spaceBtn = CreateKeyButton(Locale.Tr("KB_SPACE"), 160, 48);
		spaceBtn.Pressed += () => TypeChar(" ");

		var backBtn = CreateKeyButton(Locale.Tr("KB_BACKSPACE"), 120, 48);
		backBtn.Pressed += OnBackspace;

		var clearBtn = CreateKeyButton(Locale.Tr("KB_CLEAR"), 100, 48);
		clearBtn.Pressed += OnClear;

		actionRow.AddChild(capsBtn);
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
		string current = _nameEdit.Text ?? "";
		if (current.Length >= _nameEdit.MaxLength) return;

		// Insert at cursor position
		int insertPos = Math.Clamp(_cursorPos, 0, current.Length);
		int allowed = Math.Min(ch.Length, _nameEdit.MaxLength - current.Length);
		string toInsert = ch.Length <= allowed ? ch : ch.Substring(0, allowed);
		_nameEdit.Text = current.Insert(insertPos, toInsert);
		_cursorPos = insertPos + toInsert.Length;
	}

	private void ToggleCaps(Button capsBtn)
	{
		_capsOn = !_capsOn;
		UpdateCapsVisual();
	}

	private void UpdateCapsVisual()
	{
		if (_capsButton != null)
			_capsButton.Text = _capsOn ? "⇧ Aa" : "⇩ aA";
		foreach (var btn in _letterButtons)
			btn.Text = _capsOn ? btn.Text.ToUpper() : btn.Text.ToLower();
	}

	private void MoveCursor(int delta)
	{
		if (_nameEdit == null) return;
		int len = _nameEdit.Text?.Length ?? 0;
		_cursorPos = Math.Clamp(_cursorPos + delta, 0, len);
	}

	private void SetCursor(int pos)
	{
		if (_nameEdit == null) return;
		_cursorPos = Math.Clamp(pos, 0, _nameEdit.Text?.Length ?? 0);
	}

	private void OnDelete()
	{
		if (_nameEdit == null) return;
		int len = _nameEdit.Text?.Length ?? 0;
		if (_cursorPos >= len) return;
		_nameEdit.Text = _nameEdit.Text.Remove(_cursorPos, 1);
	}

	private void OnBackspace()
	{
		if (_nameEdit == null) return;
		if (_cursorPos > 0)
		{
			_nameEdit.Text = _nameEdit.Text.Remove(_cursorPos - 1, 1);
			_cursorPos--;
		}
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
