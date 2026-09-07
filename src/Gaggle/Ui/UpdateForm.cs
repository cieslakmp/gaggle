using System.Diagnostics;
using System.Windows.Forms;
using Gaggle.Update;

namespace Gaggle.Ui;

/// <summary>What the user decided about an available update.</summary>
internal enum UpdateChoice
{
    /// <summary>Ask again on the next background check.</summary>
    Later,

    /// <summary>Download it and restart into it.</summary>
    Install,

    /// <summary>Stay quiet about this version until a newer one, or a manual check.</summary>
    Skip,

    /// <summary>Hand it to the browser, because this install cannot be replaced in place.</summary>
    OpenPage,
}

/// <summary>
/// Offers a release, with its notes, and three ways out.
///
/// A MessageBox cannot express Install / Later / Skip without pressing Yes-No-Cancel into
/// service and hoping the user reads the prompt carefully, and the release notes are the
/// main thing someone wants before agreeing to restart mid-session.
/// </summary>
internal sealed class UpdateForm : Form
{
    public UpdateForm(ReleaseInfo release, bool canInstallInPlace)
    {
        ArgumentNullException.ThrowIfNull(release);

        Choice = UpdateChoice.Later;

        Text = $"Update {AppInfo.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 330);
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = $"{AppInfo.Name} {release.Version} is available",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point(16, 16),
            AutoSize = true,
        };

        var current = new Label
        {
            Text = canInstallInPlace
                ? $"You are running {AppInfo.Version}. Gaggle will restart to finish."
                : $"You are running {AppInfo.Version}. This copy cannot update itself.",
            Location = new Point(18, 48),
            Width = 424,
            ForeColor = SystemColors.GrayText,
            AutoSize = false,
            Height = 18,
        };

        var notes = new TextBox
        {
            // Read-only rather than a Label: release notes are as long as they need to be,
            // and this way they scroll instead of being cut off.
            Text = Normalise(release.Notes),
            Location = new Point(18, 76),
            Size = new Size(424, 160),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window,
        };

        var link = new LinkLabel
        {
            Text = "View this release on GitHub",
            Location = new Point(18, 246),
            AutoSize = true,
        };
        link.LinkClicked += (_, _) => Open(release.HtmlUrl);

        var skip = new Button
        {
            Text = "Skip this version",
            Location = new Point(18, 282),
            Width = 120,
            Height = 26,
        };
        skip.Click += (_, _) => Close(UpdateChoice.Skip);

        var later = new Button
        {
            Text = "Later",
            Location = new Point(238, 282),
            Width = 100,
            Height = 26,
            DialogResult = DialogResult.Cancel,
        };

        var install = new Button
        {
            Text = canInstallInPlace ? "Install now" : "Open downloads",
            Location = new Point(344, 282),
            Width = 100,
            Height = 26,
        };
        install.Click += (_, _) => Close(canInstallInPlace ? UpdateChoice.Install : UpdateChoice.OpenPage);

        Controls.AddRange([title, current, notes, link, skip, later, install]);
        AcceptButton = install;
        CancelButton = later;
    }

    /// <summary>What the user picked. Meaningless until the dialog has closed.</summary>
    public UpdateChoice Choice { get; private set; }

    private void Close(UpdateChoice choice)
    {
        Choice = choice;
        DialogResult = DialogResult.OK;
    }

    /// <summary>
    /// GitHub sends notes with bare newlines in places; a TextBox only breaks lines on
    /// CRLF, so without this the whole changelog arrives as one run-on line.
    /// </summary>
    private static string Normalise(string notes) =>
        string.IsNullOrWhiteSpace(notes)
            ? "No release notes were published."
            : notes.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\n", Environment.NewLine, StringComparison.Ordinal)
                .Trim();

    /// <summary>
    /// Hands a URL to the shell. A dead link should not take the tray icon down with it,
    /// so failures are swallowed — the same bargain <see cref="AboutForm"/> makes.
    /// </summary>
    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
        }
    }
}
