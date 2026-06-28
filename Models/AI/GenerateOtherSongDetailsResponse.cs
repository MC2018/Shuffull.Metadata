namespace Shuffull.Metadata.Models.AI;

/// <summary>
/// Response for the "other details" inference call. <see cref="Moods"/> and <see cref="Themes"/> are drawn
/// from the request's candidate lists (membership enforced post-parse); <see cref="Energy"/> is a 1-10 scalar,
/// or null when the model returned an out-of-range/unparseable value. Themes are sparse — usually empty.
/// <see cref="CorrectedBpm"/> is the model's genre-aware true tempo (an octave of the measured BPM), null when
/// no measured tempo was supplied; the caller snaps it back to a real octave before trusting it.
/// </summary>
[Serializable]
public record GenerateOtherSongDetailsResponse(string TimePeriod, List<string> Languages, List<string> Moods, int? Energy, List<string> Themes, int? CorrectedBpm = null);
