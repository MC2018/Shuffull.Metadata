using Newtonsoft.Json;

namespace Shuffull.Metadata.Models;

[Serializable]
public class GenresFile
{
    /// <summary>
    /// LogicalName of the embedded canonical genres list (see Shuffull.Metadata.csproj).
    /// </summary>
    private const string CanonicalResourceName = "genres.json";

    public List<MainGenre> MainGenres { get; set; } = [];

    [Serializable]
    public class MainGenre
    {
        public string Name { get; set; } = string.Empty;
        public List<string> SubGenreNames { get; set; } = [];
    }

    /// <summary>
    /// The canonical master genre list, embedded in this assembly, that both Shuffull and the
    /// the producer must infer against. This is the single shared source of the allowed genre
    /// vocabulary: generated genres must be drawn from these names so they map cleanly onto
    /// Shuffull's <c>Genres</c> table. Both sides reference this assembly, so the list can't drift.
    /// </summary>
    public static string CanonicalJson
    {
        get
        {
            var assembly = typeof(GenresFile).Assembly;
            using var stream = assembly.GetManifestResourceStream(CanonicalResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{CanonicalResourceName}' not found in {assembly.FullName}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Deserializes <see cref="CanonicalJson"/> into a <see cref="GenresFile"/>.
    /// </summary>
    public static GenresFile LoadCanonical()
        => JsonConvert.DeserializeObject<GenresFile>(CanonicalJson)
           ?? throw new InvalidOperationException("Failed to deserialize the canonical genres list.");
}
