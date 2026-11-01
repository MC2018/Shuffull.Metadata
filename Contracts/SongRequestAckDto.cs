using Shuffull.Metadata.Enums;

namespace Shuffull.Metadata.Contracts;

/// <summary>
/// The producer's report on one song request (<see cref="PendingSongRequestDto"/>). Sent as an array.
///
/// <para>The ack is conditional, like the rating ack: Shuffull only moves a request that is still in a state this
/// status may follow, and leaves any other alone. A request the user cancelled, or one an import already completed, is
/// never moved back by a late ack. <see cref="SongRequestAckResponse"/> says which acks applied.</para>
///
/// <para><see cref="Reason"/> is free text, set with <see cref="SongRequestAckStatus.Retrying"/> or
/// <see cref="SongRequestAckStatus.Failed"/>. It is not an enum on purpose: the producer's failure categories change as
/// its download tools do, and the contract should not have to follow them.</para>
/// </summary>
[Serializable]
public record SongRequestAckDto(
    string SongRequestId,
    SongRequestAckStatus Status,
    string? Reason = null);
