using System;
using Godot;

/// <summary>
/// Controla o sistema de pause: overlay, botões, toggle e input.
/// Emite sinais para que o GameManager execute restart/quit.
/// </summary>
public partial class PauseController : Control
{
	[Signal] public delegate void ResumeRequestedEventHandler();
	[Signal] public delegate void RestartRequestedEventHandler();
	[Signal] public delegate void QuitRequestedEventHandler();

	public bool IsPaused { get; private set; }

	private AudioStreamPlayer _audio;
	private Control           _pauseOverlay;
	private Button            _pauseResumeButton;
	private bool              _songEnded;

	/// <summary>
	/// Inicializa o pause controller dentro do HUD.
	/// </summary>
	public void Initialize(CanvasLayer hud, AudioStreamPlayer audio)
	{
		_audio = audio;
		ProcessMode = ProcessModeEnum.Always;

		BuildPauseOverlay(hud);
		BuildTouchPauseButton(hud);

		if (hud != null)
			hud.AddChild(this);
	}

	/// <summary>Informa que a música acabou (desabilita toggle).</summary>
	public void SetSongEnded(bool ended) => _songEnded = ended;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_songEnded) return;
			TogglePause();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TogglePause()
	{
		IsPaused = !IsPaused;
		GetTree().Paused = IsPaused;

		if (IsPaused)
		{
			if (_audio != null) _audio.StreamPaused = true;
			_pauseOverlay?.Show();
			_pauseResumeButton?.CallDeferred(Control.MethodName.GrabFocus);
		}
		else
		{
			if (_audio != null) _audio.StreamPaused = false;
			_pauseOverlay?.Hide();
		}
	}

	private void OnResume()
	{
		TogglePause();
		EmitSignal(SignalName.ResumeRequested);
	}

	private void OnRestart()
	{
		TogglePause();
		EmitSignal(SignalName.RestartRequested);
	}

	private void OnQuit()
	{
		TogglePause();
		EmitSignal(SignalName.QuitRequested);
	}

	// ── Build UI ───────────────────────────────────────────────────────────

	private void BuildPauseOverlay(CanvasLayer hud)
	{
		if (hud == null) return;

		_pauseOverlay = new Control();
		_pauseOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_pauseOverlay.ProcessMode = ProcessModeEnum.Always;

		var bg = new ColorRect();
		bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		bg.Color = new Color(0f, 0f, 0f, 0.75f);
		_pauseOverlay.AddChild(bg);

		var vbox = new VBoxContainer();
		vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		vbox.OffsetLeft   = -160;
		vbox.OffsetRight  =  160;
		vbox.OffsetTop    = -130;
		vbox.OffsetBottom =  130;
		vbox.AddThemeConstantOverride("separation", 16);
		vbox.Alignment = BoxContainer.AlignmentMode.Center;

		var title = new Label { Text = Locale.Tr("PAUSED"), HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 42);
		title.AddThemeColorOverride("font_color", new Color(0.2f, 0.9f, 1f));
		vbox.AddChild(title);

		var btnResume  = MakePauseButton(Locale.Tr("RESUME"),       OnResume);
		var btnRestart = MakePauseButton(Locale.Tr("RESTART"),      OnRestart);
		var btnQuit    = MakePauseButton(Locale.Tr("QUIT_TO_MENU"), OnQuit);

		vbox.AddChild(btnResume);
		vbox.AddChild(btnRestart);
		vbox.AddChild(btnQuit);

		_pauseResumeButton = btnResume;

		_pauseOverlay.AddChild(vbox);
		_pauseOverlay.Hide();
		hud.AddChild(_pauseOverlay);
	}

	private void BuildTouchPauseButton(CanvasLayer hud)
	{
		if (hud == null) return;

		var btn = new Button
		{
			Text              = "⏸",
			CustomMinimumSize = new Vector2(80, 80),
			FocusMode         = FocusModeEnum.None,
			ProcessMode       = ProcessModeEnum.Always,
		};
		btn.AddThemeFontSizeOverride("font_size", 38);

		btn.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
		btn.OffsetLeft   = -90f;
		btn.OffsetRight  = -10f;
		btn.OffsetTop    =  10f;
		btn.OffsetBottom =  90f;

		btn.Pressed += () =>
		{
			if (!_songEnded) TogglePause();
		};

		hud.AddChild(btn);
	}

	private static Button MakePauseButton(string text, Action callback)
	{
		var btn = new Button
		{
			Text              = text,
			CustomMinimumSize = new Vector2(280, 52),
			ProcessMode       = ProcessModeEnum.Always,
			FocusMode         = FocusModeEnum.All,
		};
		btn.AddThemeFontSizeOverride("font_size", 22);
		btn.Pressed += callback;
		return btn;
	}
}
