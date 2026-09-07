using System.Diagnostics;
using System.Windows.Forms;
using Gaggle.Configuration;

namespace Gaggle.Ui;

/// <summary>
/// Says what this is, which version, and where to report a problem.
///
/// The data folder is here rather than only in the README because a bug report is
/// worth far more with a config.json attached, and this is where someone will look
/// for it.
/// </summary>
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = $"About {AppInfo.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 252);
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = $"{AppInfo.Name} {AppInfo.Version}",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Location = new Point(16, 16),
            AutoSize = true,
        };

        var description = new Label
        {
            Text = AppInfo.Description,
            Location = new Point(18, 50),
            Width = 366,
            Height = 32,
            ForeColor = SystemColors.GrayText,
        };

        var author = new Label
        {
            Text = $"by {AppInfo.Author}",
            Location = new Point(18, 86),
            AutoSize = true,
        };

        var repository = new LinkLabel
        {
            Text = AppInfo.RepositoryUrl,
            Location = new Point(18, 112),
            AutoSize = true,
        };
        repository.LinkClicked += (_, _) => Open(AppInfo.RepositoryUrl);

        var report = new LinkLabel
        {
            Text = "Report a bug or suggest an idea",
            Location = new Point(18, 132),
            AutoSize = true,
        };
        report.LinkClicked += (_, _) => Open(IssueLink.ChooserUrl);

        var dataLabel = new Label
        {
            Text = "Settings and speech models:",
            Location = new Point(18, 166),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };

        var dataFolder = new LinkLabel
        {
            Text = AppConfig.DataDirectory,
            Location = new Point(18, 186),
            Width = 366,
            AutoEllipsis = true,
        };
        dataFolder.LinkClicked += (_, _) => Open(AppConfig.DataDirectory);

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(300, 214),
            Width = 84,
        };

        Controls.AddRange([title, description, author, repository, report, dataLabel, dataFolder, close]);
        AcceptButton = close;
        CancelButton = close;
    }

    /// <summary>
    /// Hands a URL or folder to the shell. A dead link or a missing folder should not
    /// take the tray icon down with it, so failures are swallowed.
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
