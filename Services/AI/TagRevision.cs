namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// Which revision of the genre engine's prompts produced a song's tags. Shuffull re-queues every tagged song
/// whose revision is behind <see cref="Current"/>, and the producer keys its cached other-details answers by it,
/// so bumping this re-scores the library instead of replaying old answers.
///
/// <para>Bump it only for a change worth paying a library-wide re-tag for: the queue drains at the producer's
/// pace and costs one AI call per song (more for songs with nothing cached).</para>
/// </summary>
public static class TagRevision
{
    /// <summary>
    /// 1: heaviness replaced energy (shuffull-workspace#41).
    /// 0: everything tagged before revisions existed.
    /// </summary>
    public const int Current = 1;
}
