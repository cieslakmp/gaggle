using Gaggle.Net;
using Gaggle.Speech;

namespace Gaggle.Tests;

/// <summary>
/// Describe() reads the localised table for the word between the two sizes, so these
/// assert the English wording. See the note on <see cref="PttBindingTests"/>.
/// </summary>
public class DownloadProgressTests
{
    [Fact]
    public void ReportsFractionWhenTheTotalIsKnown()
    {
        var progress = new DownloadProgress(50, 200);

        Assert.Equal(0.25d, progress.Fraction!.Value, 4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    public void HasNoFractionWithoutAUsableTotal(long? total)
    {
        var progress = new DownloadProgress(50, total);

        Assert.Null(progress.Fraction);
    }

    [Fact]
    public void DescribesPercentageAndMegabytes()
    {
        var progress = new DownloadProgress(65_123_456, 147_964_211);

        string text = progress.Describe();

        Assert.Contains("44%", text);
        Assert.Contains("141.1 MB", text);
    }

    [Fact]
    public void DescribesBytesOnlyWhenTheServerSendsNoLength()
    {
        // No Content-Length means no percentage, but silence would look like a hang.
        var progress = new DownloadProgress(1_048_576, null);

        string text = progress.Describe();

        Assert.Equal("1.0 MB", text);
        Assert.DoesNotContain("%", text);
    }

    [Fact]
    public void DescribesACompletedDownloadAsAHundredPercent()
    {
        var progress = new DownloadProgress(147_964_211, 147_964_211);

        Assert.Contains("100%", progress.Describe());
    }

    [Fact]
    public void EveryOfferedModelIsAnEnglishGgmlFile()
    {
        Assert.NotEmpty(ModelInstaller.Available);

        foreach (ModelInstaller.ModelChoice choice in ModelInstaller.Available)
        {
            Assert.StartsWith("ggml-", choice.FileName);
            Assert.EndsWith(".bin", choice.FileName);
            Assert.False(string.IsNullOrWhiteSpace(choice.Name));
        }
    }

    [Fact]
    public void ReportsAMissingModelAsNotInstalled()
    {
        string absent = Path.Combine(Path.GetTempPath(), "gaggle-no-such-model-" + Guid.NewGuid().ToString("N") + ".bin");

        Assert.False(ModelInstaller.IsInstalled(absent));
    }
}
