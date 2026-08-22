using Shuffull.Metadata.Configuration;

namespace Shuffull.Metadata.Services.AI;

/// <summary>A configured provider: its name, the client that talks to it, and the models it was given.</summary>
public sealed record AiProvider(string Name, IAIService Service, OpenAIConfiguration Config);

/// <summary>
/// Maps a tier onto the provider configured to serve it. The model comes from THAT provider's own weak/strong
/// names, so pointing the weak tier at a different vendor does not accidentally send it a model id the vendor
/// has never heard of.
/// </summary>
public sealed class AIServiceResolver : IAIServiceResolver
{
    private readonly IReadOnlyDictionary<string, AiProvider> _providers;
    private readonly AiTierConfiguration _tiers;

    public AIServiceResolver(IEnumerable<AiProvider> providers, AiTierConfiguration tiers)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _tiers = tiers;

        // Fail at startup, not at the first tagging call hours later: a typo in a provider name is otherwise
        // invisible until something tries to enrich a song, by which point the failure looks like an AI outage.
        foreach (var (tier, name) in new[] { (nameof(AiTier.Weak), tiers.Weak), (nameof(AiTier.Strong), tiers.Strong) })
        {
            if (!_providers.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"{AiTierConfiguration.AiTierConfigurationSection}:{tier} names provider '{name}', which is not configured. " +
                    $"Configured providers: {(_providers.Count == 0 ? "(none)" : string.Join(", ", _providers.Keys))}.");
            }
        }
    }

    public (IAIService Service, string Model) Resolve(AiTier tier)
    {
        var name = tier == AiTier.Weak ? _tiers.Weak : _tiers.Strong;
        var provider = _providers[name];
        var model = tier == AiTier.Weak ? provider.Config.ResolvedWeakModelName : provider.Config.ResolvedStrongModelName;

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' serves the {tier} tier but has no model configured for it.");
        }

        return (provider.Service, model);
    }
}
