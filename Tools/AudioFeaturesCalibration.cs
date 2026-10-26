namespace Shuffull.Metadata.Tools;

/// <summary>
/// Where a song's <see cref="SpectralTexture"/> sits in a mixed reference library, as a percentile, so the prompt can say
/// "grittier than 86% of songs" instead of a raw number the model has no feel for.
/// </summary>
/// <remarks>
/// The reference is 1,200 songs from the user's own library, 300 each from the heavy (Loud Bois), rock, Japanese and
/// calm (Relaxation) folders, measured with <see cref="SpectralTexture.Measure"/> on 2026-10-26. Each table holds the
/// 0th to 100th percentile. Re-measure them whenever the maths in <see cref="SpectralTexture"/> changes, and re-check
/// the folder ranges quoted in <c>AudioFeaturesPromptBlock</c>.
/// </remarks>
public static class AudioFeaturesCalibration
{
    /// <summary>Percent of the reference library with less mid-range grit than <paramref name="midGrit"/>, 0-100.</summary>
    public static int MidGritPercentile(double midGrit) => Percentile(MidGrit, midGrit);

    /// <summary>Percent of the reference library with less upper-mid grit than <paramref name="upperGrit"/>, 0-100.</summary>
    public static int UpperGritPercentile(double upperGrit) => Percentile(UpperGrit, upperGrit);

    /// <summary>
    /// Percent of the reference library that is less dense than this song, 0-100. Density is the inverse of spectral
    /// contrast, so a LOW contrast gives a HIGH percentile. NaN gives 0, the neutral end, as it does for grit.
    /// </summary>
    public static int DensityPercentile(double spectralContrastDb)
        => double.IsNaN(spectralContrastDb) ? 0 : 100 - Percentile(SpectralContrastDb, spectralContrastDb);

    /// <summary>Linear interpolation between the table's percentile points, clamped to 0-100.</summary>
    private static int Percentile(double[] table, double value)
    {
        if (double.IsNaN(value) || value <= table[0])
        {
            return 0;
        }

        if (value >= table[^1])
        {
            return 100;
        }

        var upper = 1;
        while (table[upper] < value)
        {
            upper++;
        }

        var lower = upper - 1;
        var span = table[upper] - table[lower];
        var fraction = span > 0 ? (value - table[lower]) / span : 0;
        return (int)Math.Round(lower + fraction, MidpointRounding.AwayFromZero);
    }

    private static readonly double[] MidGrit =
    [
        0.04663, 0.06635, 0.07304, 0.07763, 0.08452, 0.08987, 0.09433, 0.09755, 0.10123, 0.10419,
        0.10825, 0.11531, 0.12071, 0.12734, 0.13364, 0.14031, 0.14721, 0.15225, 0.15729, 0.17030,
        0.18074, 0.19060, 0.20026, 0.22331, 0.24682, 0.29970, 0.42020, 0.45834, 0.47759, 0.49993,
        0.50845, 0.51898, 0.52493, 0.53704, 0.54268, 0.55005, 0.55493, 0.55973, 0.56283, 0.56544,
        0.57058, 0.57585, 0.57915, 0.58287, 0.58828, 0.59456, 0.59680, 0.59887, 0.60139, 0.60431,
        0.60670, 0.60870, 0.61140, 0.61489, 0.61679, 0.61835, 0.62097, 0.62302, 0.62580, 0.62894,
        0.63130, 0.63399, 0.63717, 0.63899, 0.64142, 0.64551, 0.64818, 0.65139, 0.65343, 0.65622,
        0.65998, 0.66349, 0.66669, 0.67025, 0.67448, 0.67744, 0.67896, 0.68159, 0.68452, 0.68880,
        0.69018, 0.69182, 0.69535, 0.69710, 0.70069, 0.70241, 0.70554, 0.70886, 0.71306, 0.71704,
        0.72005, 0.72389, 0.72733, 0.73290, 0.73682, 0.73991, 0.74845, 0.75100, 0.75860, 0.77207,
        0.80189,
    ];

    private static readonly double[] UpperGrit =
    [
        0.12186, 0.25168, 0.29682, 0.34433, 0.38562, 0.41907, 0.46615, 0.48398, 0.51759, 0.53634,
        0.54333, 0.56749, 0.58207, 0.59116, 0.59992, 0.61403, 0.62214, 0.62731, 0.63585, 0.64157,
        0.64486, 0.64960, 0.65447, 0.65729, 0.66132, 0.66568, 0.66844, 0.67271, 0.67533, 0.67901,
        0.68171, 0.68562, 0.68754, 0.68946, 0.69151, 0.69409, 0.69667, 0.69759, 0.69957, 0.70140,
        0.70414, 0.70623, 0.70747, 0.70920, 0.71094, 0.71334, 0.71634, 0.71718, 0.71918, 0.72119,
        0.72298, 0.72488, 0.72659, 0.72780, 0.72962, 0.73098, 0.73294, 0.73454, 0.73620, 0.73878,
        0.74011, 0.74230, 0.74367, 0.74437, 0.74613, 0.74820, 0.74977, 0.75080, 0.75214, 0.75424,
        0.75617, 0.75763, 0.75967, 0.76149, 0.76299, 0.76455, 0.76674, 0.76811, 0.76916, 0.77139,
        0.77345, 0.77492, 0.77692, 0.77854, 0.78115, 0.78437, 0.78647, 0.78807, 0.79016, 0.79271,
        0.79379, 0.79582, 0.79726, 0.79904, 0.80065, 0.80180, 0.80342, 0.80553, 0.80885, 0.81125,
        0.82249,
    ];

    private static readonly double[] SpectralContrastDb =
    [
        14.3617, 15.3707, 15.5213, 15.6312, 15.7474, 15.8994, 15.9414, 16.0085, 16.1102, 16.1996,
        16.2943, 16.3609, 16.4511, 16.4940, 16.5625, 16.6930, 16.7466, 16.8359, 16.8884, 16.9559,
        16.9934, 17.0438, 17.1103, 17.1745, 17.2411, 17.2994, 17.3605, 17.4221, 17.4495, 17.4953,
        17.5307, 17.5729, 17.6195, 17.6699, 17.7056, 17.7351, 17.7583, 17.8191, 17.8658, 17.8883,
        17.9134, 17.9459, 17.9780, 18.0280, 18.0664, 18.1123, 18.1459, 18.1920, 18.2423, 18.2813,
        18.3062, 18.3386, 18.3700, 18.4288, 18.4777, 18.5413, 18.6006, 18.6716, 18.7220, 18.7638,
        18.8089, 18.8646, 18.9307, 18.9674, 19.0428, 19.1335, 19.2011, 19.2916, 19.3652, 19.4768,
        19.5849, 19.6728, 19.8740, 19.9707, 20.2381, 20.3516, 20.5602, 20.6978, 21.0532, 21.3002,
        21.4882, 21.8116, 21.9380, 22.0531, 22.2561, 22.4035, 22.5806, 22.9119, 23.0347, 23.2150,
        23.4175, 23.5777, 23.7686, 24.1062, 24.2399, 24.6208, 24.9173, 25.3066, 25.6347, 26.1389,
        27.2101,
    ];
}
