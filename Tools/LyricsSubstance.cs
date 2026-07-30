using System.Text.RegularExpressions;

namespace Shuffull.Metadata.Tools;

/// <summary>
/// Decides whether a lyrics payload actually contains lyrics.
///
/// Providers routinely return something rather than nothing: a lone "♪", a bare "[Instrumental]" line, or a
/// couple of words. Stored as-is those look like real lyrics to every consumer - the player opens a lyrics
/// panel for them, and the miss-recheck schedule considers the song solved and stops looking.
///
/// Shared by the producer (which rejects thin results at ingest) and the site (which sweeps ones already
/// stored), so both sides agree on what "has lyrics" means. Two different implementations of this rule would
/// eventually disagree and leave a song that is lyric-less to one and lyric-ful to the other.
///
/// This says nothing about INSTRUMENTAL tracks. "Known to have no lyrics" is a real, useful answer and a
/// different state from "we found almost nothing"; callers keep that flag as-is.
/// </summary>
public static partial class LyricsSubstance
{
    /// <summary>
    /// Default minimum units of real content. Below this the payload is not usable as lyrics.
    ///
    /// Deliberately a unit count and NOT a character count. A character floor tuned for English (~50) silently
    /// deletes real CJK lyrics: Japanese has no spaces and packs far more meaning per character, so a complete
    /// two-line verse can be barely 40 characters. Counting units - a word for spaced scripts, a character for
    /// ideographic ones - is fair to both, and still rejects everything providers actually return as junk
    /// ("♪", "[Instrumental]", "La la la"), which measures 0-3.
    /// </summary>
    public const int DefaultMinimumUnits = 10;

    /// <summary>LRC line timestamps: [mm:ss], [mm:ss.xx], [mm:ss:xx].</summary>
    [GeneratedRegex(@"\[\d+:\d{2}(?:[.:]\d{1,3})?\]")]
    private static partial Regex TimestampRegex();

    /// <summary>LRC metadata tags: [ar:...], [ti:...], [al:...], [by:...], [offset:...], [length:...].</summary>
    [GeneratedRegex(@"\[[a-zA-Z#]+:[^\]]*\]")]
    private static partial Regex MetadataTagRegex();

    /// <summary>
    /// Placeholder glyphs providers use for "no words here": musical notes, and the bracketed markers that
    /// stand in for an instrumental passage. These are not lyrics and must not count toward the totals.
    /// </summary>
    [GeneratedRegex(@"[♪♫♬♩�]|\[\s*(?:instrumental|music|intro|outro|interlude|no\s+lyrics)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>What is left of a payload once the scaffolding is removed.</summary>
    /// <param name="Units">
    /// Units of real content: one per whitespace-separated token in spaced scripts, plus one per CJK character
    /// (which are not space-separated, so tokenising them would count a whole line as a single "word").
    /// </param>
    /// <param name="Characters">Characters of real text, excluding whitespace. Diagnostic only.</param>
    public readonly record struct LyricsMeasurement(int Units, int Characters)
    {
        public static readonly LyricsMeasurement Empty = new(0, 0);
    }

    /// <summary>
    /// CJK ideographs, hiragana and katakana - scripts written without spaces, where a character is roughly a
    /// word's worth of content. Hangul syllables are included for the same reason.
    /// </summary>
    private static bool IsIdeographic(char c) =>
        (c >= '一' && c <= '鿿') ||   // CJK unified ideographs
        (c >= '㐀' && c <= '䶿') ||   // CJK extension A
        (c >= '぀' && c <= 'ゟ') ||   // hiragana
        (c >= '゠' && c <= 'ヿ') ||   // katakana
        (c >= '가' && c <= '힯');     // hangul syllables

    /// <summary>
    /// Measures the real content of a plain or LRC payload. Timestamps, metadata tags and placeholder glyphs
    /// are removed first - counting raw characters would let a 12-line LRC of nothing but "[00:12.34] ♪" clear
    /// any character threshold on punctuation alone.
    /// </summary>
    public static LyricsMeasurement Measure(string? lyrics)
    {
        if (string.IsNullOrWhiteSpace(lyrics))
        {
            return LyricsMeasurement.Empty;
        }

        var stripped = MetadataTagRegex().Replace(lyrics, " ");
        stripped = TimestampRegex().Replace(stripped, " ");
        stripped = PlaceholderRegex().Replace(stripped, " ");
        stripped = WhitespaceRegex().Replace(stripped, " ").Trim();

        if (stripped.Length == 0)
        {
            return LyricsMeasurement.Empty;
        }

        // Ideographic characters are counted individually and removed before tokenising, so a spaceless
        // Japanese line contributes its real weight instead of registering as one "word".
        var ideographic = stripped.Count(IsIdeographic);
        var spaced = new string(stripped.Select(c => IsIdeographic(c) ? ' ' : c).ToArray());
        var tokens = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Count(t => t.Any(char.IsLetterOrDigit));

        var characters = stripped.Count(c => !char.IsWhiteSpace(c));
        return new LyricsMeasurement(ideographic + tokens, characters);
    }

    /// <summary>True when the payload is too thin to be worth keeping.</summary>
    public static bool IsTooThin(string? lyrics, int minimumUnits = DefaultMinimumUnits)
        => Measure(lyrics).Units < minimumUnits;

    /// <summary>
    /// True when NEITHER payload carries usable lyrics. Songs can hold a synced and a plain form; the pair is
    /// only worth keeping if at least one of them is substantial, so this is what callers gate on.
    /// </summary>
    public static bool IsTooThin(string? syncedLyrics, string? plainLyrics, int minimumUnits = DefaultMinimumUnits)
        => IsTooThin(syncedLyrics, minimumUnits) && IsTooThin(plainLyrics, minimumUnits);
}
