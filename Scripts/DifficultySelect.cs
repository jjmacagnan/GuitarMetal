using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de seleção de dificuldade.
/// Mostra as dificuldades disponíveis no .chart selecionado.
/// Preenche dinamicamente com base em GameData.AvailableDifficulties.
/// </summary>
public partial class DifficultySelect : Control
{
	private static readonly Dictionary<string, (string LabelKey, string Stars, Color Color)> DifficultyInfo = new()
	{
		["EasySingle"]   = ("EASY",   "★☆☆☆", new Color(0.3f, 0.9f, 0.3f)),
		["MediumSingle"] = ("MEDIUM", "★★☆☆", new Color(1.0f, 0.85f, 0.2f)),
		["HardSingle"]   = ("HARD",   "★★★☆", new Color(1.0f, 0.45f, 0.1f)),
		["ExpertSingle"] = ("EXPERT", "★★★★", new Color(1.0f, 0.15f, 0.15f)),
	};

	// Ordem de exibição (da mais fácil para a mais difícil)
	private static readonly string[] DisplayOrder =
		{ "EasySingle", "MediumSingle", "HardSingle", "ExpertSingle" };

	public override void _Ready()
	{
		// Título da tela
		var titleLabel = GetNodeOrNull<Label>("VBox/TitleLabel");
		if (titleLabel != null) titleLabel.Text = Locale.Tr("DIFFICULTY");

		// Título da música
		var songLabel = GetNodeOrNull<Label>("VBox/SongLabel");
		if (songLabel != null)
			songLabel.Text = GameData.SelectedSongName;

		// Botão Voltar
		var backButton = GetNodeOrNull<Button>("VBox/BackButton");
		if (backButton != null)
		{
			backButton.Text = Locale.Tr("BACK");
			backButton.Connect("pressed", Callable.From(
				() => GetTree().ChangeSceneToFile(ScenePaths.SongSelect)));
		}

		// Grid de dificuldades
		var grid = GetNodeOrNull<GridContainer>("VBox/DifficultyGrid");
		if (grid == null)
		{
			GD.PushError("[DifficultySelect] DifficultyGrid não encontrado.");
			return;
		}

		var available = GameData.AvailableDifficulties ?? new List<string>();

		Button firstBtn = null;
		foreach (string diff in DisplayOrder)
		{
			if (!available.Contains(diff)) continue;
			if (!DifficultyInfo.TryGetValue(diff, out var info)) continue;

			string captured = diff;
			var btn = BuildDifficultyButton(Locale.Tr(info.LabelKey), info.Stars, info.Color);
			btn.Pressed += () => OnDifficultySelected(captured);
			grid.AddChild(btn);
			firstBtn ??= btn;
		}

		if (firstBtn == null)
		{
			// Nenhuma dificuldade reconhecida — informa o jogador e volta em 2s
			GD.PushWarning("[DifficultySelect] Nenhuma dificuldade disponível.");
			var msg = new Label
			{
				Text                = Locale.Tr("NO_DIFFICULTY"),
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			msg.AddThemeColorOverride("font_color", Colors.OrangeRed);
			grid.AddChild(msg);

			var timer = GetTree().CreateTimer(2.0f);
			timer.Timeout += () => GetTree().ChangeSceneToFile(ScenePaths.SongSelect);
			return;
		}

		// Modificadores
		BuildModifiers();

		// Foca a primeira dificuldade disponível
		firstBtn.CallDeferred(Control.MethodName.GrabFocus);
	}

	private OptionButton _speedOption;

	private void BuildModifiers()
	{
		var box = GetNodeOrNull<HBoxContainer>("VBox/ModifiersBox");
		if (box == null) return;

		// Mirror
		var mirrorCheck = new CheckBox
		{
			Text          = Locale.Tr("MOD_MIRROR"),
			ButtonPressed = GameData.ModMirror,
			FocusMode     = FocusModeEnum.All,
		};
		mirrorCheck.AddThemeFontSizeOverride("font_size", 16);
		mirrorCheck.Toggled += (on) => GameData.ModMirror = on;
		box.AddChild(mirrorCheck);

		// No Fail
		var noFailCheck = new CheckBox
		{
			Text          = Locale.Tr("MOD_NO_FAIL"),
			ButtonPressed = GameData.ModNoFail,
			FocusMode     = FocusModeEnum.All,
		};
		noFailCheck.AddThemeFontSizeOverride("font_size", 16);
		noFailCheck.Toggled += (on) => GameData.ModNoFail = on;
		box.AddChild(noFailCheck);

		// Speed
		var speedBox = new HBoxContainer();
		speedBox.AddThemeConstantOverride("separation", 8);

		var speedLabel = new Label { Text = Locale.Tr("MOD_SPEED"), VerticalAlignment = VerticalAlignment.Center };
		speedLabel.AddThemeFontSizeOverride("font_size", 16);
		speedLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		speedBox.AddChild(speedLabel);

		_speedOption = new OptionButton
		{
			CustomMinimumSize = new Vector2(110, 36),
			FocusMode         = FocusModeEnum.All,
		};
		_speedOption.AddThemeFontSizeOverride("font_size", 16);
		_speedOption.AddItem("75%",  0);
		_speedOption.AddItem("100%", 1);
		_speedOption.AddItem("125%", 2);
		_speedOption.Selected = GameData.ModSpeedMult switch
		{
			0.75f => 0,
			1.25f => 2,
			_     => 1
		};
		_speedOption.ItemSelected += OnSpeedSelected;
		speedBox.AddChild(_speedOption);

		box.AddChild(speedBox);
	}

	private void OnSpeedSelected(long idx)
	{
		GameData.ModSpeedMult = idx switch
		{
			0 => 0.75f,
			2 => 1.25f,
			_ => 1.0f
		};
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// B (ui_cancel) → volta à seleção de música
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile(ScenePaths.SongSelect);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnDifficultySelected(string difficulty)
	{
		GameData.SelectedDifficulty = difficulty;
		// Aplica speed modifier ao NoteSpeed
		if (GameData.ModSpeedMult != 1.0f)
			GameData.SetNoteSpeedForRun(GameData.DefaultNoteSpeed * GameData.ModSpeedMult);
		GetTree().ChangeSceneToFile(ScenePaths.Loading);
	}

	// ── UI Builder ─────────────────────────────────────────────────────────

	private static Button BuildDifficultyButton(string label, string stars, Color color)
	{
		var btn = new Button
		{
			CustomMinimumSize = new Vector2(260, 120),
			FocusMode         = Control.FocusModeEnum.All,
		};

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;

		var nameLabel = new Label
		{
			Text                = label,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 28);
		nameLabel.AddThemeColorOverride("font_color", color);

		var starsLabel = new Label
		{
			Text                = stars,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		starsLabel.AddThemeFontSizeOverride("font_size", 24);
		starsLabel.AddThemeColorOverride("font_color", color.Lightened(0.3f));

		vbox.AddChild(nameLabel);
		vbox.AddChild(starsLabel);
		btn.AddChild(vbox);

		return btn;
	}
}
