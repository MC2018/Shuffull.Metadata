namespace Shuffull.Metadata.Models.AI;

/// <summary>
/// Response for the "other details" inference call. <see cref="Moods"/> are drawn from the request's
/// candidate list (membership enforced post-parse); <see cref="Energy"/> is a 1-10 scalar, or null when
/// the model returned an out-of-range/unparseable value or mood inference was disabled.
/// </summary>
[Serializable]
public record GenerateOtherSongDetailsResponse(string TimePeriod, List<string> Languages, List<string> Moods, int? Energy);
