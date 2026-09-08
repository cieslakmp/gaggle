using System.Windows.Forms;

namespace Gaggle.Ui;

/// <summary>
/// The chrome and the controls the three dialogs all wanted, in one place.
///
/// They are laid out as a single auto-sizing column rather than at fixed coordinates,
/// and that is not a style choice: half of what they hold is two or three sentences of
/// explanation, and a translation runs longer than the English it replaced — Polish by
/// a fifth or so. A fixed height turns that into a clipped sentence, which is reliably
/// the half that mattered. Every helper here therefore sizes to its content.
///
/// Amber is the one colour worth naming. It marks the two places where a setting does
/// something the user may not expect — a joystick button Condor also sees, and sending
/// without review — and it means the same thing in both.
/// </summary>
internal static class DialogLayout
{
    /// <summary>The shade used for a caveat that applies. Not an error; a raised eyebrow.</summary>
    public static readonly Color Caution = Color.FromArgb(160, 90, 0);

    /// <summary>
    /// Applies the chrome every dialog here shares: fixed, centred, no minimise or
    /// maximise, and sized to whatever it ends up holding.
    /// </summary>
    public static void Prepare(Form form, string title)
    {
        form.Text = title;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        form.Padding = new Padding(16);
        form.Font = new Font("Segoe UI", 9f);
    }

    /// <summary>The single growing column the dialogs stack their content in.</summary>
    public static TableLayoutPanel Column(params Control[] rows)
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        layout.Controls.AddRange(rows);

        return layout;
    }

    /// <summary>A row of buttons running right to left, so the first added sits rightmost.</summary>
    public static FlowLayoutPanel ButtonRow(params Control[] buttons)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
            WrapContents = false,
        };

        row.Controls.AddRange(buttons);

        return row;
    }

    /// <summary>A section title.</summary>
    public static Label Heading(string text, float size = 10f) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(0, 16, 0, 0),
    };

    /// <summary>A paragraph that wraps at <paramref name="width"/> and grows downwards.</summary>
    public static Label Prose(int width, Color colour, string text = "") => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(width, 0),
        ForeColor = colour,
        Margin = new Padding(0, 6, 0, 0),
    };

    /// <summary>
    /// A button sized to its caption. The minimum is a floor rather than a size: a
    /// one-word caption still has to look like a button, and "Zainstaluj teraz" still
    /// has to fit.
    /// </summary>
    public static Button Button(string text, Size minimum, Padding margin = default) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = minimum,
        Margin = margin,
    };
}
