using Godot;

/// <summary>
/// Tela de configurações: remapeamento de teclado e gamepad para as 5 lanes.
///
/// Modo de escuta: clicar em um botão de binding coloca aquele slot em modo
/// de espera. O próximo pressionamento de tecla/botão (capturado em _Input)
/// é atribuído imediatamente ao InputMap. O binding é persistido ao sair
/// via Voltar ou ui_cancel.
/// </summary>
public partial class SettingsMenu : Control
{
	// Cores e nomes das lanes agora vêm de LaneConfig

	// ── Nós da cena ───────────────────────────────────────────────────────
	private Label         _titleLabel;
	private TabContainer  _tabContainer;
	private VBoxContainer _keyboardVBox;
	private VBoxContainer _gamepadVBox;
	private VBoxContainer _audioVBox;
	private Button        _resetButton;
	private Label         _savedLabel;
	private Button        _backButton;

	// ── Audio ─────────────────────────────────────────────────────────────
	private HSlider _masterSlider;
	private HSlider _musicSlider;
	private HSlider _sfxSlider;
	private Label   _masterValueLabel;
	private Label   _musicValueLabel;
	private Label   _sfxValueLabel;
	private AudioStreamPlayer _previewSfx;

	// ── Botões de binding por lane ────────────────────────────────────────
	private readonly Button[] _keyboardButtons = new Button[LaneConfig.LaneCount];
	private readonly Button[] _gamepadButtons  = new Button[LaneConfig.LaneCount];

	// ── Estado do modo de escuta ──────────────────────────────────────────
	private int  _listeningLane     = -1;  // -1 = não escutando
	private bool _listeningKeyboard;       // true = teclado, false = gamepad

	// ── Tween do label "Salvo!" ───────────────────────────────────────────
	private Tween _savedTween;

	// ─────────────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		_titleLabel   = GetNodeOrNull<Label>("VBox/TitleLabel");
		_tabContainer = GetNodeOrNull<TabContainer>("VBox/TabContainer");
		_keyboardVBox = GetNodeOrNull<VBoxContainer>("VBox/TabContainer/KeyboardTab/KeyboardVBox");
		_gamepadVBox  = GetNodeOrNull<VBoxContainer>("VBox/TabContainer/GamepadTab/GamepadVBox");
		_audioVBox    = GetNodeOrNull<VBoxContainer>("VBox/TabContainer/AudioTab/AudioVBox");
		_resetButton  = GetNodeOrNull<Button>("VBox/BottomRow/ResetButton");
		_savedLabel   = GetNodeOrNull<Label>("VBox/BottomRow/SavedLabel");
		_backButton   = GetNodeOrNull<Button>("VBox/BackButton");

		BuildLaneRows(_keyboardVBox, _keyboardButtons, isKeyboard: true);
		BuildLaneRows(_gamepadVBox,  _gamepadButtons,  isKeyboard: false);
		BuildAudioSliders();

		if (_resetButton != null) _resetButton.Pressed += OnResetPressed;
		if (_backButton  != null) _backButton.Pressed  += OnBackPressed;

		RefreshAllLabels();
		ApplyLocale();

