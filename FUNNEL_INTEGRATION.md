# Funnel ⇆ Shuffull integration (`Shuffull.Metadata`)

This document is for the **funnel** side. It describes the shared contract and the shared
genre-inference engine that now live in the `Shuffull.Metadata` library, and exactly what the funnel
needs to do to (a) keep producing valid import drops and (b) optionally attach precomputed genres so
Shuffull can skip its own AI call.

You should be able to implement the funnel side from this document alone, without reading the Shuffull
codebase.

---

## TL;DR

1. The funnel→Shuffull hand-off record and the genre-inference engine were extracted out of
   `Shuffull.Site` into a standalone library, **`Shuffull.Metadata`** (net8.0, 3 NuGet deps only). It
   builds with no dependency on Shuffull.Site, so the funnel can reference it without pulling in the app.
2. Replace the funnel's hand-copied `SongExportDetails` record with the canonical
   **`Shuffull.Metadata.Contracts.SongImportDetails`**. The two were identical by hand; now there is one
   definition both sides share so they can't drift.
3. The contract gained one **optional, backward-compatible** field: `GeneratedTags`
   (`Shuffull.Metadata.Models.GeneratedSongTags?`). Leave it `null` to keep today's behavior. Populate it
   to ship funnel-inferred genres/era/languages with the drop.
4. To populate it, call the shared engine **`Shuffull.Metadata.Services.AI.IAIService`** (impl
   `OpenAIService`) — the same engine Shuffull uses — but feed it the richer per-video context the funnel
   has. Output is a `GeneratedSongTags`.
