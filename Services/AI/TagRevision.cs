namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// Which revision of the genre engine's prompts produced a song's tags. Shuffull re-queues every tagged,
/// unlocked song whose revision is behind <see cref="Current"/> (curator-locked songs keep their hand edits),
/// so bumping this re-scores the library.
///
/// <para>Before bumping it:</para>
/// <list type="bullet">
/// <item>The producer keys only its cached OTHER-DETAILS answers by revision (YoutubeFunnel AiCacheKeys). A bump
/// for a change to the main- or sub-genre prompts must also key those caches by revision, or the re-score
/// replays the old genre answers.</item>
/// <item>Deploy the producer at the new revision BEFORE the site. A site ahead of its producer offers every song
/// for a re-score, refuses each write (same model, older revision), and the queue backs off and loops until
/// the producer catches up.</item>
/// <item>It is worth it only for a change worth a library-wide re-tag: the queue drains at the producer's pace
/// and costs at least one AI call per song.</item>
/// </list>
/// </summary>
public static class TagRevision
{
    /// <summary>
    /// 2: loud, dense EDM moved from 7 to 8 on the heaviness scale (Shuffull.Metadata#5), and the heaviness call
    /// gets measured grit and density (Shuffull.Metadata#8).
    /// 1: heaviness replaced energy (shuffull-workspace#41).
    /// 0: everything tagged before revisions existed.
    /// </summary>
    public const int Current = 2;
}
