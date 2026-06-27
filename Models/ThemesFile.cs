using Newtonsoft.Json;

namespace Shuffull.Metadata.Models;

/// <summary>
/// The canonical master theme vocabulary that both Shuffull and the producer infer against, mirroring
/// <see cref="MoodsFile"/>. Themes are cross-cutting origin/relationship labels (Anime, Vocaloid, Cover,
/// Parody, Christmas …) — NOT sonic genres, eras, languages, or moods. Unlike those, a theme is OPTIONAL and
/// sparse: most songs have none. The AI picks 0-2 of these per song only when clearly warranted; anything
/// off-list is dropped (membership enforced post-parse, exactly like moods/sub-genres). The list is embedded
/// in this assembly (see Shuffull.Metadata.csproj) so both sides reference the same source and can't drift.
/// </summary>
[Serializable]
public class ThemesFile
{
    /// <summary>
    /// LogicalName of the embedded canonical themes list (see Shuffull.Metadata.csproj).
    /// </summary>
    private const string CanonicalResourceName = "themes.json";

    public List<string> Themes { get; set; } = [];

    /// <summary>
    /// The canonical master theme list, embedded in this assembly, that both Shuffull and the producer
    /// must infer against. This is the single shared source of the allowed theme vocabulary.
    /// </summary>
    public static string CanonicalJson
    {
        get
        {
            var assembly = typeof(ThemesFile).Assembly;
            using var stream = assembly.GetManifestResourceStream(CanonicalResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{CanonicalResourceName}' not found in {assembly.FullName}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Deserializes <see cref="CanonicalJson"/> into a <see cref="ThemesFile"/>.
    /// </summary>
    public static ThemesFile LoadCanonical()
        => JsonConvert.DeserializeObject<ThemesFile>(CanonicalJson)
           ?? throw new InvalidOperationException("Failed to deserialize the canonical themes list.");
}
