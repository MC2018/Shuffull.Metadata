using Newtonsoft.Json;

namespace Shuffull.Metadata.Models;

/// <summary>
/// Genre/era/language/mood/theme tags inferred for a song by the producer (the producer) and handed to
/// Shuffull on import. <see cref="Moods"/> / <see cref="Themes"/> are drawn from their canonical lists;
/// <see cref="Heaviness"/> is the 1-10 score (1 = ambient/gentle, 10 = punishing; one absolute scale across genres).
/// <see cref="Themes"/> are sparse origin/relationship labels (Anime, Vocaloid, Cover…) — usually empty. All are optional with null
/// defaults so older payloads that omit them remain backward-compatible.
/// </summary>
[Serializable]
public record GeneratedSongTags(
    List<string> MainGenres,
    List<string> SubGenres,
    List<string> Languages,
    string TimePeriod,
    List<string>? Moods = null,
    int? Heaviness = null,
    List<string>? Themes = null)
{
    /// <summary>
    /// Heaviness under the key it had before the rename (shuffull-workspace#50). Export files,
    /// <c>SongImports.GeneratedTagsJson</c> and stored AI responses still carry it, so it is read, never written:
    /// there is no getter, and neither serializer emits a property it can't read.
    /// </summary>
    [JsonProperty("Energy")]
    [System.Text.Json.Serialization.JsonPropertyName("energy")]
    [System.Text.Json.Serialization.JsonInclude]
    private int? LegacyEnergy
    {
        init => Heaviness ??= value;
    }
}
