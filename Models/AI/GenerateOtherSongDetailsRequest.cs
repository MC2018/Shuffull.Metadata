namespace Shuffull.Metadata.Models.AI;

/// <summary>
/// Request for the "other details" inference call: language(s), original era, mood(s), energy and theme(s). The
/// <see cref="CandidateMoods"/> / <see cref="CandidateThemes"/> lists are the allowed vocabularies the model
/// must choose from; null or empty disables that dimension (energy is always returned). Mirrors how the genre
/// calls pass their candidate lists in; both are trailing optional parameters so existing callers keep
/// compiling unchanged.
/// </summary>
[Serializable]
public record GenerateOtherSongDetailsRequest(
    string SongName,
    List<string> ArtistNames,
    string? OtherDetailsContext = null,
    List<string>? CandidateMoods = null,
    List<string>? CandidateThemes = null,
    // Measured tempo from the audio. When supplied, the model also returns a genre-aware true tempo
    // (CorrectedBpm) so half/double-time tracker errors can be fixed, and uses the TRUE tempo when judging
    // energy. Null disables tempo correction. Trailing optional so existing callers keep compiling.
    int? MeasuredBpm = null,
    // Model to run instead of the configured strong model (e.g. the weak model for budget-tier tagging).
    // Null keeps the strong default, so existing callers are unaffected. Trailing optional on purpose.
    string? ModelOverride = null);
