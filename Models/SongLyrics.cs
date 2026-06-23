namespace Shuffull.Metadata.Models;

/// <summary>
/// Lyrics for a song, fetched and aligned by the producer (the YoutubeFunnel) and handed to Shuffull on
/// import. <see cref="Synced"/> is LRC text (<c>[mm:ss.xx]line</c>) already shifted by
/// <see cref="AppliedOffsetMs"/> to compensate for any leading silence the producer trimmed from the audio
/// during conversion; the player may apply a further per-song manual nudge for residual source drift.
/// <see cref="Plain"/> is the untimed fallback. Typically exactly one of <see cref="Synced"/> /
/// <see cref="Plain"/> is set. When <see cref="Instrumental"/> is true the track has no lyrics by design and
/// the player should skip the lyrics UI. All fields are optional/defaulted so older payloads that omit them
/// remain backward-compatible.
/// </summary>
[Serializable]
public record SongLyrics(
    string? Synced = null,
    string? Plain = null,
    bool Instrumental = false,
    string? Source = null,
    int AppliedOffsetMs = 0);
