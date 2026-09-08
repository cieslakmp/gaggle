using Gaggle.Speech;
using Gaggle.Ui;
using Gaggle.Update;

namespace Gaggle.Tests;

/// <summary>
/// The checks that stand between a bad answer from a remote server and something
/// happening on this machine.
///
/// All three of these guard inputs the app does not control: an asset name chosen by
/// whoever published a release, a URL out of a JSON payload, and the bytes behind a
/// download link. None of them will ever look wrong in normal use, which is exactly why
/// they need a test rather than an eye.
/// </summary>
public class HardeningTests
{
    /// <summary>
    /// Path.Combine hands back a rooted string unchanged and follows "..\" wherever it
    /// leads, so an asset name is not a file name until something makes it one. The
    /// Startup folder is the case worth naming: it turns a download into a program that
    /// runs at next logon.
    /// </summary>
    [Theory]
    [InlineData(@"..\..\..\Microsoft\Windows\Start Menu\Programs\Startup\evil-win-x64.zip")]
    [InlineData(@"C:\Windows\Temp\evil-win-x64.zip")]
    [InlineData("/etc/passwd")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void APackageNameNeverEscapesTheUpdatesFolder(string? supplied)
    {
        string name = UpdateInstaller.PackageFileName(supplied);
        string combined = Path.GetFullPath(Path.Combine(@"C:\updates", name));

        Assert.StartsWith(@"C:\updates\", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(name, Path.GetFileName(name));
    }

    [Fact]
    public void AnOrdinaryPackageNameIsLeftAlone()
    {
        Assert.Equal("Gaggle-0.7.0-win-x64.zip", UpdateInstaller.PackageFileName("Gaggle-0.7.0-win-x64.zip"));
    }

    /// <summary>
    /// UseShellExecute dispatches by scheme, so anything that is not http(s) is a program
    /// launch rather than a page. The release's html_url comes from the GitHub API, which
    /// is the one link in the app that is not a compile-time constant.
    /// </summary>
    [Theory]
    [InlineData("https://github.com/cieslakmp/gaggle/releases/tag/v0.7.0", true)]
    [InlineData("http://example.com", true)]
    [InlineData(@"file:///C:/Windows/System32/calc.exe", false)]
    [InlineData(@"C:\Windows\System32\calc.exe", false)]
    [InlineData(@"\\attacker\share\evil.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("ms-settings:", false)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyWebLinksReachTheShell(string? url, bool expected)
    {
        Assert.Equal(expected, Shell.IsWebUrl(url));
    }

    /// <summary>
    /// Model files are handed to whisper.cpp, which parses them in native code, so an
    /// unverified download is a memory-safety surface rather than merely a wrong file.
    /// Every model on the menu has to carry the digest Hugging Face publishes for it.
    /// </summary>
    [Fact]
    public void EveryModelCarriesAChecksumAndASize()
    {
        foreach (ModelInstaller.ModelChoice choice in ModelInstaller.Available)
        {
            Assert.True(
                choice.Sha256.Length == 64 && choice.Sha256.All(Uri.IsHexDigit),
                $"{choice.FileName} has no usable SHA256.");

            Assert.Equal(choice.Sha256, choice.Sha256.ToLowerInvariant());
            Assert.True(choice.Bytes > 0, $"{choice.FileName} has no published size.");
        }
    }

    /// <summary>
    /// A digest pasted twice is a digest pasted wrong: two models sharing one would mean
    /// whichever was downloaded second could never pass.
    /// </summary>
    [Fact]
    public void NoTwoModelsShareAChecksum()
    {
        List<string> digests = [.. ModelInstaller.Available.Select(choice => choice.Sha256)];

        Assert.Equal(digests.Count, digests.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
