using System.Text.RegularExpressions;

namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// Normalizes the model's free-text time-period output to a single decade/century token (e.g. "2010s",
/// "1800s"), salvaging one embedded in prose (e.g. "2020s (cannot verify)" -> "2020s"). Returns null when no
/// valid token is present. The era is REQUIRED: the caller treats a null here as a generation failure to be
/// regenerated, so an empty/garbage time period never reaches the hand-off contract (where it would otherwise
/// become an empty-named tag on import).
/// </summary>
public static class TimePeriodFormat
{
    private static readonly Regex DecadeOrCentury = new(@"\b\d{3,4}0s\b", RegexOptions.Compiled);

    /// <summary>The normalized decade/century token, or null when the input contains none.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = DecadeOrCentury.Match(raw);
        return match.Success ? match.Value : null;
    }

    /// <summary>True when <paramref name="raw"/> yields a usable decade/century token.</summary>
    public static bool IsValid(string? raw) => Normalize(raw) is not null;

    /// <summary>
    /// Converts an authoritative release year into the era token: a decade for 1900+ ("1985" -> "1980s",
    /// "2017" -> "2010s") and a century before that ("1850" -> "1800s", "1723" -> "1700s"). Matches the
    /// engine's output format and the <see cref="Normalize"/> guard. Shared by the producer's export-time
    /// MusicBrainz override and the site's re-tag (which re-applies the stored year instead of re-guessing).
    /// </summary>
    public static string FromYear(int year)
    {
        if (year >= 1900)
        {
            var decade = year / 10 * 10;
            return $"{decade}s";
        }

        var century = year / 100 * 100;
        return $"{century}s";
    }
}
