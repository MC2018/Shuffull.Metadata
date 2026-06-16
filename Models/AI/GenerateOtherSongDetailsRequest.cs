namespace Shuffull.Metadata.Models.AI;

/// <summary>
/// Request for the "other details" inference call: language(s), original era, mood(s) and energy. The
/// <see cref="CandidateMoods"/> list is the allowed mood vocabulary the model must choose from (typically
/// <see cref="Models.Moods.Canonical"/>); null or empty disables mood inference (energy is still returned).
/// Mirrors how the genre calls pass their candidate lists in. <see cref="CandidateMoods"/> is the trailing
/// optional parameter so existing 3-argument callers keep compiling unchanged.
/// </summary>
[Serializable]
public record GenerateOtherSongDetailsRequest(
    string SongName,
    List<string> ArtistNames,
    string? OtherDetailsContext = null,
    List<string>? CandidateMoods = null);
