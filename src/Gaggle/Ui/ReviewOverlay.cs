using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Gaggle.Ui;

/// <summary>
/// Always-on-top strip showing the transcript before it is sent.
///
/// It must never take focus. If it did, Condor would lose the foreground and the
/// injected keystrokes would land here instead of in the game — and in full screen
/// the sim would likely minimise. Hence WS_EX_NOACTIVATE plus ShowWithoutActivation;
/// the confirm and cancel keys are read by the global hook, not by this window.
/// </summary>
internal sealed class ReviewOverlay : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopMost = 0x00000008;

    private readonly Label _transcript;
    private readonly Label _hint;

    public ReviewOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(18, 20, 24);
        Opacity = 0.92;
        Size = new Size(720, 96);
        Padding = new Padding(20, 14, 20, 14);

        _transcript = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        _hint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = Color.FromArgb(150, 160, 175),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        Controls.Add(_transcript);
        Controls.Add(_hint);
    }

    /// <summary>Stops the window activating when shown.</summary>
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow | WsExTopMost;
            return parameters;
        }
    }

    public void ShowTranscript(string text, Keys confirmKey, Keys cancelKey)
    {
        _transcript.ForeColor = Color.White;
        _transcript.Text = text;
        _hint.Text = $"{confirmKey} to send    ·    {cancelKey} to discard";

        PositionAboveTaskbar();
        Show();
    }

    /// <summary>Transient status line — recording, transcribing, or an error.</summary>
    public void ShowStatus(string text, bool isError = false)
    {
        _transcript.ForeColor = isError ? Color.FromArgb(255, 140, 140) : Color.FromArgb(170, 200, 255);
        _transcript.Text = text;
        _hint.Text = string.Empty;

        PositionAboveTaskbar();
        Show();
    }

    public void HideOverlay()
    {
        if (Visible)
        {
            Hide();
        }
    }

    private void PositionAboveTaskbar()
    {
        Rectangle screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            screen.X + ((screen.Width - Width) / 2),
            screen.Y + screen.Height - Height - 80);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var border = new Pen(Color.FromArgb(70, 80, 95), 1f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }
}
