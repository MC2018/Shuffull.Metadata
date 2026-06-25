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
    List<string>? CandidateThemes = null);
