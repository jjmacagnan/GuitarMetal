using Godot;
using System.Collections.Generic;

/// <summary>
/// Tela de leaderboard — mostra top 10 scores por música.
/// </summary>
public partial class Leaderboard : Control
{
	private Label         _titleLabel;
	private VBoxContainer _songList;
	private VBoxContainer _scoreList;
	private Label         _scoreTitle;
	private Label         _hintLabel;
	private Button        _backButton;

	// Reutiliza o scanner de músicas do SongSelectMenu
	private static readonly string[] LooseAudioExtensions = { ".ogg", ".mp3", ".wav" };
	private static readonly string[] FolderAudioCandidates =
		{ "song.ogg", "guitar.ogg", "backing.ogg", "song.mp3", "song.wav" };

	public override void _Ready()
	{
		_titleLabel = GetNodeOrNull<Label>("VBox/TitleLabel");
		_songList   = GetNodeOrNull<VBoxContainer>("VBox/HSplit/SongScroll/SongList");
		_scoreList  = GetNodeOrNull<VBoxContainer>("VBox/HSplit/ScorePanel/ScoreScroll/ScoreList");
		_scoreTitle = GetNodeOrNull<Label>("VBox/HSplit/ScorePanel/ScoreTitle");
		_hintLabel  = GetNodeOrNull<Label>("VBox/HSplit/ScorePanel/HintLabel");
		_backButton = GetNodeOrNull<Button>("VBox/BackButton");

		if (_titleLabel != null) _titleLabel.Text = Locale.Tr("LEADERBOARD");
		if (_backButton != null)
		{
			_backButton.Text = Locale.Tr("BACK");
			_backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
		}
		if (_hintLabel != null) _hintLabel.Text = Locale.Tr("SELECT_SONG_LB");

		PopulateSongs();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void PopulateSongs()
	{
		if (_songList == null) return;

		// Pega músicas com scores salvos
		var songsWithScores = ScoreStorage.GetSongsWithScores();

		// Também escaneia as músicas disponíveis em Audio/
		var availableSongs = FindAllSongNames("res://Audio/");

		// Mescla: mostra todas com scores + disponíveis (sem duplicatas)
		var allSongs = new HashSet<string>(songsWithScores);
		foreach (var s in availableSongs) allSongs.Add(s);

		if (allSongs.Count == 0)
		{
			var empty = new Label { Text = Locale.Tr("NO_SCORES") };
			_songList.AddChild(empty);
			return;
		}

		Button firstBtn = null;
		foreach (var songName in allSongs)
		{
			string captured = songName;
			var btn = new Button
			{
				Text              = songName,
				CustomMinimumSize = new Vector2(0, 48),
				FocusMode         = Control.FocusModeEnum.All,
			};
			btn.AddThemeFontSizeOverride("font_size", 18);
			btn.Pressed += () => ShowScores(captured);
			_songList.AddChild(btn);
			firstBtn ??= btn;
		}

		firstBtn?.CallDeferred(Control.MethodName.GrabFocus);
	}

	private void ShowScores(string songName)
	{
		if (_scoreList == null) return;

		// Limpa lista anterior
		foreach (var child in _scoreList.GetChildren())
			child.QueueFree();

		if (_scoreTitle != null) _scoreTitle.Text = songName;
		if (_hintLabel  != null) _hintLabel.Visible = false;

		var scores = ScoreStorage.GetTopScores(songName, 10);

		if (scores.Count == 0)
		{
			var empty = new Label
			{
				Text = Locale.Tr("NO_SCORES"),
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.8f));
			_scoreList.AddChild(empty);
			return;
		}

		// Header
		var header = BuildScoreRow(Locale.Tr("RANK"), Locale.Tr("PLAYER"), "Score", "Grade", "Acc", "Combo");
		header.AddThemeColorOverride("font_color", new Color(0.2f, 0.9f, 1f));
		_scoreList.AddChild(header);

		// Entries
		for (int i = 0; i < scores.Count; i++)
		{
			var e = scores[i];
			var row = BuildScoreRow(
				$"{i + 1}",
				e.PlayerName,
				$"{e.Score:N0}",
				e.Grade,
				$"{e.Accuracy:F1}%",
				$"{e.MaxCombo}x"
			);

			// Destaca top 3
			Color rowColor = i switch
			{
				0 => new Color(1f, 0.85f, 0f),    // ouro
				1 => new Color(0.75f, 0.75f, 0.8f), // prata
				2 => new Color(0.8f, 0.5f, 0.2f),  // bronze
				_ => Colors.White
			};
			row.AddThemeColorOverride("font_color", rowColor);
			_scoreList.AddChild(row);
		}
	}

