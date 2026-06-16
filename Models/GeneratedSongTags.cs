namespace Shuffull.Metadata.Models;

/// <summary>
/// Genre/era/language/mood tags inferred for a song by the producer (the YoutubeFunnel) and handed to
/// Shuffull on import. <see cref="Moods"/> is drawn from the canonical <see cref="Moods.Canonical"/> list;
/// <see cref="Energy"/> is a 1-10 scalar (1 = calm/sparse, 10 = intense/driving). Both are optional with
/// null defaults so older payloads that omit them remain backward-compatible.
/// </summary>
[Serializable]
public record GeneratedSongTags(
    List<string> MainGenres,
    List<string> SubGenres,
    List<string> Languages,
    string TimePeriod,
    List<string>? Moods = null,
    int? Energy = null);
