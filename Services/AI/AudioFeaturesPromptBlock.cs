using System.Globalization;
using System.Text;

namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// Renders measured audio-shape features into the self-describing block the other-details (energy) prompt
/// consumes. Lives in the shared engine so the producer (at export) and Shuffull (at re-tag, from the values
/// persisted on the Song) build byte-identical context. The descriptions ride with the data - not the system
/// prompt - so callers can add/drop signals without changing the engine. Returns null when nothing is measured.
/// </summary>
public static class AudioFeaturesPromptBlock
{
    public static string? Format(double? loudnessRangeLu, double? crestFactorDb, double? onsetsPerSecond)
    {
        if (loudnessRangeLu is null && crestFactorDb is null && onsetsPerSecond is null)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Objective audio analysis (measured from the actual audio - weigh these heavily for the energy estimate):");

        if (loudnessRangeLu is double lra)
        {
            sb.AppendLine($"- Loudness range (LRA): {Num(lra)} LU (lower = more consistently loud / compressed, typical of high-energy electronic & metal; higher = more dynamic, with quiet and loud sections)");
        }

        if (crestFactorDb is double crest)
        {
            sb.AppendLine($"- Crest factor: {Num(crest)} dB (lower = more heavily compressed / 'slammed' / aggressive; higher = more dynamic / acoustic / gentle)");
        }

        if (onsetsPerSecond is double onsets)
        {
            sb.AppendLine($"- Onset density: {Num(onsets)} onsets/sec (higher = busier, more percussive / rhythmic activity; lower = sparse / sustained)");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Num(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
