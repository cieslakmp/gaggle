using Gaggle.Update;

namespace Gaggle.Tests;

/// <summary>
/// When Gaggle checks, and when it speaks up.
///
/// The rules are small but easy to get subtly wrong in ways nobody notices for a day at a
/// time: a throttle that never expires means updates are never found, and a Skip that also
/// silences the menu item means a mis-click can never be undone.
/// </summary>
public class UpdateCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ChecksOnceWhenItHasNeverChecked()
    {
        Assert.True(UpdateCheck.ShouldCheckInBackground(enabled: true, lastCheck: null, Now));
    }

    [Fact]
    public void WaitsADayBetweenBackgroundChecks()
    {
        Assert.False(UpdateCheck.ShouldCheckInBackground(true, Now - TimeSpan.FromHours(23), Now));
        Assert.True(UpdateCheck.ShouldCheckInBackground(true, Now - TimeSpan.FromHours(25), Now));
    }

    [Fact]
    public void ChecksAgainWhenTheClockHasGoneBackwards()
    {
        // A machine with a wrong clock, corrected, would otherwise park the next check
        // arbitrarily far in the future.
        Assert.True(UpdateCheck.ShouldCheckInBackground(true, Now + TimeSpan.FromDays(400), Now));
    }

    [Fact]
    public void NeverTouchesTheNetworkWhenCheckingIsOff()
    {
        Assert.False(UpdateCheck.ShouldCheckInBackground(enabled: false, lastCheck: null, Now));
        Assert.False(UpdateCheck.ShouldCheckInBackground(false, Now - TimeSpan.FromDays(30), Now));
    }

    [Fact]
    public void OffersOnlyALaterRelease()
    {
        Assert.True(UpdateCheck.ShouldOffer("0.3.1", "0.4.0", skippedVersion: null, silent: true));
        Assert.False(UpdateCheck.ShouldOffer("0.3.1", "0.3.1", null, silent: true));
        Assert.False(UpdateCheck.ShouldOffer("0.3.1", "0.3.0", null, silent: true));
    }

    [Fact]
    public void StaysQuietAboutASkippedVersionInTheBackground()
    {
        Assert.False(UpdateCheck.ShouldOffer("0.3.1", "0.4.0", skippedVersion: "0.4.0", silent: true));
    }

    [Fact]
    public void OffersASkippedVersionAgainWhenAskedDirectly()
    {
        // Skip exists to stop nagging, not to make a version unreachable.
        Assert.True(UpdateCheck.ShouldOffer("0.3.1", "0.4.0", skippedVersion: "0.4.0", silent: false));
    }

    [Fact]
    public void StopsSkippingOnceANewerVersionArrives()
    {
        Assert.True(UpdateCheck.ShouldOffer("0.3.1", "0.4.1", skippedVersion: "0.4.0", silent: true));
    }

    [Fact]
    public void TreatsASkippedVersionWrittenAsATagAsTheSameVersion()
    {
        Assert.False(UpdateCheck.ShouldOffer("0.3.1", "0.4.0", skippedVersion: "v0.4.0", silent: true));
    }
}
