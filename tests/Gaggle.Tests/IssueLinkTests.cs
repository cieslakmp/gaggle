using System.Collections.Specialized;
using System.Web;
using Gaggle;
using Gaggle.Configuration;

namespace Gaggle.Tests;

/// <summary>
/// The prefill is a contract with the YAML in .github/ISSUE_TEMPLATE, and it fails
/// quietly on both sides: GitHub answers an unknown template or an unknown field id
/// with a perfectly ordinary empty form. These pin the template names and the field
/// ids, and check that the setup block carries what a report is otherwise missing.
/// </summary>
public class IssueLinkTests
{
    [Theory]
    [InlineData(IssueLink.BugTemplate)]
    [InlineData(IssueLink.SuggestionTemplate)]
    public void TemplateFilesExist(string template)
    {
        // Walk up from the test binary to the repository root.
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".github")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        string path = Path.Combine(directory!.FullName, ".github", "ISSUE_TEMPLATE", template);

        Assert.True(File.Exists(path), $"the tray menu links to a template that is not there: {path}");
    }

    [Fact]
    public void BugLinkOpensTheBugFormWithTheVersionFilledIn()
    {
        var uri = new Uri(IssueLink.For(IssueLink.BugTemplate));

        Assert.Equal("github.com", uri.Host);
        Assert.Equal("/cieslakmp/gaggle/issues/new", uri.AbsolutePath);

        NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);

        Assert.Equal(IssueLink.BugTemplate, query["template"]);
        Assert.Equal(AppInfo.Version, query["version"]);
    }

    [Fact]
    public void SetupBlockSurvivesTheQueryStringIntact()
    {
        var config = new AppConfig { WhisperModelFile = "ggml-small.bin", Language = "pl" };
        string environment = IssueLink.DescribeEnvironment(config, "Yeti Nano", condorRunning: true);

        NameValueCollection query =
            HttpUtility.ParseQueryString(new Uri(IssueLink.For(IssueLink.BugTemplate, environment)).Query);

        // The newlines in the block must come back unmangled, or the setup arrives as
        // one unreadable line.
        Assert.Equal(environment, query["environment"]);
    }

    [Fact]
    public void SetupBlockNamesWhatDecidesTheSpeechPath()
    {
        var config = new AppConfig { WhisperModelFile = "ggml-small.bin", Language = "pl" };

        string environment = IssueLink.DescribeEnvironment(config, "Yeti Nano", condorRunning: false);

        Assert.Contains(AppInfo.Version, environment, StringComparison.Ordinal);
        Assert.Contains("ggml-small.bin", environment, StringComparison.Ordinal);
        Assert.Contains("Polski (pl)", environment, StringComparison.Ordinal);
        Assert.Contains("Yeti Nano", environment, StringComparison.Ordinal);
        Assert.Contains("not running", environment, StringComparison.Ordinal);
    }

    [Fact]
    public void OmittedSetupLeavesTheFieldOutRatherThanEmpty()
    {
        Assert.DoesNotContain("environment=", IssueLink.For(IssueLink.SuggestionTemplate), StringComparison.Ordinal);
    }

    [Fact]
    public void ChooserIsAnHttpsUrl()
    {
        Assert.True(Uri.TryCreate(IssueLink.ChooserUrl, UriKind.Absolute, out Uri? uri));
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
        Assert.Equal("github.com", uri.Host);
    }
}
