namespace Shuffull.Metadata.Models;

/// <summary>
/// Lyrics for a song, fetched and aligned by the producer (the producer) and handed to Shuffull on
/// import. <see cref="Synced"/> is LRC text (<c>[mm:ss.xx]line</c>). For YT-Music-sourced lyrics it has
/// already been shifted to compensate for any leading silence the producer trimmed from the audio (those
/// lyrics are timed to the exact track we downloaded); LRCLIB-sourced lyrics are timed to a different
/// canonical recording, so they are left unshifted. Either way the player may apply a further per-song
/// manual nudge for residual source drift. <see cref="Plain"/> is the untimed fallback; typically exactly
/// one of <see cref="Synced"/> / <see cref="Plain"/> is set. When <see cref="Instrumental"/> is true the
/// track has no lyrics by design and the player should skip the lyrics UI. All fields are
/// optional/defaulted so older payloads that omit them remain backward-compatible.
/// </summary>
[Serializable]
public record SongLyrics(
    string? Synced = null,
    string? Plain = null,
    bool Instrumental = false,
    string? Source = null);
