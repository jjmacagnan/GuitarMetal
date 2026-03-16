using Godot;

/// <summary>
/// Tela de créditos e licença do projeto.
/// Exibe informações sobre o projeto, dedicatória e o texto da licença MIT.
/// </summary>
public partial class Credits : Control
{
    private const string MitLicenseText =
        "MIT License\n\n" +
        "Copyright (c) 2026 jjmacagnan\n\n" +
        "Permission is hereby granted, free of charge, to any person obtaining a copy " +
        "of this software and associated documentation files (the \"Software\"), to deal " +
        "in the Software without restriction, including without limitation the rights " +
        "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
        "copies of the Software, and to permit persons to whom the Software is " +
        "furnished to do so, subject to the following conditions:\n\n" +
        "The above copyright notice and this permission notice shall be included in all " +
        "copies or substantial portions of the Software.\n\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
        "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
        "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
        "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
        "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
        "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE " +
        "SOFTWARE.";

    private Label  _titleLabel;
    private Label  _aboutTitleLabel;
    private Label  _aboutTextLabel;
    private Label  _dedicationTitleLabel;
    private Label  _dedicationTextLabel;
    private Label  _developerTitleLabel;
    private Label  _developerTextLabel;
    private Label  _githubProfileLabel;
    private Label  _githubProjectLabel;
    private Label  _linkedInLabel;
    private Label  _techTitleLabel;
    private Label  _techTextLabel;
    private Label  _licenseTitleLabel;
    private Label  _licenseTextLabel;
    private Button _backButton;

    public override void _Ready()
    {
        _titleLabel           = GetNodeOrNull<Label>("VBox/TitleLabel");
        _aboutTitleLabel      = GetNodeOrNull<Label>("VBox/Scroll/Content/AboutTitleLabel");
        _aboutTextLabel       = GetNodeOrNull<Label>("VBox/Scroll/Content/AboutTextLabel");
        _dedicationTitleLabel = GetNodeOrNull<Label>("VBox/Scroll/Content/DedicationTitleLabel");
        _dedicationTextLabel  = GetNodeOrNull<Label>("VBox/Scroll/Content/DedicationTextLabel");
        _developerTitleLabel  = GetNodeOrNull<Label>("VBox/Scroll/Content/DeveloperTitleLabel");
        _developerTextLabel   = GetNodeOrNull<Label>("VBox/Scroll/Content/DeveloperTextLabel");
        _githubProfileLabel   = GetNodeOrNull<Label>("VBox/Scroll/Content/GitHubProfileRow/GitHubProfileLabel");
        _githubProjectLabel   = GetNodeOrNull<Label>("VBox/Scroll/Content/GitHubProjectRow/GitHubProjectLabel");
        _linkedInLabel        = GetNodeOrNull<Label>("VBox/Scroll/Content/LinkedInRow/LinkedInLabel");
        _techTitleLabel       = GetNodeOrNull<Label>("VBox/Scroll/Content/TechTitleLabel");
        _techTextLabel        = GetNodeOrNull<Label>("VBox/Scroll/Content/TechTextLabel");
        _licenseTitleLabel    = GetNodeOrNull<Label>("VBox/Scroll/Content/LicenseTitleLabel");
        _licenseTextLabel     = GetNodeOrNull<Label>("VBox/Scroll/Content/LicenseTextLabel");
        _backButton           = GetNodeOrNull<Button>("VBox/BackButton");

        if (_backButton != null) _backButton.Pressed += OnBackPressed;
        _backButton?.CallDeferred(Control.MethodName.GrabFocus);

        ApplyLocale();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            OnBackPressed();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBackPressed() =>
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");

    private void ApplyLocale()
    {
        if (_titleLabel           != null) _titleLabel.Text           = Locale.Tr("CREDITS_TITLE");
        if (_aboutTitleLabel      != null) _aboutTitleLabel.Text      = Locale.Tr("CREDITS_ABOUT_TITLE");
        if (_aboutTextLabel       != null) _aboutTextLabel.Text       = Locale.Tr("CREDITS_ABOUT_TEXT");
        if (_dedicationTitleLabel != null) _dedicationTitleLabel.Text = Locale.Tr("CREDITS_DEDICATION_TITLE");
        if (_dedicationTextLabel  != null) _dedicationTextLabel.Text  = Locale.Tr("CREDITS_DEDICATION_TEXT");
        if (_developerTitleLabel  != null) _developerTitleLabel.Text  = Locale.Tr("CREDITS_DEVELOPER");
        if (_developerTextLabel   != null) _developerTextLabel.Text   = "jjmacagnan";
        if (_githubProfileLabel   != null) _githubProfileLabel.Text   = Locale.Tr("CREDITS_GITHUB_PROFILE");
        if (_githubProjectLabel   != null) _githubProjectLabel.Text   = Locale.Tr("CREDITS_GITHUB_PROJECT");
        if (_linkedInLabel        != null) _linkedInLabel.Text        = Locale.Tr("CREDITS_LINKEDIN");
        if (_techTitleLabel       != null) _techTitleLabel.Text       = Locale.Tr("CREDITS_TECH");
        if (_techTextLabel        != null) _techTextLabel.Text        = Locale.Tr("CREDITS_TECH_TEXT");
        if (_licenseTitleLabel    != null) _licenseTitleLabel.Text    = Locale.Tr("CREDITS_LICENSE_TITLE");
        if (_licenseTextLabel     != null) _licenseTextLabel.Text     = MitLicenseText;
        if (_backButton           != null) _backButton.Text           = Locale.Tr("BACK");
    }
}
