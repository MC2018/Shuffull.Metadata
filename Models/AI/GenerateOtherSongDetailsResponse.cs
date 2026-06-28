namespace Shuffull.Metadata.Models.AI;

/// <summary>
/// Response for the "other details" inference call. <see cref="Moods"/> and <see cref="Themes"/> are drawn
/// from the request's candidate lists (membership enforced post-parse); <see cref="Energy"/> is a 1-10 scalar,
/// or null when the model returned an out-of-range/unparseable value. Themes are sparse — usually empty.
/// <see cref="TrueBpm"/> is the model's best tempo estimate; <see cref="BpmRecognized"/> is true only when the
/// model genuinely recognises the specific track (vs guessing from genre), so the caller can decide whether to
/// trust it over the unreliable measured tempo.
/// </summary>
[Serializable]
public record GenerateOtherSongDetailsResponse(string TimePeriod, List<string> Languages, List<string> Moods, int? Energy, List<string> Themes, int? TrueBpm = null, bool BpmRecognized = false);