5. **Hard requirement:** both sides must infer against the **same master genre list**
   (`Shuffull.Metadata.Models.GenresFile`). See [Master genre list](#master-genre-list) — this is the
   one unresolved coordination item.

> **Sequencing note.** As of this writing Shuffull *accepts* the new field on the wire and has a column
> to store it, but does **not yet consume it** (the "prefer precomputed tags over the AI call" wiring is a
> later, separately-coordinated stage). So you can implement and start emitting `GeneratedTags` now with
> zero risk — Shuffull will simply ignore it until that stage lands. Nothing you ship breaks the current
> pipeline.

---

## Why it's structured this way (and the constraint it respects)

The funnel is intentionally **private** (so the scraping/evasion logic isn't easy to patch out). Shuffull
is meant to stay **easily self-hostable** by others. The rule is: *the two projects must not hard-require
each other.*

What YouTube would ever patch is the scraping (yt-dlp ciphers, rotation, etc.) — **not** a genre prompt.
So the genre engine has no reason to be private and is fine to share. The safe dependency direction is:

```
  private funnel  ──references──▶  public Shuffull.Metadata  ◀──references──  Shuffull.Site
```

Nothing flows back into the funnel; `Shuffull.Metadata` never references Shuffull.Site or the funnel. The
runtime coupling between funnel and Shuffull remains exactly what it is today: **a JSON file dropped in a
shared folder** (see [Drop mechanics](#drop-mechanics)).

---

## Consuming `Shuffull.Metadata`

**Target framework / versioning.** `Shuffull.Metadata` targets **net8.0**. To reference it the funnel
project must target net8.0+ (the old net6.0 / netstandard2.0 targets won't work). Updating the funnel's
TFM is expected and acceptable.

**Package dependencies** (these are all the lib needs):

| Package | Version |
| --- | --- |
| `Newtonsoft.Json` | 13.0.3 |
| `Nut.Results` | 2.1.1 |
| `OpenAI` | 2.2.0-beta.4 |

**How to pull it in** — pick one:

- **Git submodule (simplest to start).** Add the Shuffull repo (or a split-out of just `Shuffull.Metadata`)
  as a submodule, then `<ProjectReference>` the `Shuffull.Metadata.csproj`. Zero publishing infrastructure;
  you build from source.
- **NuGet package (cleaner long-term).** Pack `Shuffull.Metadata` and publish to a private feed (or
  GitHub Packages), then `<PackageReference>` it. Better for versioning, but needs a feed + a pack/publish
  step.

Recommendation: start with the submodule, move to NuGet once the contract stabilizes.

---

## The wire contract: `SongImportDetails`

Namespace `Shuffull.Metadata.Contracts`. This is the JSON object the funnel writes into the drop folder.

```csharp
public record SongImportDetails(
    string ExternalSongId,
    string FileExtension,
    string ExternalPlaylistId,
    string TargetUserId,
    string? TargetPlaylistId,
    string? TargetPlaylistName,
    bool LikelyOriginalArtist,
    ExternalSource ExternalSource,
    GeneratedSongTags? GeneratedTags = null);   // NEW, optional
```

| Field | Type | Notes |
| --- | --- | --- |
| `ExternalSongId` | string | YouTube video id. Also used to locate the audio file (see below). |
| `FileExtension` | string | Include the dot, e.g. `".m4a"`. Used to build the audio filename. |
| `ExternalPlaylistId` | string | Source playlist id. |
| `TargetUserId` | string | Shuffull user the import belongs to. |
| `TargetPlaylistId` | string? | Target playlist id, or null. |
| `TargetPlaylistName` | string? | Target playlist name, or null. |
| `LikelyOriginalArtist` | bool | Funnel's heuristic flag. |
| `ExternalSource` | `ExternalSource` enum | See below. For the funnel this is `Youtube`. |
| `GeneratedTags` | `GeneratedSongTags?` | **New.** Optional precomputed tags; null = "let Shuffull generate". |

### `ExternalSource` enum

Namespace `Shuffull.Metadata.Enums`:

```csharp
public enum ExternalSource { Unknown = 0, Youtube = 1, Manual = 2 }
```

The funnel always uses **`Youtube` (= 1)**.

### Serialization rules — read this carefully

Shuffull deserializes the drop with **`Newtonsoft.Json` (`JsonConvert.DeserializeObject<SongImportDetails>`)
using default settings**. That means:

- **Property names are PascalCase** exactly as above (`ExternalSongId`, not `externalSongId`). No camelCase
  resolver is configured.
- **Enums serialize as their integer value** by default. `ExternalSource` should be the number `1` for
  YouTube. (Newtonsoft will also accept the string `"Youtube"` on read, but the canonical form Shuffull
  writes/expects is the int — emit `1`.)
- Reference the shared `SongImportDetails` type and serialize it with `JsonConvert.SerializeObject(...)` to
  guarantee a matching shape. Don't hand-roll the JSON.

### Example drop JSON

```json
{
  "ExternalSongId": "dQw4w9WgXcQ",
  "FileExtension": ".m4a",
  "ExternalPlaylistId": "PL0123456789",
  "TargetUserId": "01J9XYZ...",
  "TargetPlaylistId": "01J9ABC...",
  "TargetPlaylistName": null,
  "LikelyOriginalArtist": true,
  "ExternalSource": 1,
  "GeneratedTags": {
    "MainGenres": ["Pop"],
    "SubGenres": ["J-Pop"],
    "Languages": ["Japanese"],
    "TimePeriod": "2010s"
  }
}
```

`GeneratedTags` may be omitted entirely or set to `null` — both deserialize to null and reproduce today's
behavior.

---

## The output payload: `GeneratedSongTags`

Namespace `Shuffull.Metadata.Models`. This is the only type that crosses the wire as the engine's output.

```csharp
public record GeneratedSongTags(
    List<string> MainGenres,
    List<string> SubGenres,
    List<string> Languages,
    string TimePeriod);
```

- `MainGenres` / `SubGenres` — must be values drawn from the shared master genre list (see below).
- `Languages` — prominent lyric languages; `["Instrumental"]` if no lyrics.
- `TimePeriod` — a decade like `"2010s"` (post‑1900) or a century like `"1800s"`.

---

## Drop mechanics

How Shuffull currently ingests (so your output lands correctly):

- Shuffull's `ExternalSongImporterService` polls the configured **external import directory** on an
  interval and processes every file ending in `.json`.
- For each JSON file it deserializes a `SongImportDetails`, then looks for the **audio file next to it**
  at `"{ExternalSongId}{FileExtension}"` in the same directory (e.g. `dQw4w9WgXcQ.m4a`).
- It reads the song title from the audio file's tags (TagLib), falling back to `ExternalSongId`.
- It creates a `SongImport` row, moves the audio into Shuffull's import area, and deletes the JSON.

So the funnel must, for each song, place **two files** in the shared import directory:

1. the audio file named `{ExternalSongId}{FileExtension}`, and
2. a `{anything}.json` (conventionally `{ExternalSongId}.json`) containing the serialized
   `SongImportDetails`.

This is unchanged by this work — it's the same drop the funnel already does. The only addition is the
optional `GeneratedTags` inside the JSON.

---

## Producing `GeneratedTags` with the shared engine

The whole point of moving the engine into `Shuffull.Metadata` is that the funnel can run the *same*
inference Shuffull would, but with **better inputs** (topic categories, hashtags, duration, upload date,
etc. that the funnel already fetched per video).

### The interface

Namespace `Shuffull.Metadata.Services.AI`:

```csharp
public interface IAIService
{
    Task<Result<GenerateMainGenresResponse>>       GenerateMainGenresAsync(GenerateMainGenresRequest request, CancellationToken ct = default);
    Task<Result<GenerateSubGenresResponse>>        GenerateSubGenresAsync(GenerateSubGenresRequest request, CancellationToken ct = default);
    Task<Result<GenerateOtherSongDetailsResponse>> GenerateOtherSongDetailsAsync(GenerateOtherSongDetailsRequest request, CancellationToken ct = default);
}
```

`Result<T>` is `Nut.Results` — check `.IsError` / `.Get()`. The methods **return** an error result rather
than throwing for AI/validation failures.

### Constructing the OpenAI implementation

The engine no longer reads `IConfiguration`; you hand it a config POCO directly. There is **no DI
requirement** — just `new` it:

```csharp
using Shuffull.Metadata.Configuration;
using Shuffull.Metadata.Services.AI;

var openAiConfig = new OpenAIConfiguration
{
    ApiKey    = "...",                 // required
    ModelName = "gpt-4o-2024-08-06",   // required; must support JSON-schema structured outputs
    // ApiEndpoint / InstructionFile exist on the POCO but the engine itself only reads ApiKey + ModelName
};

IAIService ai = new OpenAIService(openAiConfig);
```

(The engine uses the OpenAI `ChatClient` with strict JSON-schema structured outputs, so use a model that
supports them.)

### Request/response DTOs

Namespace `Shuffull.Metadata.Models.AI`. The `*Context` strings are **optional free-text** — this is where
the funnel injects its richer per-video context.

```csharp
record GenerateMainGenresRequest(string SongName, List<string> ArtistNames, List<string> MainGenres, string? MainGenresContext = null);
record GenerateMainGenresResponse(List<string> MainGenres);

record GenerateSubGenresRequest(string SongName, List<string> ArtistNames, List<string> SubGenres, string? SubGenresContext = null);
record GenerateSubGenresResponse(List<string> SubGenres);

record GenerateOtherSongDetailsRequest(string SongName, List<string> ArtistNames, string? OtherDetailsContext = null);
record GenerateOtherSongDetailsResponse(string TimePeriod, List<string> Languages);
```

### Reference orchestration

This mirrors exactly what Shuffull does today (`SongImportService.GetGeneratedSongTagsAsync`). Replicate it
on the funnel, substituting your richer context:

```csharp
// 1. MAIN genres — candidates = all main-genre names from the master genre list.
var mainReq = new GenerateMainGenresRequest(songName, artistNames, allMainGenreNames,
    MainGenresContext: $"YouTube Topic Categories: {string.Join(", ", topicCategories)}");
var mainRes = await ai.GenerateMainGenresAsync(mainReq, ct);
if (mainRes.IsError) { /* skip / log; leave GeneratedTags null */ }
var mainGenres = mainRes.Get().MainGenres;

// 2. SUB genres — candidates = sub-genre names belonging to the chosen main genres (from the master list).
var subReq = new GenerateSubGenresRequest(songName, artistNames, subGenreNamesForChosenMains,
    SubGenresContext: $"YouTube Topic Categories: {string.Join(", ", topicCategories)}\nDuration: {durationSeconds} seconds");
var subRes = await ai.GenerateSubGenresAsync(subReq, ct);
var subGenres = subRes.Get().SubGenres;

// 3. ERA + languages.
var otherReq = new GenerateOtherSongDetailsRequest(songName, artistNames,
    OtherDetailsContext: $"Upload Date: {uploadDate:yyyy-MM-dd}\nHashtags: {string.Join(", ", hashtags)}");
var otherRes = await ai.GenerateOtherSongDetailsAsync(otherReq, ct);

// 4. Assemble the payload that goes into SongImportDetails.GeneratedTags.
var tags = new GeneratedSongTags(mainGenres, subGenres, otherRes.Get().Languages, otherRes.Get().TimePeriod);
```

The sub-genre candidate list is derived from the **main genres the model just chose**: for each chosen
main genre, take its `SubGenreNames` from the master genre list.

### Engine behavior / gotchas

- **Main genres are validated against the candidate list** (case-insensitive). The engine returns an error
  result if the model returns a genre not in the provided list, returns more than 3, or returns 0. Pass a
  good candidate list and handle the error result.
- **Sub genres:** at least 1 expected; 0 → error result.
- **Languages:** if the model returns none, the engine fills in `["Instrumental"]`.
- On any error result, the safe behavior is to **omit `GeneratedTags`** (leave it null) and let Shuffull
  fall back to its own generation. Never block the drop on inference.

---

## Master genre list

This is the **allowed vocabulary** the engine must pick from. It must be identical on both sides: when
Shuffull consumes funnel-supplied `GeneratedTags`, it maps the genre strings onto its own `Genres` table.
If the funnel inferred against a different list, the strings won't match and the mapping degrades.

**This is now solved — the canonical list ships *inside* `Shuffull.Metadata`** as an embedded resource, so
both sides reference the same compiled artifact and it cannot drift. There is no file to copy or keep in
sync. Get it from the static helpers on `GenresFile` (namespace `Shuffull.Metadata.Models`):

```csharp
public class GenresFile
{
    public List<MainGenre> MainGenres { get; set; } = [];
    public class MainGenre
    {
        public string Name { get; set; } = "";
        public List<string> SubGenreNames { get; set; } = [];
    }

    public static string      CanonicalJson  { get; }   // raw embedded JSON
    public static GenresFile  LoadCanonical();           // deserialized (via Newtonsoft)
}
```

So on the funnel side, build your candidate lists straight from the shared list — no local genres file:

```csharp
var genres = GenresFile.LoadCanonical();
var allMainGenreNames = genres.MainGenres.Select(g => g.Name).ToList();
// after the model picks main genres, gather the sub-genre candidates for those mains:
var subGenreNamesForChosenMains = genres.MainGenres
    .Where(g => chosenMainGenres.Contains(g.Name))
    .SelectMany(g => g.SubGenreNames)
    .Distinct()
    .ToList();
```

These feed `GenerateMainGenresRequest.MainGenres` and `GenerateSubGenresRequest.SubGenres` in the
[reference orchestration](#reference-orchestration) above.

**How Shuffull uses it:** Shuffull seeds its `Genres` table from a local `genres.json` file so a deployment
can override the list. That file is now *seeded from* `GenresFile.CanonicalJson` on first run (an existing
file is left untouched), so a fresh Shuffull starts with the identical vocabulary. Changing the canonical
list = editing `Resources/genres.json` in `Shuffull.Metadata` and bumping the version both sides consume.

---

## What Shuffull does with the field — now vs. later

| Stage | Shuffull behavior |
| --- | --- |
| **Now** (contract field + DB column added) | Accepts `GeneratedTags` on the wire; has a nullable `SongImport.GeneratedTagsJson` column. Does **not** yet read the field or persist it. Sending it is harmless and forward-compatible. |
| **Later** (separately coordinated) | `ExternalSongImporterService` persists incoming `GeneratedTags` into `GeneratedTagsJson`, and `SongImportService` prefers it over making its own AI call (and the YouTube re-fetch Shuffull currently does only to build AI context can be dropped). |

So: the funnel can build and start emitting `GeneratedTags` independently and ahead of the Shuffull
consumption work.

---

## Funnel-side checklist

- [ ] Bump the funnel project(s) to **net8.0+**.
- [ ] Reference `Shuffull.Metadata` (submodule or NuGet).
- [ ] Replace the funnel's local `SongExportDetails` with `Shuffull.Metadata.Contracts.SongImportDetails`;
      serialize the drop with `Newtonsoft.Json` (PascalCase, `ExternalSource` = `1`).
- [ ] Keep the existing two-file drop (audio named `{ExternalSongId}{FileExtension}` + a `.json`).
- [ ] (Optional, the valuable part) Build candidate lists from `GenresFile.LoadCanonical()` (the shared
      embedded list), run the 3-call engine orchestration with funnel context, and attach the resulting
      `GeneratedSongTags` as `GeneratedTags`. On any error, leave it null.
- [ ] Provide an `OpenAIConfiguration` (ApiKey + ModelName at minimum) and a structured-output-capable model.

---

*Questions / contract changes: the canonical types live in `Shuffull.Metadata` — change them there once,
and both sides move together. Do not reintroduce a hand-copied record on the funnel side.*
