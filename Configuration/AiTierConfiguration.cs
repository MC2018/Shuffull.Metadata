namespace Shuffull.Metadata.Configuration;

/// <summary>
/// Which configured provider serves each tier. Both default to "OpenAI", so an install that says nothing
/// about tiers behaves exactly as it did before providers existed.
/// </summary>
/// <remarks>
/// The values are the names of sibling sections under <c>AI:</c> — "OpenAI", "Meta", … — each of which binds
/// to an <see cref="OpenAIConfiguration"/>. That is the whole extension point: a new vendor is a new section
/// plus a name here, with no code change, as long as it speaks the OpenAI chat/completions shape.
/// </remarks>
public class AiTierConfiguration
{
    public const string AiTierConfigurationSection = "AI:Tiers";

    public string Weak { get; set; } = "OpenAI";
    public string Strong { get; set; } = "OpenAI";
}
