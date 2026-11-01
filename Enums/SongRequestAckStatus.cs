namespace Shuffull.Metadata.Enums;

/// <summary>
/// What the producer reports about a song request it picked up (<see cref="Contracts.SongRequestAckDto"/>). Only the
/// producer's progress lives here. The states Shuffull sets itself (pending, completed, cancelled) are not acks.
/// Goes over the wire as its int, like every enum the API sends.
/// </summary>
public enum SongRequestAckStatus
{
    /// <summary>
    /// Never sent on purpose. A missing status reads as this. Shuffull refuses it, and any value that isn't a defined
    /// member (an int such as 7 still arrives as 7), rather than checking for this one alone.
    /// </summary>
    Unknown = 0,

    /// <summary>The producer has queued the request for download.</summary>
    Accepted = 1,

    /// <summary>A download attempt is under way.</summary>
    Downloading = 2,

    /// <summary>An attempt failed and the producer is trying again. The user sees that it is taking longer.</summary>
    Retrying = 3,

    /// <summary>
    /// The producer gave up reporting progress (its attempt or time cap ran out). It may still keep trying in the
    /// background, so an import can complete the request later.
    /// </summary>
    Failed = 4,
}
