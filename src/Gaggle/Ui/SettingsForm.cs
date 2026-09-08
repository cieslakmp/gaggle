using System.Windows.Forms;
using Gaggle.Input;
using Gaggle.Localisation;

namespace Gaggle.Ui;

/// <summary>
/// Lets the user rebind push-to-talk by pressing the key or button they want.
///
/// Capture goes through <see cref="PttController"/> rather than this window's own
/// key events, for two reasons: the global hook sees keys the form would never
/// receive (Caps Lock, media keys), and joystick buttons produce no window messages
/// at all.
///
/// The layout is a single auto-sizing column rather than hand-placed coordinates. Half
/// the controls here hold two or three sentences of explanation, and a translation runs
/// longer than the English it replaced — Polish by a fifth or so. Fixed heights turn that
/// into a clipped sentence, which is the half that mattered.
/// </summary>
internal sealed class SettingsForm : Form
{
    /// <summary>How wide prose is allowed to run before it wraps.</summary>
    private const int ContentWidth = 400;

    private readonly PttController _controller;
    private readonly TextBox _bindingBox;
    private readonly Button _changeButton;
    private readonly Label _hint;
    private readonly Label _warning;
    private readonly ListBox _devices;
    private readonly CheckBox _handsFreeBox;
    private readonly Label _handsFreeNote;
    private readonly CheckBox _cuesBox;
    private readonly Button _okButton;

    private PttBinding _binding;

    public SettingsForm(PttController controller, PttBinding current, bool handsFree, bool audibleCues)
    {
        _controller = controller;
        _binding = current;

        Text = Strings.Current.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 9f);

        Label title = Heading(Strings.Current.PushToTalkHeading, 10f);

        _bindingBox = new TextBox
        {
            ReadOnly = true,
            Width = 250,
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(0, 0, 10, 0),
        };

        _changeButton = new Button
        {
            Text = Strings.Current.ChangeBinding,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(120, 0),
            Margin = new Padding(0),
        };
        _changeButton.Click += (_, _) => ToggleCapture();

        var bindingRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 8, 0, 0),
            WrapContents = false,
        };
        bindingRow.Controls.Add(_bindingBox);
        bindingRow.Controls.Add(_changeButton);

        _hint = Prose(SystemColors.GrayText);

        Label devicesTitle = Heading(Strings.Current.DetectedDevices, 10f);

        _devices = new ListBox
        {
            Size = new Size(ContentWidth, 74),
            IntegralHeight = false,
            Margin = new Padding(0, 8, 0, 0),
        };

        // Amber, and only filled in when a joystick binding makes it true. With AutoSize
        // an empty warning takes no room at all rather than leaving a gap.
        _warning = Prose(Color.FromArgb(160, 90, 0));

        Label sendingTitle = Heading(Strings.Current.SendingHeading, 10f);

        _handsFreeBox = new CheckBox
        {
            Text = Strings.Current.HandsFreeCheckbox,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Checked = handsFree,
            Margin = new Padding(0, 8, 0, 0),
        };
        _handsFreeBox.CheckedChanged += (_, _) => RefreshHandsFree();

        _handsFreeNote = Prose(SystemColors.GrayText);

        _cuesBox = new CheckBox
        {
            Text = Strings.Current.AudibleCuesCheckbox,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Checked = audibleCues,
            Margin = new Padding(0, 12, 0, 0),
        };

        _okButton = new Button
        {
            Text = Strings.Current.Ok,
            DialogResult = DialogResult.OK,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 0),
            Margin = new Padding(8, 0, 0, 0),
        };

        var cancelButton = new Button
        {
            Text = Strings.Current.Cancel,
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 0),
            Margin = new Padding(8, 0, 0, 0),
        };

        // Right to left, so the rightmost button is the one added first.
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 16, 0, 0),
            WrapContents = false,
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_okButton);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        layout.Controls.AddRange([
            title, bindingRow, _hint, devicesTitle, _devices, _warning,
            sendingTitle, _handsFreeBox, _handsFreeNote, _cuesBox, buttons,
        ]);

        Controls.Add(layout);
        AcceptButton = _okButton;
        CancelButton = cancelButton;

        _controller.Captured += OnCaptured;

        RefreshDevices();
        RefreshBinding();
        RefreshHandsFree();
    }

    /// <summary>The binding chosen, valid once the dialog returns OK.</summary>
    public PttBinding Binding => _binding;

    /// <summary>Whether to skip the review step, valid once the dialog returns OK.</summary>
    public bool HandsFree => _handsFreeBox.Checked;

    /// <summary>Whether to play the cue tones, valid once the dialog returns OK.</summary>
    public bool AudibleCues => _cuesBox.Checked;

    /// <summary>A section title.</summary>
    private static Label Heading(string text, float size) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 16, 0, 0),
    };

    /// <summary>A paragraph that wraps at <see cref="ContentWidth"/> and grows downwards.</summary>
    private static Label Prose(Color colour) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(ContentWidth, 0),
        ForeColor = colour,
        Margin = new Padding(0, 6, 0, 0),
    };

    /// <summary>
    /// Says what the checkbox above it actually means, in the colour that matches how
    /// much it matters - the same amber the joystick caveat uses when it applies.
    /// </summary>
    private void RefreshHandsFree()
    {
        _handsFreeNote.ForeColor = _handsFreeBox.Checked
            ? Color.FromArgb(160, 90, 0)
            : SystemColors.GrayText;

        _handsFreeNote.Text = _handsFreeBox.Checked
            ? Strings.Current.HandsFreeOnNote
            : Strings.Current.HandsFreeOffNote;
    }

    private void ToggleCapture()
    {
        if (_controller.IsCapturing)
        {
            _controller.CancelCapture();
            RefreshBinding();
            return;
        }

        _controller.BeginCapture();

        _changeButton.Text = Strings.Current.StopCapture;
        _bindingBox.Text = Strings.Current.PressAKeyOrButton;
        _hint.Text = Strings.Current.CaptureListening;
        _okButton.Enabled = false;
    }

    private void OnCaptured(PttBinding binding)
    {
        // Raised from the hook or the poll timer; both are on the UI thread, but the
        // form may already be closing.
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        _binding = binding;
        RefreshBinding();
    }

    private void RefreshBinding()
    {
        _changeButton.Text = Strings.Current.ChangeBinding;
        _bindingBox.Text = _binding.Describe();
        _okButton.Enabled = true;

        _hint.Text = _binding.IsKeyboard
            ? Strings.Current.KeyHiddenFromCondor
            : Strings.Current.ButtonsCannotBeHidden;

        _warning.Text = _binding.IsKeyboard
            ? string.Empty
            : Strings.Current.JoystickWarning;
    }

    private void RefreshDevices()
    {
        _devices.Items.Clear();

        IReadOnlyList<(int Id, string Name, int Buttons)> found = JoystickWatcher.ListDevices();

        if (found.Count == 0)
        {
            _devices.Items.Add(Strings.Current.NoJoysticksDetected);
            return;
        }

        foreach ((int id, string name, int buttons) in found)
        {
            _devices.Items.Add(Strings.Current.JoystickDevice(id + 1, name, buttons));
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller.Captured -= OnCaptured;
        _controller.CancelCapture();

        base.OnFormClosed(e);
    }
}
