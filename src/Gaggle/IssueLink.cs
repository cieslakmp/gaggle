using System.Text;
using Gaggle.Configuration;
using Gaggle.Speech;

namespace Gaggle;

/// <summary>
/// Builds "new issue" links against the forms in <c>.github/ISSUE_TEMPLATE</c>.
///
/// GitHub prefills an issue form from query parameters named after the field ids, so
/// the version and the setup block arrive already filled in and a one-line report is
/// still worth reading. That makes the template file names and the field ids a
/// contract with the YAML: rename either there and the link still opens a perfectly
/// normal empty form, with nothing to say it went wrong.
/// </summary>
public static class IssueLink
{
    /// <summary>Matches the file name in <c>.github/ISSUE_TEMPLATE</c>.</summary>
    public const string BugTemplate = "bug_report.yml";

    /// <summary>Matches the file name in <c>.github/ISSUE_TEMPLATE</c>.</summary>
    public const string SuggestionTemplate = "suggestion.yml";

    /// <summary>The template picker, for anyone arriving without a form in mind.</summary>
    public static string ChooserUrl => AppInfo.RepositoryUrl + "/issues/new/choose";

    /// <summary>
    /// A link that opens <paramref name="template"/> with the version filled in, and
    /// the setup block too when one is given.
    /// </summary>
    public static string For(string template, string? environment = null)
    {
        StringBuilder url = new StringBuilder(AppInfo.RepositoryUrl)
            .Append("/issues/new?template=")
            .Append(Uri.EscapeDataString(template))
            .Append("&version=")
            .Append(Uri.EscapeDataString(AppInfo.Version));

        if (!string.IsNullOrWhiteSpace(environment))
        {
            url.Append("&environment=").Append(Uri.EscapeDataString(environment));
        }

        return url.ToString();
    }

    /// <summary>
    /// The setup block: the handful of settings that decide how the speech path
    /// behaves, so they do not have to be asked for one at a time.
    ///
    /// Deliberately not the whole config — the form asks for that separately, and a
    /// URL long enough to hold it risks being cut short by the browser.
    /// </summary>
    public static string DescribeEnvironment(AppConfig config, string microphone, bool condorRunning)
    {
        ArgumentNullException.ThrowIfNull(config);

        return string.Join(
            Environment.NewLine,
            $"Gaggle:      {AppInfo.Version}",
            $"Windows:     {Environment.OSVersion.Version}",
            $"Model:       {config.WhisperModelFile}",
            $"Language:    {SpokenLanguage.Describe(config.Language)} ({config.Language})",
            $"Microphone:  {microphone}",
            $"Fast:        {config.FastTranscription}",
            $"Review:      {config.ReviewBeforeSending}",
            $"Condor:      {(condorRunning ? "running" : "not running")}");
    }
}
