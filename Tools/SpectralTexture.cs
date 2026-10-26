namespace Shuffull.Metadata.Tools;

/// <summary>
/// How gritty and dense a song's spectrum is, measured from mono samples at <see cref="SampleRate"/>. These are what
/// separate heavy music from J-pop and rock: on the user's library they scored AUC ~0.9 heavy-vs-J-pop, where the
/// loudness-shape numbers (LRA, crest, onsets) scored 0.6 (Shuffull.Metadata#8). A re-encode at 128k barely moves
/// them, so they measure the music, not the file.
/// </summary>
/// <param name="MidGrit">Mean spectral flatness at 250-2000 Hz over the loudest 30% of frames, 0-1. Noise-like
/// energy (distorted guitars, saturated synths, dense production) is high; clean tones are low.</param>
/// <param name="UpperGrit">The same at 2000-6000 Hz.</param>
/// <param name="SpectralContrastDb">Mean peak-to-valley spectral contrast over six octave bands (librosa's
/// definition), in dB. LOWER means the sound fills the spectrum more completely.</param>
public sealed record SpectralTexture(double MidGrit, double UpperGrit, double SpectralContrastDb)
{
    public const int SampleRate = 22050;

    /// <summary>Decode at most this much of the song; the texture is set well before the end.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(6);

    private const int FrameSize = 2048;
    private const int Hop = 512;
    private const float SilenceThreshold = 0.0031622776f; // -50 dBFS: leading/trailing silence is trimmed
    private const double LoudFramePercentile = 70;

    /// <summary>
    /// Measures <paramref name="samples"/>, mono at <see cref="SampleRate"/>. Null when there is less than a
    /// few seconds of sound to measure.
    /// </summary>
    /// <remarks>
    /// <see cref="AudioFeaturesCalibration"/> holds the reference distribution these values are ranked against. It
    /// was measured with THIS code, so any change to the maths here needs a re-calibration there.
    /// </remarks>
    public static SpectralTexture? Measure(ReadOnlySpan<float> samples)
    {
        var start = 0;
        while (start < samples.Length && Math.Abs(samples[start]) <= SilenceThreshold) start++;
        var end = samples.Length - 1;
        while (end > start && Math.Abs(samples[end]) <= SilenceThreshold) end--;
        // Matches the reference measurement, which kept y[first:last] (the last loud sample excluded).
        var trimmed = samples[start..Math.Max(start, end)];
        if (trimmed.Length < SampleRate * 5)
        {
            return null;
        }

        // One pass, keeping only a few numbers per frame: holding the whole spectrogram of a six-minute song would
        // cost ~60 MB, on a box with no swap.
        var binHz = (double)SampleRate / FrameSize;
        var mid = BinRange(250, 2000, binHz);
        var upper = BinRange(2000, 6000, binHz);
        var contrastBands = ContrastBands(binHz);

        var padded = new float[trimmed.Length + FrameSize]; // librosa's center=True: half a frame of zeros each side
        trimmed.CopyTo(padded.AsSpan(FrameSize / 2));
        var frames = 1 + (padded.Length - FrameSize) / Hop;
        var window = new double[FrameSize];
        for (var n = 0; n < FrameSize; n++) window[n] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / FrameSize); // periodic Hann

        var power = new double[frames];
        var midFlatness = new double[frames];
        var upperFlatness = new double[frames];
        var peaks = new double[contrastBands.Length, frames];
        var valleys = new double[contrastBands.Length, frames];

        var fft = new Fft(FrameSize);
        var re = new double[FrameSize];
        var im = new double[FrameSize];
        var magnitude = new double[FrameSize / 2 + 1];
        var scratch = new double[magnitude.Length];
        for (var t = 0; t < frames; t++)
        {
            var offset = t * Hop;
            for (var n = 0; n < FrameSize; n++)
            {
                re[n] = padded[offset + n] * window[n];
                im[n] = 0;
            }

            fft.Transform(re, im);
            double sum = 0;
            for (var k = 0; k < magnitude.Length; k++)
            {
                // Rounded through float, as the reference measurement's float32 spectrogram is.
                magnitude[k] = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                sum += magnitude[k] * magnitude[k];
            }

            power[t] = sum;
            midFlatness[t] = Flatness(magnitude, mid);
            upperFlatness[t] = Flatness(magnitude, upper);
            for (var band = 0; band < contrastBands.Length; band++)
            {
                (valleys[band, t], peaks[band, t]) = ValleyAndPeak(magnitude, contrastBands[band], scratch);
            }
        }

        var loudThreshold = Percentile(power, LoudFramePercentile);
        double midSum = 0, upperSum = 0;
        var loudFrames = 0;
        for (var t = 0; t < frames; t++)
        {
            if (power[t] > loudThreshold)
            {
                midSum += midFlatness[t];
                upperSum += upperFlatness[t];
                loudFrames++;
            }
        }

        if (loudFrames == 0)
        {
            return null;
        }

