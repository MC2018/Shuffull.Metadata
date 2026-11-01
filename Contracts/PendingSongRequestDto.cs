namespace Shuffull.Metadata.Contracts;

/// <summary>
/// One song a user asked for that the library doesn't have yet, as Shuffull hands it to the producer from its queue of
/// pending requests. The producer downloads it, acks its progress with <see cref="SongRequestAckDto"/>, and imports
/// it with <see cref="SongImportDetails.SongRequestId"/> set to <see cref="SongRequestId"/>, which completes the request.
///
/// <para>Title, artists, duration and thumbnail are what the user picked from a <see cref="SourceSearchResultDto"/>, so
/// the producer can name the song without looking it up again. <see cref="TargetPlaylistName"/> is decided by Shuffull
/// (the user's requests playlist for the week). The producer passes it through unchanged on the import, so a song
/// linked straight away and a song downloaded first land in the same playlist.</para>
/// </summary>
[Serializable]
public record PendingSongRequestDto(
    string SongRequestId,
    string UserId,
    string ExternalSongId,
    string Title,
    List<string> Artists,
    int? DurationSeconds,
    string? ThumbnailUrl,
    string TargetPlaylistName);
