using System.Diagnostics.CodeAnalysis;

namespace Gaggle.Update;

/// <summary>
/// Compares the running version against a release tag.
///
/// This exists as its own file because the obvious implementation is wrong: compared as
/// strings, "0.10.0" sorts below "0.9.0", so the tenth release of a series would never be
/// offered to anyone. Everything here goes through <see cref="Version"/>.
/// </summary>
public static class ReleaseVersion
{
    /// <summary>Where a pre-release or build suffix starts.</summary>
    private static readonly char[] SuffixSeparators = ['-', '+'];

    /// <summary>
    /// Parses "v0.4.0", "0.4.0" or "0.4" into a <see cref="Version"/>. Any pre-release or
    /// build suffix ("-rc1", "+abc123") is cut off first, because <see cref="Version"/>
    /// cannot parse it and the numeric part is what orders releases.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out Version? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();

        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        int suffix = trimmed.IndexOfAny(SuffixSeparators);
        if (suffix >= 0)
        {
            trimmed = trimmed[..suffix];
        }

        if (!Version.TryParse(trimmed, out Version? parsed))
        {
            return false;
        }

        // Normalised to exactly three parts. Version leaves anything the text omitted at
        // -1, which makes 0.4 and 0.4.0 compare unequal and makes ToString(3) throw, and
        // every version this project ships is three parts by construction: the release
        // workflow refuses a tag that disagrees with <Version> in the csproj.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));

        return true;
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is a later release than
    /// <paramref name="current"/>.
    ///
    /// Anything unparseable on either side answers false. A malformed tag on a release
    /// must never be able to talk a running app into replacing itself.
    /// </summary>
    public static bool IsNewer(string? current, string? candidate)
    {
        if (!TryParse(current, out Version? running) || !TryParse(candidate, out Version? offered))
        {
            return false;
        }

        return offered > running;
    }

    /// <summary>Version numbers as written, so "0.4" and "0.4.0" compare equal.</summary>
    public static bool AreSame(string? left, string? right)
    {
        if (!TryParse(left, out Version? a) || !TryParse(right, out Version? b))
        {
            return false;
        }

        return a == b;
    }
}
