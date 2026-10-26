using System.Globalization;
using System.Text;
using Shuffull.Metadata.Tools;

namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// Renders measured audio-shape features into the self-describing block the other-details (heaviness) prompt
/// consumes. Lives in the shared engine so the producer (at export) and Shuffull (at re-tag, from the values
/// persisted on the Song) build the same context. The <see cref="SpectralTexture"/> lines are the exception: the site
/// doesn't store it, so only the producer, which has the audio, sends them. The descriptions ride with the data - not
/// the system prompt - so callers can add/drop signals without changing the engine. Returns null when nothing is
/// measured.
/// </summary>
public static class AudioFeaturesPromptBlock
{
    public static string? Format(double? loudnessRangeLu, double? crestFactorDb, double? onsetsPerSecond, SpectralTexture? texture = null)
    {
        if (loudnessRangeLu is null && crestFactorDb is null && onsetsPerSecond is null && texture is null)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Objective audio analysis (measured from the actual audio - use these to ground the heaviness estimate):");

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

        // Grit and density are what tell heavy music from J-pop and rock; the three lines above barely do. They go in
        // as percentiles of a reference library because a raw flatness of 0.69 means nothing to the model. This text
        // took a blind test (name and artist hidden) from 0% to 78% of heavy songs at 8+ (Shuffull.Metadata#8). The
        // folder ranges quoted come from AudioFeaturesCalibration's reference set; re-test any edit.
        if (texture is not null)
        {
            sb.AppendLine($"- Mid-range grit: noisier than {AudioFeaturesCalibration.MidGritPercentile(texture.MidGrit)}% of a mixed reference library (noise-like energy at 250-2000 Hz in the loud parts: distorted guitars, saturated synths and dense hard-hitting production score high; clean vocals and clean or acoustic instruments score low. In that library, heavy music (metal, hardcore, loud dense EDM) mostly sits at 70-95, pop, J-pop and rock mostly at 35-70, calm music below 20)");
            sb.AppendLine($"- Upper-mid grit: noisier than {AudioFeaturesCalibration.UpperGritPercentile(texture.UpperGrit)}% of the same library (the same measure at 2000-6000 Hz)");
            sb.AppendLine($"- Spectral density: denser than {AudioFeaturesCalibration.DensityPercentile(texture.SpectralContrastDb)}% of the same library (how completely the sound fills the spectrum instead of leaving clear peaks; heavy music sits high, sparse or clean arrangements low)");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Num(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