        return new SpectralTexture(midSum / loudFrames, upperSum / loudFrames, MeanContrast(peaks, valleys));
    }

    /// <summary>The bins at or above <paramref name="lowHz"/> and below <paramref name="highHz"/>, end exclusive.</summary>
    private static (int From, int To) BinRange(double lowHz, double highHz, double binHz)
        => ((int)Math.Ceiling(lowHz / binHz), Math.Min(FrameSize / 2 + 1, (int)Math.Ceiling(highHz / binHz)));

    /// <summary>Geometric over arithmetic mean of the band's magnitudes in one frame.</summary>
    private static double Flatness(double[] magnitude, (int From, int To) band)
    {
        double logSum = 0, sum = 0;
        for (var k = band.From; k < band.To; k++)
        {
            var v = magnitude[k] + 1e-10;
            logSum += Math.Log(v);
            sum += v;
        }

        var width = band.To - band.From;
        return Math.Exp(logSum / width) / (sum / width);
    }

    /// <summary>One spectral-contrast band: the bins it reads, and how many of the lowest and highest it averages.</summary>
    private sealed record ContrastBand(int[] Bins, int Alpha);

    /// <summary>
    /// librosa.feature.spectral_contrast's bands with its defaults: fmin 200 Hz, six octaves plus the remainder,
    /// quantile 0.02, each band borrowing the bin below it and (but for the last) dropping its top bin.
    /// </summary>
    private static ContrastBand[] ContrastBands(double binHz)
    {
        const int octaves = 6;
        const double fMin = 200;
        const double quantile = 0.02;
        var bins = FrameSize / 2 + 1;
        var edges = new double[octaves + 2];
        for (var i = 1; i < edges.Length; i++) edges[i] = fMin * Math.Pow(2, i - 1);

        var result = new ContrastBand[octaves + 1];
        for (var k = 0; k <= octaves; k++)
        {
            var inBand = new bool[bins];
            int first = -1, last = -1;
            for (var b = 0; b < bins; b++)
            {
                if (b * binHz >= edges[k] && b * binHz <= edges[k + 1])
                {
                    inBand[b] = true;
                    if (first < 0) first = b;
                    last = b;
                }
            }

            if (k > 0 && first > 0) inBand[first - 1] = true;
            if (k == octaves) for (var b = last + 1; b < bins; b++) inBand[b] = true;

            var rows = Enumerable.Range(0, bins).Where(b => inBand[b]).ToList();
            var alpha = (int)Math.Max(1, Math.Round(quantile * rows.Count, MidpointRounding.ToEven));
            if (k < octaves) rows.RemoveAt(rows.Count - 1);
            result[k] = new ContrastBand([.. rows], alpha);
        }

        return result;
    }

    private static (double Valley, double Peak) ValleyAndPeak(double[] magnitude, ContrastBand band, double[] scratch)
    {
        var count = band.Bins.Length;
        for (var i = 0; i < count; i++) scratch[i] = magnitude[band.Bins[i]];
        Array.Sort(scratch, 0, count);
        double low = 0, high = 0;
        for (var i = 0; i < band.Alpha; i++)
        {
            low += scratch[i];
            high += scratch[count - 1 - i];
        }

        return (low / band.Alpha, high / band.Alpha);
    }

    /// <summary>Peak minus valley in dB (librosa's power_to_db, top_db 80 over each whole matrix), averaged.</summary>
    private static double MeanContrast(double[,] peaks, double[,] valleys)
    {
        ToDecibels(peaks);
        ToDecibels(valleys);
        double total = 0;
        for (var k = 0; k < peaks.GetLength(0); k++)
        for (var t = 0; t < peaks.GetLength(1); t++)
            total += peaks[k, t] - valleys[k, t];
        return total / peaks.Length;
    }

    /// <summary>librosa.power_to_db(x, ref=1, amin=1e-10, top_db=80), in place.</summary>
    private static void ToDecibels(double[,] values)
    {
        var max = double.NegativeInfinity;
        for (var i = 0; i < values.GetLength(0); i++)
        for (var j = 0; j < values.GetLength(1); j++)
        {
            values[i, j] = 10 * Math.Log10(Math.Max(1e-10, values[i, j]));
            max = Math.Max(max, values[i, j]);
        }

        for (var i = 0; i < values.GetLength(0); i++)
        for (var j = 0; j < values.GetLength(1); j++)
            values[i, j] = Math.Max(values[i, j], max - 80);
    }

    /// <summary>numpy.percentile with linear interpolation.</summary>
    private static double Percentile(double[] values, double percentile)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var position = percentile / 100 * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    /// <summary>In-place iterative radix-2 FFT for one fixed power-of-two size.</summary>
    private sealed class Fft
    {
        private readonly int _size;
        private readonly int[] _reversed;
        private readonly double[] _cos;
        private readonly double[] _sin;

        public Fft(int size)
        {
            _size = size;
            var bits = (int)Math.Log2(size);
            _reversed = new int[size];
            for (var i = 0; i < size; i++)
            {
                var r = 0;
                for (var b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b);
                _reversed[i] = r;
            }

            _cos = new double[size / 2];
            _sin = new double[size / 2];
            for (var i = 0; i < size / 2; i++)
            {
                _cos[i] = Math.Cos(-2 * Math.PI * i / size);
                _sin[i] = Math.Sin(-2 * Math.PI * i / size);
            }
        }

        public void Transform(double[] re, double[] im)
        {
            for (var i = 0; i < _size; i++)
            {
                var j = _reversed[i];
                if (j > i)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }

            for (var length = 2; length <= _size; length <<= 1)
            {
                var half = length / 2;
                var step = _size / length;
                for (var start = 0; start < _size; start += length)
                for (var k = 0; k < half; k++)
                {
                    var wr = _cos[k * step];
                    var wi = _sin[k * step];
                    var a = start + k;
                    var b = a + half;
                    var tr = re[b] * wr - im[b] * wi;
                    var ti = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                }
            }
        }
    }
}
