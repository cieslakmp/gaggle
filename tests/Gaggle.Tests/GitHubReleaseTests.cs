using Gaggle.Update;

namespace Gaggle.Tests;

/// <summary>
/// Reading a GitHub release payload.
///
/// Assets are matched by suffix rather than rebuilt from the version number, so that
/// renaming the artifact in release.yml cannot silently orphan every copy already
/// installed. These tests pin that contract, and pin that a release without a checksum is
/// reported as not installable rather than installed unverified.
/// </summary>
public class GitHubReleaseTests
{
    /// <summary>A real response, cut down to the fields that are read.</summary>
    private const string Payload = """
        {
          "tag_name": "v0.4.0",
          "name": "Gaggle 0.4.0",
          "draft": false,
          "prerelease": false,
          "html_url": "https://github.com/cieslakmp/gaggle/releases/tag/v0.4.0",
          "body": "Adds in-app updates.",
          "author": { "login": "cieslakmp" },
          "assets": [
            {
              "name": "Gaggle-0.4.0-win-x64.zip",
              "size": 84123456,
              "content_type": "application/zip",
              "browser_download_url": "https://github.com/cieslakmp/gaggle/releases/download/v0.4.0/Gaggle-0.4.0-win-x64.zip"
            },
            {
              "name": "Gaggle-0.4.0-win-x64.zip.sha256",
              "size": 90,
              "content_type": "text/plain",
              "browser_download_url": "https://github.com/cieslakmp/gaggle/releases/download/v0.4.0/Gaggle-0.4.0-win-x64.zip.sha256"
            }
          ]
        }
        """;

    [Fact]
    public void ReadsTheVersionNotesAndPageFromARelease()
    {
        ReleaseInfo release = Assert.IsType<ReleaseInfo>(GitHubReleases.ParseRelease(Payload));

        // Normalised to three parts, so it can be compared with AppInfo.Version.
        Assert.Equal("0.4.0", release.Version);
        Assert.Equal("Adds in-app updates.", release.Notes);
        Assert.EndsWith("/releases/tag/v0.4.0", release.HtmlUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void PicksThePackageAndItsChecksumApart()
    {
        ReleaseInfo release = Assert.IsType<ReleaseInfo>(GitHubReleases.ParseRelease(Payload));

        Assert.Equal("Gaggle-0.4.0-win-x64.zip", release.PackageName);
        Assert.EndsWith("Gaggle-0.4.0-win-x64.zip", release.PackageUrl!, StringComparison.Ordinal);
        Assert.EndsWith("Gaggle-0.4.0-win-x64.zip.sha256", release.ChecksumUrl!, StringComparison.Ordinal);
        Assert.Equal(84_123_456, release.PackageSize);
        Assert.True(release.IsInstallable);
    }

    [Fact]
    public void MatchesAFutureVersionNumberItHasNeverSeen()
    {
        // The suffix contract, not the exact file name, is what the updater relies on.
        string payload = Payload.Replace("0.4.0", "12.0.99", StringComparison.Ordinal);

        ReleaseInfo release = Assert.IsType<ReleaseInfo>(GitHubReleases.ParseRelease(payload));

        Assert.Equal("Gaggle-12.0.99-win-x64.zip", release.PackageName);
        Assert.True(release.IsInstallable);
    }

    [Fact]
    public void ReportsAReleaseWithNoChecksumAsNotInstallable()
    {
        // Everything published before checksums existed. It is still an update worth
        // announcing; it just cannot be installed without being verified first.
        string payload = """
            {
              "tag_name": "v0.3.1",
              "html_url": "https://github.com/cieslakmp/gaggle/releases/tag/v0.3.1",
              "body": "",
              "assets": [
                {
                  "name": "Gaggle-0.3.1-win-x64.zip",
                  "size": 80000000,
                  "browser_download_url": "https://example.invalid/Gaggle-0.3.1-win-x64.zip"
                }
              ]
            }
            """;

        ReleaseInfo release = Assert.IsType<ReleaseInfo>(GitHubReleases.ParseRelease(payload));

        Assert.NotNull(release.PackageUrl);
        Assert.Null(release.ChecksumUrl);
        Assert.False(release.IsInstallable);
    }

    [Fact]
    public void ReportsAReleaseWithNoWindowsPackageAsNotInstallable()
    {
        string payload = """
            {
              "tag_name": "v0.5.0",
              "html_url": "https://example.invalid/tag",
              "body": "Source only.",
              "assets": []
            }
            """;

        ReleaseInfo release = Assert.IsType<ReleaseInfo>(GitHubReleases.ParseRelease(payload));

        Assert.Null(release.PackageUrl);
        Assert.False(release.IsInstallable);
        Assert.Equal("0.5.0", release.Version);
    }

    [Fact]
    public void FallsBackToTheReleasesPageWhenTheUrlIsMissing()
    {
        ReleaseInfo release = Assert.IsType<ReleaseInfo>(
            GitHubReleases.ParseRelease("""{ "tag_name": "v0.5.0" }"""));

        Assert.Equal(GitHubReleases.ReleasesPageUrl, release.HtmlUrl);
        Assert.Equal(string.Empty, release.Notes);
    }

    [Theory]
    [InlineData("""{ "message": "Not Found" }""")]
    [InlineData("""{ "tag_name": "nightly" }""")]
    [InlineData("[]")]
    public void RefusesAPayloadWithNoUsableVersion(string payload)
    {
        Assert.Null(GitHubReleases.ParseRelease(payload));
    }

    [Fact]
    public void BuildsTheApiUrlFromTheRepositoryUrl()
    {
        // Written down once, so the owner and repo cannot drift apart.
        Assert.Equal(
            "https://api.github.com/repos/cieslakmp/gaggle/releases/latest",
            GitHubReleases.LatestReleaseUrl);
    }
}
