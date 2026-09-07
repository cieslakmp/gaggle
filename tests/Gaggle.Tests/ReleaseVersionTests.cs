using Gaggle.Update;

namespace Gaggle.Tests;

/// <summary>
/// Release tags are compared as numbers, never as strings.
///
/// Compared as text, "0.10.0" sorts below "0.9.0", so the tenth release of a series would
/// never be offered to anyone and the updater would look like it had simply stopped
/// working. That case is the reason this type exists.
/// </summary>
public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.4.0", "0.4.0")]
    [InlineData("0.4.0", "0.4.0")]
    [InlineData("V0.4.0", "0.4.0")]
    [InlineData("  v0.4.0  ", "0.4.0")]
    [InlineData("0.4", "0.4.0")]
    [InlineData("v1.2.3-rc1", "1.2.3")]
    [InlineData("v1.2.3+abc123", "1.2.3")]
    public void ParsesTheShapesATagCanTake(string tag, string expected)
    {
        Assert.True(ReleaseVersion.TryParse(tag, out Version? version));
        Assert.Equal(expected, version!.ToString(3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("latest")]
    [InlineData("v.1.2")]
    public void RefusesAnythingThatIsNotAVersion(string? tag)
    {
        Assert.False(ReleaseVersion.TryParse(tag, out _));
    }

    [Fact]
    public void TreatsTenAsLaterThanNine()
    {
        // The whole point of the file: "0.10.0" < "0.9.0" as a string.
        Assert.True(ReleaseVersion.IsNewer("0.9.0", "0.10.0"));
        Assert.False(ReleaseVersion.IsNewer("0.10.0", "0.9.0"));
    }

    [Theory]
    [InlineData("0.3.1", "0.3.2")]
    [InlineData("0.3.1", "0.4.0")]
    [InlineData("0.3.1", "1.0.0")]
    [InlineData("0.3.1", "v0.3.2")]
    public void OffersALaterRelease(string current, string candidate)
    {
        Assert.True(ReleaseVersion.IsNewer(current, candidate));
    }

    [Theory]
    [InlineData("0.3.1", "0.3.1")]
    [InlineData("0.3.1", "v0.3.1")]
    [InlineData("0.3.1", "0.3.0")]
    [InlineData("1.0.0", "0.9.9")]
    public void DoesNotOfferTheSameOrAnOlderRelease(string current, string candidate)
    {
        Assert.False(ReleaseVersion.IsNewer(current, candidate));
    }

    [Theory]
    [InlineData("0.3.1", null)]
    [InlineData("0.3.1", "")]
    [InlineData("0.3.1", "not-a-version")]
    [InlineData("unknown", "0.4.0")]
    public void NeverOffersAnUpdateOnUnreadableInput(string? current, string? candidate)
    {
        // A malformed tag on a release must not be able to talk a running app into
        // replacing itself.
        Assert.False(ReleaseVersion.IsNewer(current, candidate));
    }

    [Fact]
    public void ComparesTheSameVersionWrittenTwoWaysAsEqual()
    {
        // Skipping "0.4.0" has to keep suppressing a tag written "v0.4".
        Assert.True(ReleaseVersion.AreSame("0.4.0", "v0.4"));
        Assert.False(ReleaseVersion.AreSame("0.4.0", "0.4.1"));
        Assert.False(ReleaseVersion.AreSame("0.4.0", null));
    }
}