		_backButton?.CallDeferred(Control.MethodName.GrabFocus);
	}

	// ── Captura input no modo de escuta ───────────────────────────────────
	public override void _Input(InputEvent @event)
	{
		if (_listeningLane < 0) return;
		if (@event.IsEcho()) return;
		if (@event is InputEventMouseButton or InputEventMouseMotion) return;

		if (_listeningKeyboard)
		{
			if (@event is InputEventKey evKey && evKey.Pressed)
			{
				Key kc = evKey.Keycode;

				// Rejeita modificadores sozinhos
				if (kc is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta) return;

				// Escape cancela
				if (kc == Key.Escape)
				{
					CancelListening();
					GetViewport().SetInputAsHandled();
					return;
				}

				KeybindingStorage.SetKey(_listeningLane, kc);
				CommitListening();
				GetViewport().SetInputAsHandled();
			}
		}
		else
		{
			if (@event is InputEventJoypadButton evBtn && evBtn.Pressed)
			{
				KeybindingStorage.SetButton(_listeningLane, evBtn.ButtonIndex);
				CommitListening();
				GetViewport().SetInputAsHandled();
			}
			else if (@event is InputEventJoypadMotion evAxis && Mathf.Abs(evAxis.AxisValue) > 0.5f)
			{
				KeybindingStorage.SetAxis(_listeningLane, evAxis.Axis, evAxis.AxisValue);
				CommitListening();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	// ── ui_cancel: cancela escuta ou volta ao menu ─────────────────────────
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_listeningLane >= 0)
				CancelListening();
			else
				SaveAndGoBack();

			GetViewport().SetInputAsHandled();
		}
	}

	// ── Construção das linhas de lane ─────────────────────────────────────
	private void BuildLaneRows(VBoxContainer parent, Button[] buttons, bool isKeyboard)
	{
		if (parent == null) return;

		for (int i = 0; i < LaneConfig.LaneCount; i++)
		{
			int lane = i; // captura para closure

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 20);
			row.SizeFlagsHorizontal = SizeFlags.ExpandFill;

			// Barra colorida da lane
			var bar = new ColorRect
			{
				CustomMinimumSize = new Vector2(12, 0),
				Color             = LaneConfig.LaneColors[i],
				SizeFlagsVertical = SizeFlags.ExpandFill,
			};
			row.AddChild(bar);

			// Nome da lane
			var nameLabel = new Label
			{
				Text              = Locale.Tr(LaneConfig.LaneNameKeys[i]),
				CustomMinimumSize = new Vector2(130, 0),
				VerticalAlignment = VerticalAlignment.Center,
			};
			nameLabel.AddThemeFontSizeOverride("font_size", 20);
			nameLabel.AddThemeColorOverride("font_color", LaneConfig.LaneColors[i]);
			row.AddChild(nameLabel);

			// Botão de binding
			var btn = new Button
			{
				CustomMinimumSize   = new Vector2(220, 48),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				FocusMode           = FocusModeEnum.All,
			};
			btn.AddThemeFontSizeOverride("font_size", 20);
			btn.Pressed += () => OnBindButtonPressed(lane, isKeyboard);
			row.AddChild(btn);
			buttons[i] = btn;

			parent.AddChild(row);
		}
	}

	// ── Audio sliders ─────────────────────────────────────────────────────
	private void BuildAudioSliders()
	{
		if (_audioVBox == null) return;

		// Player para preview do volume ao ajustar sliders
		_previewSfx = new AudioStreamPlayer { Bus = "SFX", VolumeDb = -6f };
		var sfx = GD.Load<AudioStream>("res://SFX/Caixa 1.mp3");
		if (sfx != null) _previewSfx.Stream = sfx;
		AddChild(_previewSfx);

		(_masterSlider, _masterValueLabel) = AddVolumeRow(_audioVBox, Locale.Tr("VOLUME_MASTER"), AudioSettings.MasterVolumeDb,
			(val) => { AudioSettings.SetMasterVolume((float)val); UpdateVolumeLabel(_masterValueLabel, (float)val); PlayPreviewSfx(); });

		(_musicSlider, _musicValueLabel) = AddVolumeRow(_audioVBox, Locale.Tr("VOLUME_MUSIC"), AudioSettings.MusicVolumeDb,
			(val) => { AudioSettings.SetMusicVolume((float)val); UpdateVolumeLabel(_musicValueLabel, (float)val); PlayPreviewSfx(); });

		(_sfxSlider, _sfxValueLabel) = AddVolumeRow(_audioVBox, Locale.Tr("VOLUME_SFX"), AudioSettings.SfxVolumeDb,
			(val) => { AudioSettings.SetSfxVolume((float)val); UpdateVolumeLabel(_sfxValueLabel, (float)val); PlayPreviewSfx(); });
	}

	private static (HSlider slider, Label valueLabel) AddVolumeRow(VBoxContainer parent, string label, float currentDb, HSlider.ValueChangedEventHandler onChanged)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);

		var nameLabel = new Label
		{
			Text              = label,
			CustomMinimumSize = new Vector2(160, 0),
			VerticalAlignment = VerticalAlignment.Center,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
		row.AddChild(nameLabel);

		var slider = new HSlider
		{
			MinValue            = -30,
			MaxValue            = 0,
			Step                = 1,
			Value               = currentDb,
			CustomMinimumSize   = new Vector2(250, 0),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			FocusMode           = Control.FocusModeEnum.All,
		};
		slider.ValueChanged += onChanged;
		row.AddChild(slider);

		var valueLabel = new Label
		{
			CustomMinimumSize = new Vector2(60, 0),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right,
		};
		valueLabel.AddThemeFontSizeOverride("font_size", 18);
		UpdateVolumeLabel(valueLabel, currentDb);
		row.AddChild(valueLabel);

		parent.AddChild(row);
		return (slider, valueLabel);
	}

	private static void UpdateVolumeLabel(Label label, float db)
	{
		if (label == null) return;
		if (db <= -30f)
			label.Text = "MUTE";
		else
			label.Text = $"{(int)((db + 30f) / 30f * 100f)}%";
	}

	private void PlayPreviewSfx()
	{
		if (_previewSfx?.Stream != null)
			_previewSfx.Play();
	}

	// ── Entrada no modo de escuta ─────────────────────────────────────────
	private void OnBindButtonPressed(int lane, bool isKeyboard)
	{
		// Clicar de novo no mesmo botão cancela
		if (_listeningLane == lane && _listeningKeyboard == isKeyboard)
		{
			CancelListening();
			return;
		}

		if (_listeningLane >= 0) CancelListening();

		_listeningLane     = lane;
		_listeningKeyboard = isKeyboard;

		Button btn = isKeyboard ? _keyboardButtons[lane] : _gamepadButtons[lane];
		btn.Text = Locale.Tr(isKeyboard ? "PRESS_KEY" : "PRESS_INPUT");
		btn.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0f)); // amarelo
	}

	private void CancelListening()
	{
		if (_listeningLane < 0) return;
		RefreshLabel(_listeningLane, _listeningKeyboard);
		_listeningLane = -1;
	}

	private void CommitListening()
	{
		// Aplica imediatamente ao InputMap para que o jogo funcione logo
		KeybindingStorage.ApplyToInputMap();
		RefreshLabel(_listeningLane, _listeningKeyboard);
		_listeningLane = -1;
	}

	// ── Atualização dos rótulos ───────────────────────────────────────────
	private void RefreshAllLabels()
	{
		for (int i = 0; i < LaneConfig.LaneCount; i++)
		{
			RefreshLabel(i, isKeyboard: true);
			RefreshLabel(i, isKeyboard: false);
		}
	}

	private void RefreshLabel(int lane, bool isKeyboard)
	{
		Button btn = isKeyboard ? _keyboardButtons[lane] : _gamepadButtons[lane];
		if (btn == null) return;
		btn.RemoveThemeColorOverride("font_color");

		if (isKeyboard)
		{
			// Trata Space separadamente para exibição localizada
			btn.Text = KeybindingStorage.GetKey(lane) == Key.Space
				? Locale.Tr("KB_SPACE")
				: OS.GetKeycodeString(KeybindingStorage.GetKey(lane));
		}
		else
		{
			btn.Text = KeybindingStorage.GetIsAxis(lane)
				? KeybindingStorage.AxisDisplayName(KeybindingStorage.GetAxis(lane))
				: KeybindingStorage.ButtonDisplayName(KeybindingStorage.GetButton(lane));
		}
	}

	// ── Restaurar padrões ─────────────────────────────────────────────────
	private void OnResetPressed()
	{
		if (_listeningLane >= 0) CancelListening();

		KeybindingStorage.ResetToDefaults();
		KeybindingStorage.Save();
		KeybindingStorage.ApplyToInputMap();
		RefreshAllLabels();

		// Reset audio para 100% (0 dB)
		AudioSettings.SetMasterVolume(0f);
		AudioSettings.SetMusicVolume(0f);
		AudioSettings.SetSfxVolume(0f);
		AudioSettings.Save();
		if (_masterSlider != null) _masterSlider.Value = 0;
		if (_musicSlider  != null) _musicSlider.Value  = 0;
		if (_sfxSlider    != null) _sfxSlider.Value    = 0;
		UpdateVolumeLabel(_masterValueLabel, 0f);
		UpdateVolumeLabel(_musicValueLabel,  0f);
		UpdateVolumeLabel(_sfxValueLabel,    0f);

		ShowSavedFeedback();
	}

	// ── Voltar / Salvar ───────────────────────────────────────────────────
	private void OnBackPressed() => SaveAndGoBack();

	private void SaveAndGoBack()
	{
		if (_listeningLane >= 0) CancelListening();
		KeybindingStorage.Save();
		AudioSettings.Save();
		GetTree().ChangeSceneToFile(ScenePaths.MainMenu);
	}

	// ── Flash "Salvo!" ────────────────────────────────────────────────────
	private void ShowSavedFeedback()
	{
		if (_savedLabel == null) return;
		_savedTween?.Kill();
		_savedLabel.Text     = Locale.Tr("SETTINGS_SAVED");
		_savedLabel.Visible  = true;
		_savedLabel.Modulate = Colors.White;

		_savedTween = CreateTween();
		_savedTween.TweenProperty(_savedLabel, "modulate:a", 0f, 0.6f).SetDelay(1.0f);
		_savedTween.TweenCallback(Callable.From(() => _savedLabel.Visible = false));
	}

	// ── Localização ───────────────────────────────────────────────────────
	private void ApplyLocale()
	{
		if (_titleLabel  != null) _titleLabel.Text  = Locale.Tr("SETTINGS_TITLE");
		if (_resetButton != null) _resetButton.Text = Locale.Tr("RESET_DEFAULTS");
		if (_backButton  != null) _backButton.Text  = Locale.Tr("BACK");

		if (_tabContainer != null)
		{
			_tabContainer.SetTabTitle(0, Locale.Tr("KEYBOARD_TAB"));
			_tabContainer.SetTabTitle(1, Locale.Tr("GAMEPAD_TAB"));
			_tabContainer.SetTabTitle(2, Locale.Tr("AUDIO_TAB"));
		}
	}
}
