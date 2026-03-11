using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de seleção de dificuldade.
/// Mostra as dificuldades disponíveis no .chart selecionado.
/// Preenche dinamicamente com base em GameData.AvailableDifficulties.
/// </summary>
public partial class DifficultySelect : Control
{
	private static readonly Dictionary<string, (string Label, string Stars, Color Color)> DifficultyInfo = new()
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
		// Título da música
		var songLabel = GetNodeOrNull<Label>("VBox/SongLabel");
		if (songLabel != null)
			songLabel.Text = GameData.SelectedSongName;

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
			var btn = BuildDifficultyButton(info.Label, info.Stars, info.Color);
			btn.Pressed += () => OnDifficultySelected(captured);
			grid.AddChild(btn);
			firstBtn ??= btn;
		}

		// Foca a primeira dificuldade disponível
		firstBtn?.CallDeferred(Control.MethodName.GrabFocus);

		// Botão Voltar
		GetNodeOrNull<Button>("VBox/BackButton")
			?.Connect("pressed", Callable.From(
				() => GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn")));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// B (ui_cancel) → volta à seleção de música
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile("res://Scenes/SongSelect.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnDifficultySelected(string difficulty)
	{
		GameData.SelectedDifficulty = difficulty;
		GetTree().ChangeSceneToFile("res://Scenes/Loading.tscn");
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
