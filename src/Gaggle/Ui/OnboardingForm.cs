using System.Windows.Forms;

namespace Gaggle.Ui;

/// <summary>
/// The getting-started guide: what to set up, in what order, and what happens in flight.
///
/// Shown once, the first time Gaggle runs, and reopenable from the tray afterwards. It
/// only explains - nothing here changes a setting except the language it is written in,
/// because the tray menu and the settings window already do that and two places to
/// change one thing is how they end up disagreeing.
/// </summary>
internal sealed class OnboardingForm : Form
{
    private readonly ComboBox _languages;
    private readonly Label _heading;
    private readonly Label _intro;
    private readonly Label[] _titles;
    private readonly Label[] _bodies;
    private readonly Label _footer;
    private readonly Button _close;

    private OnboardingText _text;

    public OnboardingForm(string languageCode)
    {
        _text = OnboardingText.For(languageCode);
        LanguageCode = Normalise(languageCode);

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 618);
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 9f);

        _heading = new Label
        {
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Location = new Point(18, 18),
            AutoSize = true,
        };

        var languageLabel = new Label
        {
            Location = new Point(340, 24),
            Width = 70,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = SystemColors.GrayText,
        };

        _languages = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(414, 21),
            Width = 128,
        };

        foreach (string code in OnboardingText.AvailableCodes)
        {
            _languages.Items.Add(OnboardingText.For(code).LanguageName);
        }

        _languages.SelectedIndex = Math.Max(0, IndexOf(LanguageCode));
        _languages.SelectedIndexChanged += (_, _) =>
        {
            LanguageCode = OnboardingText.AvailableCodes[_languages.SelectedIndex];
            _text = OnboardingText.For(LanguageCode);
            Retranslate(languageLabel);
        };

        _intro = new Label
        {
            Location = new Point(18, 60),
            Width = 524,
            Height = 40,
            ForeColor = SystemColors.GrayText,
        };

        // Four steps and the etiquette note, laid out on one pitch so a longer
        // translation cannot push the next heading into the paragraph above it.
        _titles = new Label[5];
        _bodies = new Label[5];

        for (int i = 0; i < _titles.Length; i++)
        {
            int top = 112 + (i * 88);

            _titles[i] = new Label
            {
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(18, top),
                AutoSize = true,
            };

            _bodies[i] = new Label
            {
                Location = new Point(18, top + 20),
                Width = 524,
                Height = 60,
            };
        }

        // The etiquette note is the one paragraph that is a warning rather than a step,
        // in the same amber the settings window uses for the joystick caveat.
        _bodies[4].ForeColor = Color.FromArgb(160, 90, 0);

        // Kept clear of the button rather than running the full width: the footer wraps
        // to two lines in Polish and German, and at full width the second line would
        // pass behind Close.
        _footer = new Label
        {
            Location = new Point(18, 548),
            Width = 430,
            Height = 44,
            ForeColor = SystemColors.GrayText,
        };

        _close = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(458, 578),
            Width = 84,
        };

        Controls.Add(_heading);
        Controls.Add(languageLabel);
        Controls.Add(_languages);
        Controls.Add(_intro);
        Controls.AddRange(_titles);
        Controls.AddRange(_bodies);
        Controls.Add(_footer);
        Controls.Add(_close);

        AcceptButton = _close;
        CancelButton = _close;

        Retranslate(languageLabel);
    }

    /// <summary>The language the guide was last shown in, valid once the dialog closes.</summary>
    public string LanguageCode { get; private set; }

    private static string Normalise(string? code) =>
        OnboardingText.AvailableCodes.Contains(code?.ToLowerInvariant() ?? "en")
            ? code!.ToLowerInvariant()
            : "en";

    private static int IndexOf(string code)
    {
        for (int i = 0; i < OnboardingText.AvailableCodes.Count; i++)
        {
            if (OnboardingText.AvailableCodes[i] == code)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// Repaints every string from <see cref="_text"/>. One method rather than setting
    /// text at construction, so switching language costs the same as opening the window.
    /// </summary>
    private void Retranslate(Label languageLabel)
    {
        Text = _text.WindowTitle;
        _heading.Text = _text.Heading;
        _intro.Text = _text.Intro;
        languageLabel.Text = _text.LanguageLabel;
        _footer.Text = _text.Footer;
        _close.Text = _text.CloseButton;

        _titles[0].Text = _text.Step1Title;
        _bodies[0].Text = _text.Step1Body;
        _titles[1].Text = _text.Step2Title;
        _bodies[1].Text = _text.Step2Body;
        _titles[2].Text = _text.Step3Title;
        _bodies[2].Text = _text.Step3Body;
        _titles[3].Text = _text.Step4Title;
        _bodies[3].Text = _text.Step4Body;
        _titles[4].Text = _text.EtiquetteTitle;
        _bodies[4].Text = _text.EtiquetteBody;
    }
}
