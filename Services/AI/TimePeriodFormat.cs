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
}