	private static Label BuildScoreRow(string rank, string name, string score, string grade, string acc, string combo)
	{
		// Trunca nome se muito longo
		if (name.Length > 12) name = name[..12] + "…";

		return new Label
		{
			Text                = $"{rank,-4} {name,-14} {score,10}   {grade,-2}   {acc,7}   {combo,5}",
			HorizontalAlignment = HorizontalAlignment.Left,
		};
	}

	// ── Scanner de nomes (mesma lógica do SongSelectMenu) ──────────────

	private static List<string> FindAllSongNames(string baseDir)
	{
		var result = new List<string>();
		var access = DirAccess.Open(baseDir);
		if (access == null) return result;

		access.ListDirBegin();
		string entry = access.GetNext();
		while (entry != "")
		{
			if (access.CurrentIsDir())
			{
				string dir = baseDir.TrimEnd('/') + "/" + entry + "/";

				// Só lista se a pasta tiver áudio reconhecido (mesma lógica do SongSelectMenu)
				if (!FolderHasAudio(dir))
				{
					entry = access.GetNext();
					continue;
				}

				// Resolve o nome exatamente como SongSelectMenu + LoadingScreen:
				// 1) song.ini → "Artista - Nome"
				// 2) notes.chart → SongName (sobrescreve se existir)
				string displayName = entry;
				string iniPath = dir + "song.ini";
				if (FileAccess.FileExists(iniPath))
				{
					var info = SongIniReader.Read(iniPath);
					string iniName = SongIniReader.BuildDisplayName(info, entry);
					if (!string.IsNullOrEmpty(iniName)) displayName = iniName;
				}

				string chartPath = dir + "notes.chart";
				if (FileAccess.FileExists(chartPath))
				{
					var imported = ChartImporter.Import(chartPath);
					if (imported != null && !string.IsNullOrEmpty(imported.SongName))
						displayName = imported.SongName;
				}

				result.Add(displayName);
			}
			else
			{
				foreach (var ext in LooseAudioExtensions)
				{
					if (entry.ToLower().EndsWith(ext))
					{
						int dot = entry.LastIndexOf('.');
						string name = dot >= 0 ? entry[..dot] : entry;
						result.Add(name);
						break;
					}
				}
			}
			entry = access.GetNext();
		}
		access.ListDirEnd();
		return result;
	}

	/// <summary>Verifica se a pasta tem pelo menos um arquivo de áudio reconhecido.</summary>
	private static bool FolderHasAudio(string dir)
	{
		foreach (var candidate in FolderAudioCandidates)
		{
			if (FileAccess.FileExists(dir + candidate)) return true;
		}

		var access = DirAccess.Open(dir);
		if (access == null) return false;

		access.ListDirBegin();
		string entry = access.GetNext();
		while (entry != "")
		{
			if (!access.CurrentIsDir())
			{
				string lower = entry.ToLower();
				foreach (var ext in LooseAudioExtensions)
				{
					if (lower.EndsWith(ext)) { access.ListDirEnd(); return true; }
				}
			}
			entry = access.GetNext();
		}
		access.ListDirEnd();
		return false;
	}
}
