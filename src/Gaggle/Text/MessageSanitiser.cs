using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Gaggle.Text;

/// <summary>
/// Turns raw Whisper output into something safe to type into a game chat prompt.
///
/// Three problems to solve. Whisper emits smart quotes and dashes that most keyboard
/// layouts cannot type. It emits newlines, which would submit the message early
/// because Enter is the send key. And on near-silent audio it hallucinates a small
/// set of stock phrases from its training data.
/// </summary>
public static partial class MessageSanitiser
{
    /// <summary>
    /// Phrases Whisper produces from silence or noise. Compared against the whole
    /// sanitised message, never as a substring, so a genuine "thank you" still sends.
    /// </summary>
    private static readonly HashSet<string> Hallucinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "thank you.",
        "thank you",
        "thanks for watching!",
        "thanks for watching.",
        "you",
        "bye.",
        "bye",
        ".",
        "[blank_audio]",
        "(upbeat music)",
        "[music]",
    };

    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['‘'] = "'",
        ['’'] = "'",
        ['“'] = "\"",
        ['”'] = "\"",
        ['–'] = "-",
        ['—'] = "-",
        ['…'] = "...",
        [' '] = " ",
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"^\s*[\[\(][^\]\)]*[\]\)]\s*")]
    private static partial Regex LeadingAnnotation();

    /// <summary>
    /// Cleans a transcript. Returns null when nothing worth sending survives —
    /// empty audio, an annotation-only result, or a known hallucination.
    /// </summary>
    public static string? Clean(string? raw, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string text = raw;

        // Whisper brackets non-speech as [BLANK_AUDIO], (wind blowing) and similar.
        while (LeadingAnnotation().IsMatch(text))
        {
            text = LeadingAnnotation().Replace(text, string.Empty, 1);
        }

        var builder = new StringBuilder(text.Length);

        foreach (char ch in text)
        {
            if (Replacements.TryGetValue(ch, out string? replacement))
            {
                builder.Append(replacement);
                continue;
            }

            // Drop control characters (newlines included) rather than typing them:
            // a newline mid-message would submit it half-finished.
            if (char.IsControl(ch))
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(ch);
        }

        text = WhitespaceRun().Replace(builder.ToString(), " ").Trim();

        if (text.Length == 0 || Hallucinations.Contains(text))
        {
            return null;
        }

        // A result that is only punctuation carries no message.
        if (text.All(ch => char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsWhiteSpace(ch)))
        {
            return null;
        }

        return Truncate(text, maxLength);
    }

    /// <summary>Trims to the length limit, preferring a word boundary.</summary>
    private static string Truncate(string text, int maxLength)
    {
        if (maxLength <= 0 || text.Length <= maxLength)
        {
            return text;
        }

        string clipped = text[..maxLength];
        int lastSpace = clipped.LastIndexOf(' ');

        // Only fall back to the word boundary if it does not throw most of the
        // message away.
        if (lastSpace > maxLength / 2)
        {
            clipped = clipped[..lastSpace];
        }

        return clipped.TrimEnd();
    }

    /// <summary>
    /// True if every character can be typed on the given layout. The tray uses this
    /// to warn rather than silently dropping characters.
    /// </summary>
    public static bool IsTypable(string text, IntPtr layout)
    {
        foreach (char ch in text)
        {
            if (Interop.NativeMethods.VkKeyScanEx(ch, layout) == -1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Best-effort fold of accented characters to ASCII for stubborn layouts.</summary>
    public static string FoldToAscii(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
