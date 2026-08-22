using System.Text.Json.Serialization;

namespace Shuffull.Metadata.Configuration;

public class OpenAIConfiguration
{
    [JsonIgnore]
    public const string OpenAIConfigurationSection = "AI:OpenAI";
    public string ApiKey { get; set; } = string.Empty;
    // The capable ("strong") model for nuanced inference (genres, sub-genres, era, mood, energy). Provider-neutral:
    // just the model identifier the configured AI provider understands. Prefer this over the legacy ModelName.
    public string StrongModelName { get; set; } = string.Empty;
    // Legacy single-model key, kept so existing "AI:OpenAI:ModelName" config still binds (used as the strong model).
    public string ModelName { get; set; } = string.Empty;
    // The cheap ("weak") model for budget work (coarse filters, Standard-tier tagging). Optional: when unset,
    // the strong model is used everywhere, so weak-model behavior is strictly opt-in.
    public string WeakModelName { get; set; } = string.Empty;
    public string InstructionFile { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
    // Base URL of the OpenAI-COMPATIBLE host to call. Empty means OpenAI itself. Set it to point the very same
    // client at another vendor serving the chat/completions shape - Meta's Model API (https://api.meta.ai/v1,
    // model "muse-spark-1.2-contributor") does, strict JSON-schema structured outputs included. That compatibility is why a
    // second provider is CONFIGURATION here rather than a second IAIService: prompts, schemas and parsing are
    // identical, only the host and model id differ.
    //
    // NOTE: a non-OpenAI host almost certainly has no /v1/batches endpoint, so the caller must not route batch
    // work to it. See IAIService.SupportsBatch.
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The effective strong model: <see cref="StrongModelName"/>, falling back to the legacy <see cref="ModelName"/>.</summary>
    [JsonIgnore]
    public string ResolvedStrongModelName => !string.IsNullOrWhiteSpace(StrongModelName) ? StrongModelName : ModelName;

    /// <summary>The effective weak model: <see cref="WeakModelName"/>, falling back to the strong model.</summary>
    [JsonIgnore]
    public string ResolvedWeakModelName => !string.IsNullOrWhiteSpace(WeakModelName) ? WeakModelName : ResolvedStrongModelName;

    public class SupportedApiEndpoints
    {
        // Lowercase for consistency
        public const string ChatCompletions = "chat/completions";
        public const string Responses = "responses";
        public static readonly string[] All = { ChatCompletions, Responses };
    }
}
