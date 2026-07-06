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
    List<string>? Artists = null,
    // --- Provenance / regeneration inputs (all optional; null for older payloads & manual uploads) ---
    // The AI model that produced GeneratedTags. Persisted on the Song so a future "re-tag with a better
    // model" pass can target exactly the songs whose metadata came from a weaker model. Null when Shuffull
    // generated the tags itself (it stamps its own model in that case).
    string? TagModel = null,
    // The raw measured tempo (best-effort beat tracking) that was fed to the AI as a hint - distinct from the
    // resolved Bpm above (which prefers the AI's recognised tempo). Persisted so a tag regeneration can replay
    // the same input without re-analysing the audio.
    int? MeasuredBpm = null,
    // Objective audio-shape features measured from the exported track, used to ground the AI energy estimate
    // (EBU R128 loudness range / crest factor / onset density). Persisted so energy can be regenerated later
    // from stored inputs instead of re-downloading + re-analysing the audio. Null when unmeasured.
    double? LoudnessRangeLu = null,
    double? CrestFactorDb = null,
    double? OnsetsPerSecond = null,
    // Authoritative original release YEAR (from a reliable MusicBrainz match), or null when unknown / not
    // reliable. Persisted so a re-tag can re-apply the correct era instead of regressing to the AI's guess.
    int? OriginalReleaseYear = null,
    // Exploratory ("audition") ingest: the producer skipped the paid genre calls for this song, so it arrives
    // untagged for the user to try. Shuffull persists it as provisional (Song.Exploratory) and does NOT
    // self-generate tags; the song is enriched later only if the user keeps it (a re-tag clears the flag).
    // Default false = a normal, fully-tagged import.
    bool Exploratory = false);
