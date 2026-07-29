using Nut.Results;
using Shuffull.Metadata.Models.AI;

namespace Shuffull.Metadata.Services.AI;

public interface IAIService
{
    public Task<Result<GenerateMainGenresResponse>> GenerateMainGenresAsync(GenerateMainGenresRequest request, CancellationToken cancellationToken = default!);
    public Task<Result<GenerateSubGenresResponse>> GenerateSubGenresAsync(GenerateSubGenresRequest request, CancellationToken cancellationToken = default!);
    public Task<Result<GenerateOtherSongDetailsResponse>> GenerateOtherSongDetailsAsync(GenerateOtherSongDetailsRequest request, CancellationToken cancellationToken = default!);

    // ── OpenAI Batch support (OPTIONAL) ─────────────────────────────────────────────────────────────
    //
    // The Batch API is ~50% cheaper but asynchronous, so a caller that wants it needs the two halves of a call
    // separately: the request BODY to put in the batch file, and the PARSER to apply to the result that comes
    // back hours later. In the real implementation these use the same prompt and the same validation as the
    // *Async methods above - the point of exposing them is that the batch path CANNOT drift from the live one.
    //
    // DEFAULT-IMPLEMENTED on purpose. This interface is shared by two repos (the funnel and the site) and has
    // implementors that only ever make live calls - including test doubles. Batching is an opt-in capability,
    // so requiring all six members would break those for no benefit; an implementation that does not support
    // batching simply inherits these and says so loudly if something tries.

    private static NotSupportedException Unsupported(string member) =>
        new($"This IAIService implementation does not support the OpenAI Batch API ({member}).");

    /// <summary>The chat-completions request body for a batched main-genres call.</summary>
    public string BuildMainGenresBatchBody(GenerateMainGenresRequest request, string? modelOverride = null) =>
        throw Unsupported(nameof(BuildMainGenresBatchBody));

    /// <summary>The chat-completions request body for a batched sub-genres call.</summary>
    public string BuildSubGenresBatchBody(GenerateSubGenresRequest request, string? modelOverride = null) =>
        throw Unsupported(nameof(BuildSubGenresBatchBody));

    /// <summary>The chat-completions request body for a batched other-details call.</summary>
    public string BuildOtherSongDetailsBatchBody(GenerateOtherSongDetailsRequest request, string? modelOverride = null) =>
        throw Unsupported(nameof(BuildOtherSongDetailsBatchBody));

    /// <summary>Validates a main-genres completion, whichever path produced it.</summary>
    public Result<GenerateMainGenresResponse> ParseMainGenres(string content, GenerateMainGenresRequest request) =>
        throw Unsupported(nameof(ParseMainGenres));

    /// <summary>Validates a sub-genres completion, whichever path produced it.</summary>
    public Result<GenerateSubGenresResponse> ParseSubGenres(string content, GenerateSubGenresRequest request) =>
        throw Unsupported(nameof(ParseSubGenres));

    /// <summary>
    /// Validates an other-details completion, whichever path produced it. An error means "regenerate", not
    /// "give up": the live path retries immediately, the batch path re-queues.
    /// </summary>
    public Result<GenerateOtherSongDetailsResponse> ParseOtherSongDetails(string content, GenerateOtherSongDetailsRequest request) =>
        throw Unsupported(nameof(ParseOtherSongDetails));
}
