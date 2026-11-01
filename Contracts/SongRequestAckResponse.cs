namespace Shuffull.Metadata.Contracts;

/// <summary>
/// Shuffull's answer to a batch of <see cref="SongRequestAckDto"/>, by request id. <see cref="Applied"/> moved their
/// request, or found it already at that status (a re-sent ack is applied, not rejected). <see cref="Rejected"/> did
/// not: the request is gone, or it is in a state that ack can't follow. So a rejected
/// <see cref="Enums.SongRequestAckStatus.Accepted"/> means the request was cancelled, completed or removed before the
/// producer got to it, and the producer should drop the download rather than finish it.
/// </summary>
[Serializable]
public record SongRequestAckResponse(
    List<string> Applied,
    List<string> Rejected);
