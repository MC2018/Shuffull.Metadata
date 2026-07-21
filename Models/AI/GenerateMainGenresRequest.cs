namespace Shuffull.Metadata.Models.AI;

[Serializable]
public record GenerateMainGenresRequest(
    string SongName,
    List<string> ArtistNames,
    List<string> MainGenres,
    string? MainGenresContext = null,
    // Model to run instead of the configured strong model (e.g. the weak model for budget-tier tagging).
    // Null keeps the strong default, so existing callers are unaffected. Trailing optional on purpose.
    string? ModelOverride = null);
