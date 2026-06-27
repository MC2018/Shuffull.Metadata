using System.Collections.Generic;
using Shuffull.Metadata.Enums;
using Shuffull.Metadata.Models;

namespace Shuffull.Metadata.Contracts;

/// <summary>
/// Canonical wire contract for a single song handed off from an external producer to Shuffull for import.
/// It is serialized to JSON and dropped into the external-import folder next to
/// the audio file; Shuffull's ExternalSongImporterService deserializes it into a SongImport row.
///
/// This is the single shared definition both sides are meant to reference, replacing the previously
/// hand-copied SongImportDetails (Shuffull) / SongExportDetails (producer) record pair that could silently
/// drift. The producer is repointed at this type as a separately-coordinated step.
///
/// <para><see cref="GeneratedTags"/> is optional. When the producer (with its richer
/// per-video context) has already inferred genres/era/languages, it may attach them here so Shuffull can
/// persist them directly and skip its own AI call. When null — e.g. manual uploads or producers that do
/// not generate tags — Shuffull falls back to generating them itself. Older payloads that omit the field
/// deserialize to null, so this addition is backward-compatible.</para>
/// </summary>
[Serializable]
public record SongImportDetails(
    string ExternalSongId,
    string FileExtension,
    string ExternalPlaylistId,
    string TargetUserId,
    string? TargetPlaylistId,
    string? TargetPlaylistName,
    ExternalSource ExternalSource,
    GeneratedSongTags? GeneratedTags = null,
    // True when the user liked the source song on the external platform (a YouTube / YT Music "Liked videos"
    // item). Shuffull may auto-like the song on import. Optional with a false default, so older payloads that
    // omit it remain backward-compatible.
    bool MarkAsLiked = false,
    // Estimated tempo in beats per minute, derived from the audio by the producer (best-effort beat tracking).
    // Null when the producer could not determine it (analysis failed, too few beats, an implausible result, or
    // the producer doesn't compute BPM at all). Optional with a null default for backward compatibility.
    int? Bpm = null,
    // Lyrics fetched + aligned by the producer (YT Music first, LRCLIB fallback). Null when none were found,
    // or for manual uploads / producers that don't fetch lyrics — in which case Shuffull may fetch its own
    // (LRCLIB-only, since it does not touch YouTube). Optional with a null default for backward compatibility.
    SongLyrics? Lyrics = null,
    // Shuffull SongId this import should REPLACE in place (re-sourced audio for a song flagged as poor quality).
    // Null = a normal new-song import. When set, Shuffull overwrites that song's audio/tags/metadata while
    // keeping its id and every user association (likes, playlists, recently-played). Optional/null for back-compat.
    string? ReplacesSongId = null,
    // The producer's vetted display title + ordered artist list. When set, Shuffull uses these as the
    // AUTHORITATIVE name/artists instead of the audio file's ID3 tags — which a MusicBrainz match on the
    // producer side can overwrite (collapsing a multi-artist collab into one credit string, or renaming a
    // song to a mis-matched recording). Null/empty => Shuffull falls back to the file's tags, so manual
    // uploads and older payloads that omit these remain backward-compatible.
    string? Name = null,
    List<string>? Artists = null);
