using System.Windows.Forms;
using Gaggle.Configuration;
using Gaggle.Localisation;

namespace Gaggle.Ui;

/// <summary>
/// Says what this is, which version, and where to report a problem.
///
/// The data folder is here rather than only in the README because a bug report is
/// worth far more with a config.json attached, and this is where someone will look
/// for it.
///
/// Auto-sizing for the reason <see cref="SettingsForm"/> explains: the description and
/// the folder path are both longer in some languages than in others.
/// </summary>
internal sealed class AboutForm : Form
{
    private const int ContentWidth = 380;

    public AboutForm()
    {
        Text = Strings.Current.AboutTitle(AppInfo.Name);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 9f);

        var title = new Label
        {
            Text = $"{AppInfo.Name} {AppInfo.Version}",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0),
        };

        var description = new Label
        {
            Text = AppInfo.Description,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(2, 8, 0, 0),
        };

        var author = new Label
        {
            Text = Strings.Current.ByAuthor(AppInfo.Author),
            AutoSize = true,
            Margin = new Padding(2, 12, 0, 0),
        };

        var repository = new LinkLabel
        {
            Text = AppInfo.RepositoryUrl,
            AutoSize = true,
            Margin = new Padding(2, 10, 0, 0),
        };
        repository.LinkClicked += (_, _) => Shell.OpenUrl(AppInfo.RepositoryUrl);

        var report = new LinkLabel
        {
            Text = Strings.Current.ReportBugOrIdea,
            AutoSize = true,
            Margin = new Padding(2, 4, 0, 0),
        };
        report.LinkClicked += (_, _) => Shell.OpenUrl(IssueLink.ChooserUrl);

        var dataLabel = new Label
        {
            Text = Strings.Current.SettingsAndModels,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(2, 18, 0, 0),
        };

        var dataFolder = new LinkLabel
        {
            Text = AppConfig.DataDirectory,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Margin = new Padding(2, 4, 0, 0),
        };
        dataFolder.LinkClicked += (_, _) => Shell.OpenPath(AppConfig.DataDirectory);

        var close = new Button
        {
            Text = Strings.Current.Close,
            DialogResult = DialogResult.OK,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 0),
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 18, 0, 0),
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        layout.Controls.AddRange([
            title, description, author, repository, report, dataLabel, dataFolder, close,
        ]);

        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }
}
