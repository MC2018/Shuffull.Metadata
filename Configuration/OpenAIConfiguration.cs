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
    public string InstructionFile { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>The effective strong model: <see cref="StrongModelName"/>, falling back to the legacy <see cref="ModelName"/>.</summary>
    [JsonIgnore]
    public string ResolvedStrongModelName => !string.IsNullOrWhiteSpace(StrongModelName) ? StrongModelName : ModelName;

    public class SupportedApiEndpoints
    {
        // Lowercase for consistency
        public const string ChatCompletions = "chat/completions";
        public const string Responses = "responses";
        public static readonly string[] All = { ChatCompletions, Responses };
    }
}
