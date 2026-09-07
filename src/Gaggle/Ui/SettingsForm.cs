using System.Windows.Forms;
using Gaggle.Input;

namespace Gaggle.Ui;

/// <summary>
/// Lets the user rebind push-to-talk by pressing the key or button they want.
///
/// Capture goes through <see cref="PttController"/> rather than this window's own
/// key events, for two reasons: the global hook sees keys the form would never
/// receive (Caps Lock, media keys), and joystick buttons produce no window messages
/// at all.
/// </summary>
internal sealed class SettingsForm : Form
{
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

        Text = "Gaggle settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 486);
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = "Push-to-talk",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(16, 16),
            AutoSize = true,
        };

        _bindingBox = new TextBox
        {
            ReadOnly = true,
            Location = new Point(16, 44),
            Width = 250,
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 10f),
        };

        _changeButton = new Button
        {
            Text = "Change…",
            Location = new Point(276, 43),
            Width = 120,
        };
        _changeButton.Click += (_, _) => ToggleCapture();

        _hint = new Label
        {
            Location = new Point(16, 76),
            Width = 380,
            Height = 32,
            ForeColor = SystemColors.GrayText,
        };

        var devicesTitle = new Label
        {
            Text = "Detected devices",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Location = new Point(16, 116),
            AutoSize = true,
        };

        _devices = new ListBox
        {
            Location = new Point(16, 142),
            Size = new Size(380, 74),
            IntegralHeight = false,
        };

        _warning = new Label
        {
            Location = new Point(16, 224),
            Width = 380,
            Height = 48,
            ForeColor = Color.FromArgb(160, 90, 0),
        };

        var sendingTitle = new Label
        {
            Text = "Sending",
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(16, 288),
            AutoSize = true,
        };

        _handsFreeBox = new CheckBox
        {
            Text = "Hands-free — send without reviewing",
            Location = new Point(16, 314),
            AutoSize = true,
            Checked = handsFree,
        };
        _handsFreeBox.CheckedChanged += (_, _) => RefreshHandsFree();

        _handsFreeNote = new Label
        {
            Location = new Point(16, 340),
            Width = 380,
            Height = 64,
        };

        _cuesBox = new CheckBox
        {
            Text = "Play a tone when recording starts, sends, or is dropped",
            Location = new Point(16, 410),
            AutoSize = true,
            Checked = audibleCues,
        };

        _okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(226, 440),
            Width = 84,
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(318, 440),
            Width = 84,
        };

        Controls.AddRange([
            title, _bindingBox, _changeButton, _hint, devicesTitle, _devices, _warning,
            sendingTitle, _handsFreeBox, _handsFreeNote, _cuesBox, _okButton, cancelButton,
        ]);
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
            ? Color.FromArgb(160, 90, 0)
            : SystemColors.GrayText;

        _handsFreeNote.Text = _handsFreeBox.Checked
            ? "Whatever is transcribed goes straight into chat, mishearings included, "
                + "with nothing to read or discard first. Recordings are also cut shorter "
                + "than usual, because nobody is watching what the time limit sends."
            : "Every message waits in the overlay first: Enter sends it, Escape discards "
                + "it. In VR that overlay cannot be seen or answered — that is what "
                + "hands-free is for.";
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

        _changeButton.Text = "Stop";
        _bindingBox.Text = "Press a key or button…";
        _hint.Text = "Listening. Every keystroke is captured, so use the Stop button "
            + "with the mouse if you change your mind.";
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
        _changeButton.Text = "Change…";
        _bindingBox.Text = _binding.Describe();
        _okButton.Enabled = true;

        _hint.Text = _binding.IsKeyboard
            ? "This key is hidden from Condor while Gaggle is running."
            : "Joystick buttons cannot be hidden from Condor.";

        _warning.Text = _binding.IsKeyboard
            ? string.Empty
            : "Condor will still see this button. Pick one the sim does not use, or it "
                + "will do both things at once.";
    }

    private void RefreshDevices()
    {
        _devices.Items.Clear();

        IReadOnlyList<(int Id, string Name, int Buttons)> found = JoystickWatcher.ListDevices();

        if (found.Count == 0)
        {
            _devices.Items.Add("No joysticks detected — keyboard only.");
            return;
        }

        foreach ((int id, string name, int buttons) in found)
        {
            _devices.Items.Add($"Stick {id + 1}: {name} ({buttons} buttons)");
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller.Captured -= OnCaptured;
        _controller.CancelCapture();

        base.OnFormClosed(e);
    }
}
