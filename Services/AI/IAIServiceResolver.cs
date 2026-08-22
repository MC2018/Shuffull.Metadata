namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// Picks the provider and model id for a tier. Callers ask for the TIER they need and stay ignorant of who
/// serves it, so moving bulk tagging onto a cheaper vendor is a configuration change rather than an edit to
/// every call site.
/// </summary>
public interface IAIServiceResolver
{
    /// <summary>
    /// The service to call for <paramref name="tier"/>, plus the model id to pin on the request. Pass that id
    /// through as the request's <c>ModelOverride</c> — the service otherwise falls back to its own configured
    /// strong model, which for a weak-tier call would quietly spend the expensive model.
    /// </summary>
    (IAIService Service, string Model) Resolve(AiTier tier);
}
