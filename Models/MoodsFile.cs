using Newtonsoft.Json;

namespace Shuffull.Metadata.Models;

/// <summary>
/// The canonical master mood vocabulary that both Shuffull and the producer infer against, mirroring
/// <see cref="GenresFile"/>. Inferred moods must be drawn from this fixed list so they map cleanly onto a
/// shared, stable set of tags rather than free-form prose. The list is embedded in this assembly (see
/// Shuffull.Metadata.csproj) so both sides reference the same source and the vocabulary can't drift. The AI
/// picks 1-3 of these per song; anything off-list is dropped (membership is enforced post-parse, exactly
/// like sub-genres).
/// </summary>
[Serializable]
public class MoodsFile
{
    /// <summary>
    /// LogicalName of the embedded canonical moods list (see Shuffull.Metadata.csproj).
    /// </summary>
    private const string CanonicalResourceName = "moods.json";

    public List<string> Moods { get; set; } = [];

    /// <summary>
    /// The canonical master mood list, embedded in this assembly, that both Shuffull and the producer
    /// must infer against. This is the single shared source of the allowed mood vocabulary.
    /// </summary>
    public static string CanonicalJson
    {
        get
        {
            var assembly = typeof(MoodsFile).Assembly;
            using var stream = assembly.GetManifestResourceStream(CanonicalResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{CanonicalResourceName}' not found in {assembly.FullName}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Deserializes <see cref="CanonicalJson"/> into a <see cref="MoodsFile"/>.
    /// </summary>
    public static MoodsFile LoadCanonical()
        => JsonConvert.DeserializeObject<MoodsFile>(CanonicalJson)
           ?? throw new InvalidOperationException("Failed to deserialize the canonical moods list.");
}
