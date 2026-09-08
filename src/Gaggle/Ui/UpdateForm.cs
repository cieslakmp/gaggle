using System.Windows.Forms;
using Gaggle.Localisation;
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
///
/// Auto-sizing for the reason <see cref="SettingsForm"/> explains. The notes box keeps a
/// fixed size on purpose: release notes have no length to size to, so they scroll.
/// </summary>
internal sealed class UpdateForm : Form
{
    private const int ContentWidth = 424;

    public UpdateForm(ReleaseInfo release, bool canInstallInPlace)
    {
        ArgumentNullException.ThrowIfNull(release);

        Choice = UpdateChoice.Later;

        Text = Strings.Current.UpdateTitle(AppInfo.Name);
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
            Text = Strings.Current.VersionIsAvailable(AppInfo.Name, release.Version),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Margin = new Padding(0),
        };

        var current = new Label
        {
            Text = canInstallInPlace
                ? Strings.Current.RunningWillRestart(AppInfo.Name, AppInfo.Version)
                : Strings.Current.RunningCannotUpdate(AppInfo.Version),
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(2, 10, 0, 0),
        };

        var notes = new TextBox
        {
            // Read-only rather than a Label: release notes are as long as they need to be,
            // and this way they scroll instead of being cut off.
            Text = Normalise(release.Notes),
            Size = new Size(ContentWidth, 160),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window,
            Margin = new Padding(2, 12, 0, 0),
        };

        var link = new LinkLabel
        {
            Text = Strings.Current.ViewReleaseOnGitHub,
            AutoSize = true,
            Margin = new Padding(2, 12, 0, 0),
        };
        link.LinkClicked += (_, _) => Shell.OpenUrl(release.HtmlUrl);

        var skip = new Button
        {
            Text = Strings.Current.SkipThisVersion,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(120, 26),
            Margin = new Padding(0),
        };
        skip.Click += (_, _) => Close(UpdateChoice.Skip);

        var later = new Button
        {
            Text = Strings.Current.Later,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(100, 26),
            Margin = new Padding(8, 0, 0, 0),
            DialogResult = DialogResult.Cancel,
        };

        var install = new Button
        {
            Text = canInstallInPlace ? Strings.Current.InstallNow : Strings.Current.OpenDownloads,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(100, 26),
            Margin = new Padding(8, 0, 0, 0),
            Anchor = AnchorStyles.Right,
        };
        install.Click += (_, _) => Close(canInstallInPlace ? UpdateChoice.Install : UpdateChoice.OpenPage);

        // Skip sits apart from the pair on the right: it is the one answer that is hard
        // to take back, so it does not want to be next to the button people reach for.
        var buttons = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 16, 0, 0),
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var rightPair = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0),
            WrapContents = false,
        };
        rightPair.Controls.Add(install);
        rightPair.Controls.Add(later);

        buttons.Controls.Add(skip, 0, 0);
        buttons.Controls.Add(rightPair, 1, 0);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        layout.Controls.AddRange([title, current, notes, link, buttons]);

        Controls.Add(layout);
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
            ? Strings.Current.NoReleaseNotes
            : notes.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\n", Environment.NewLine, StringComparison.Ordinal)
                .Trim();
}
