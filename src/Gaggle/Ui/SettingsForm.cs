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
/// Built out of <see cref="DialogLayout"/>, which explains why everything here sizes to
/// its content instead of sitting at fixed coordinates.
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

        DialogLayout.Prepare(this, Strings.Current.SettingsTitle);

        Label title = DialogLayout.Heading(Strings.Current.PushToTalkHeading);

        _bindingBox = new TextBox
        {
            ReadOnly = true,
            Width = 250,
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(0, 0, 10, 0),
        };

        _changeButton = DialogLayout.Button(Strings.Current.ChangeBinding, new Size(120, 0));
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

        _hint = DialogLayout.Prose(ContentWidth, SystemColors.GrayText);

        Label devicesTitle = DialogLayout.Heading(Strings.Current.DetectedDevices);

        _devices = new ListBox
        {
            Size = new Size(ContentWidth, 74),
            IntegralHeight = false,
            Margin = new Padding(0, 8, 0, 0),
        };

        // Amber, and only filled in when a joystick binding makes it true. With AutoSize
        // an empty warning takes no room at all rather than leaving a gap.
        _warning = DialogLayout.Prose(ContentWidth, DialogLayout.Caution);

        Label sendingTitle = DialogLayout.Heading(Strings.Current.SendingHeading);

        _handsFreeBox = new CheckBox
        {
            Text = Strings.Current.HandsFreeCheckbox,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Checked = handsFree,
            Margin = new Padding(0, 8, 0, 0),
        };
        _handsFreeBox.CheckedChanged += (_, _) => RefreshHandsFree();

        _handsFreeNote = DialogLayout.Prose(ContentWidth, SystemColors.GrayText);

        _cuesBox = new CheckBox
        {
            Text = Strings.Current.AudibleCuesCheckbox,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Checked = audibleCues,
            Margin = new Padding(0, 12, 0, 0),
        };

        var buttonMargin = new Padding(8, 0, 0, 0);

        _okButton = DialogLayout.Button(Strings.Current.Ok, new Size(84, 0), buttonMargin);
        _okButton.DialogResult = DialogResult.OK;

        Button cancelButton = DialogLayout.Button(Strings.Current.Cancel, new Size(84, 0), buttonMargin);
        cancelButton.DialogResult = DialogResult.Cancel;

        // Cancel is added first, so right-to-left puts it rightmost and OK to its left.
        FlowLayoutPanel buttons = DialogLayout.ButtonRow(cancelButton, _okButton);
        buttons.Margin = new Padding(0, 16, 0, 0);

        Controls.Add(DialogLayout.Column(
            title, bindingRow, _hint, devicesTitle, _devices, _warning,
            sendingTitle, _handsFreeBox, _handsFreeNote, _cuesBox, buttons));
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

    /// <summary>
    /// Says what the checkbox above it actually means, in the colour that matches how
    /// much it matters - the same amber the joystick caveat uses when it applies.
    /// </summary>
    private void RefreshHandsFree()
    {
        _handsFreeNote.ForeColor = _handsFreeBox.Checked
            ? DialogLayout.Caution
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
