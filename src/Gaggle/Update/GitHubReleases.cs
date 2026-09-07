using System.Text.Json;
using Gaggle.Net;

namespace Gaggle.Update;

/// <summary>
/// Asks GitHub what the newest published release is.
///
/// The release workflow creates releases as drafts, and the "latest" endpoint skips both
/// drafts and pre-releases, so an unreviewed build can never be offered to anyone. That
/// is the whole gate: there is no separate "stable" channel to maintain.
/// </summary>
public static class GitHubReleases
{
    /// <summary>
    /// The asset the updater installs, matched by suffix rather than rebuilt from the
    /// version number, so a future rename in the workflow cannot silently orphan every
    /// copy already in the wild. Renaming these breaks installed clients — see
    /// .github/workflows/release.yml.
    /// </summary>
    private const string PackageSuffix = "-win-x64.zip";

    private const string ChecksumSuffix = "-win-x64.zip.sha256";

    /// <summary>Long enough for a slow link, short enough not to stall a startup check.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Where a user is sent when the app cannot install the update itself.</summary>
    public static string ReleasesPageUrl => AppInfo.RepositoryUrl + "/releases/latest";

    /// <summary>
    /// The API endpoint, derived from <see cref="AppInfo.RepositoryUrl"/> so the
    /// owner/repo pair is written down exactly once.
    /// </summary>
    public static string LatestReleaseUrl =>
        "https://api.github.com/repos/"
        + AppInfo.RepositoryUrl.Replace("https://github.com/", string.Empty, StringComparison.OrdinalIgnoreCase)
        + "/releases/latest";

    /// <summary>
    /// Fetches the latest release, or null when GitHub cannot be reached or answers with
    /// something unexpected. A background check that fails must be invisible; the caller
    /// decides whether to say anything.
    /// </summary>
    public static async Task<ReleaseInfo?> FetchLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string json = await FileDownloader.GetStringAsync(
                LatestReleaseUrl,
                Timeout,
                [new KeyValuePair<string, string>("Accept", "application/vnd.github+json")],
                cancellationToken);

            return ParseRelease(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Pulls the interesting fields out of a release payload. Kept separate from the
    /// fetch, and reading through <see cref="JsonDocument"/> rather than a DTO, so it can
    /// be tested against a captured response and so the dozens of fields we ignore cost
    /// nothing.
    /// </summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? tag = ReadString(root, "tag_name");

        if (!ReleaseVersion.TryParse(tag, out Version? version))
        {
            return null;
        }

        string? packageUrl = null;
        string packageName = string.Empty;
        long packageSize = 0;
        string? checksumUrl = null;

        if (root.TryGetProperty("assets", out JsonElement assets)
            && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string name = ReadString(asset, "name") ?? string.Empty;
                string? url = ReadString(asset, "browser_download_url");

                if (url is null)
                {
                    continue;
                }

                if (name.EndsWith(ChecksumSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    checksumUrl = url;
                }
                else if (name.EndsWith(PackageSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    packageUrl = url;
                    packageName = name;
                    packageSize = asset.TryGetProperty("size", out JsonElement size)
                        && size.TryGetInt64(out long bytes) ? bytes : 0;
                }
            }
        }

        return new ReleaseInfo(
            version.ToString(3),
            ReadString(root, "html_url") ?? ReleasesPageUrl,
            ReadString(root, "body") ?? string.Empty,
            packageName,
            packageUrl,
            packageSize,
            checksumUrl);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// A published release, reduced to what the updater needs.
///
/// <paramref name="PackageUrl"/> is null when the release carries no Windows package, and
/// <paramref name="ChecksumUrl"/> is null for anything published before checksums were
/// added. Both cases mean "cannot install this automatically", not "no update".
/// </summary>
public sealed record ReleaseInfo(
    string Version,
    string HtmlUrl,
    string Notes,
    string PackageName,
    string? PackageUrl,
    long PackageSize,
    string? ChecksumUrl)
{
    /// <summary>Whether this release can be installed rather than merely announced.</summary>
    public bool IsInstallable => PackageUrl is not null && ChecksumUrl is not null;
}
