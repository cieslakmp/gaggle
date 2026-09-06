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
    private readonly Button _okButton;

    private PttBinding _binding;

    public SettingsForm(PttController controller, PttBinding current)
    {
        _controller = controller;
        _binding = current;

        Text = "Gaggle settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 330);
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

        _okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(226, 284),
            Width = 84,
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(318, 284),
            Width = 84,
        };

        Controls.AddRange([title, _bindingBox, _changeButton, _hint, devicesTitle, _devices, _warning, _okButton, cancelButton]);
        AcceptButton = _okButton;
        CancelButton = cancelButton;

        _controller.Captured += OnCaptured;

        RefreshDevices();
        RefreshBinding();
    }

    /// <summary>The binding chosen, valid once the dialog returns OK.</summary>
    public PttBinding Binding => _binding;

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
