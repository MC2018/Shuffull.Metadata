namespace Shuffull.Metadata.Contracts;

/// <summary>
/// One song from a search of the external source, which the producer runs and Shuffull relays to the app. Songs
/// (audio) only, never music videos. <see cref="ExternalSongId"/> is the id an import of it would carry, so Shuffull
/// can tell which results are already in the library. Anything the source doesn't give is null (the album and
/// duration often are). Per-user flags, such as whether the caller already asked for a song, are added by Shuffull
/// on its own response, not here.
/// </summary>
[Serializable]
public record SourceSearchResultDto(
    string ExternalSongId,
    string Title,
    List<string> Artists,
    string? Album,
    int? DurationSeconds,
    string? ThumbnailUrl,
    bool IsExplicit);
