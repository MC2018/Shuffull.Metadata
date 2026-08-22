namespace Shuffull.Metadata.Services.AI;

/// <summary>
/// How much model a call is worth. Deliberately an axis SEPARATE from which vendor serves it: tier is a
/// property of the work (a bulk import is cheap, a song the user kept is not), while the provider is a
/// deployment choice. Keeping them apart is what stops a provider-per-tier class matrix — adding a vendor
/// stays one config entry instead of doubling the implementations — and preserves the per-request
/// <c>ModelOverride</c> the tiered-retag path pins its calls with.
/// </summary>
public enum AiTier
{
    /// <summary>Budget work: coarse filters, bulk/Standard-tier tagging. High volume, low stakes.</summary>
    Weak = 0,

    /// <summary>Nuanced inference for songs the user kept or liked. Low volume, and the tags are kept.</summary>
    Strong = 1,
}
