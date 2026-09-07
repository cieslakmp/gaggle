namespace Gaggle.Update;

/// <summary>
/// When to look for an update, and whether to say anything about what was found.
///
/// Pure decisions, kept out of the tray context so they can be tested against an injected
/// clock rather than by waiting a day.
/// </summary>
public static class UpdateCheck
{
    /// <summary>
    /// How long a background check waits before running again. Gaggle is launched before
    /// a flight and left running, so anything shorter mostly means checking on every
    /// restart for no benefit.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether a background check should run. A check the user asked for ignores this
    /// entirely — the point of the menu item is to check right now.
    /// </summary>
    public static bool ShouldCheckInBackground(bool enabled, DateTimeOffset? lastCheck, DateTimeOffset now)
    {
        if (!enabled)
        {
            return false;
        }

        if (lastCheck is null)
        {
            return true;
        }

        // A clock that has gone backwards (a fixed system time, a restored machine) would
        // otherwise park the next check arbitrarily far in the future.
        if (lastCheck > now)
        {
            return true;
        }

        return now - lastCheck.Value >= Interval;
    }

    /// <summary>
    /// Whether to offer a release to the user.
    ///
    /// A skipped version stays skipped only for background checks. Someone who clicks
    /// "Check for updates" is asking about this version again, and having no way back
    /// from a mis-click would be worse than the nagging that Skip exists to stop.
    /// </summary>
    public static bool ShouldOffer(string currentVersion, string? candidateVersion, string? skippedVersion, bool silent)
    {
        if (!ReleaseVersion.IsNewer(currentVersion, candidateVersion))
        {
            return false;
        }

        return !silent || !ReleaseVersion.AreSame(candidateVersion, skippedVersion);
    }
}
