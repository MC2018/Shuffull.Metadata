namespace Shuffull.Metadata.Models;

/// <summary>
/// Genre/era/language/mood/theme tags inferred for a song by the producer (the YoutubeFunnel) and handed to
/// Shuffull on import. <see cref="Moods"/> / <see cref="Themes"/> are drawn from their canonical lists;
/// <see cref="Energy"/> is a 1-10 scalar (1 = calm/sparse, 10 = intense/driving). <see cref="Themes"/> are
/// sparse origin/relationship labels (Anime, Vocaloid, Cover…) — usually empty. All are optional with null
/// defaults so older payloads that omit them remain backward-compatible.
/// </summary>
[Serializable]
public record GeneratedSongTags(
    List<string> MainGenres,
    List<string> SubGenres,
    List<string> Languages,
    string TimePeriod,
    List<string>? Moods = null,
    int? Energy = null,
    List<string>? Themes = null);
