namespace Shuffull.Metadata.Contracts;

/// <summary>
/// Shuffull's answer to a batch of <see cref="SongRequestAckDto"/>, by request id. <see cref="Applied"/> moved their
/// request. <see cref="Rejected"/> did not: the request is gone, or it is in a state that ack can't follow. A rejected
/// <see cref="Enums.SongRequestAckStatus.Accepted"/> means the user cancelled before the producer got to it, so the
/// producer should drop the download rather than finish it.
/// </summary>
[Serializable]
public record SongRequestAckResponse(
    List<string> Applied,
    List<string> Rejected);
