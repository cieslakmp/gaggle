using System.Text.RegularExpressions;
using Gaggle;

namespace Gaggle.Tests;

/// <summary>
/// The version reaches users through the tray tooltip and the About box, and comes
/// back in bug reports. These pin the shape of it, because the failure mode is
/// cosmetic-looking and quietly useless: a tooltip reading "Gaggle 0.3.0.0+8f2c1a…"
/// tells nobody anything and crowds out the status it sits next to.
/// </summary>
public class AppInfoTests
{
    [Fact]
    public void VersionIsPresent()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
        Assert.NotEqual("unknown", AppInfo.Version);
    }

    [Fact]
    public void VersionIsWrittenTheWayAPersonWouldWriteIt()
    {
        // Three parts, no four-part padding, and no "+<commit>" build metadata.
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+$"), AppInfo.Version);
    }

    [Fact]
    public void VersionLeavesRoomForTheStatusBesideIt()
    {
        // NotifyIcon.Text throws above 63 characters, and the tooltip is
        // "Gaggle <version> — <status>". The status is the useful half.
        string prefix = $"{AppInfo.Name} {AppInfo.Version} — ";

        Assert.True(prefix.Length <= 24, $"tooltip prefix crowds out the status: '{prefix}'");
    }

    [Fact]
    public void CreditsTheAuthor()
    {
        Assert.Equal("Maciej Cieslak", AppInfo.Author);
    }

    [Fact]
    public void RepositoryIsAnHttpsUrl()
    {
        // The About box hands this straight to the shell, so it had better be a URL
        // and not, say, a path that would open something local.
        Assert.True(Uri.TryCreate(AppInfo.RepositoryUrl, UriKind.Absolute, out Uri? uri));
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
        Assert.Equal("github.com", uri.Host);
    }

    [Fact]
    public void DescriptionSaysWhatTheAppIs()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Description));
        Assert.Contains("Condor", AppInfo.Description, StringComparison.OrdinalIgnoreCase);
    }
}
