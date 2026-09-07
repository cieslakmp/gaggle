using Gaggle.Update;

namespace Gaggle.Tests;

/// <summary>
/// Reading the published SHA256 for an update package.
///
/// This is the only thing standing between a tampered or truncated download and a script
/// that copies it over a working install, so anything that is not unambiguously a hash has
/// to come back null. Returning a partial or malformed value would be worse than useless:
/// it would compare unequal and be reported as a corrupt download.
/// </summary>
public class ChecksumTests
{
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public void ReadsABareHash()
    {
        Assert.Equal(Hash, UpdateInstaller.ParseChecksum(Hash));
    }

    [Fact]
    public void ReadsTheShaTwoFiveSixSumFormat()
    {
        Assert.Equal(Hash, UpdateInstaller.ParseChecksum($"{Hash}  Gaggle-0.4.0-win-x64.zip"));
    }

    [Fact]
    public void ReadsWhatGetFileHashPipelinesWriteWithCrlfAndTrailingBlankLines()
    {
        Assert.Equal(Hash, UpdateInstaller.ParseChecksum($"{Hash}  Gaggle-0.4.0-win-x64.zip\r\n\r\n"));
    }

    [Fact]
    public void LowercasesAnUppercaseHash()
    {
        // Get-FileHash returns uppercase; sha256sum lowercase. Both have to compare equal.
        Assert.Equal(Hash, UpdateInstaller.ParseChecksum(Hash.ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9f86d081")]
    [InlineData("<!DOCTYPE html><html><head><title>404</title></head></html>")]
    [InlineData("zzzzd081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08")]
    public void RefusesAnythingThatIsNotAHash(string? text)
    {
        Assert.Null(UpdateInstaller.ParseChecksum(text));
    }

    [Fact]
    public void RefusesAHashThatIsOneCharacterTooLong()
    {
        Assert.Null(UpdateInstaller.ParseChecksum(Hash + "a"));
    }

    [Fact]
    public void SkipsAHeaderLineToFindTheHash()
    {
        string text = $"Algorithm : SHA256\n{Hash}  Gaggle-0.4.0-win-x64.zip\n";

        Assert.Equal(Hash, UpdateInstaller.ParseChecksum(text));
    }
}
