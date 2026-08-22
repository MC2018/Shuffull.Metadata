using System.Text.Json.Nodes;
using System.ClientModel;
using Newtonsoft.Json;
using Nut.Results;
using OpenAI;
using OpenAI.Chat;
using Shuffull.Metadata.Configuration;
using Shuffull.Metadata.Models.AI;

namespace Shuffull.Metadata.Services.AI;

public class OpenAIService(OpenAIConfiguration config) : IAIService
{
    private readonly OpenAIConfiguration _config = config;

    /// <summary>
    /// The single place a chat client is built, so the configured host applies to every call rather than to
    /// whichever ones remembered to opt in. An empty BaseUrl keeps the SDK's own default endpoint (OpenAI).
    /// </summary>
    /// <summary>
    /// Batch is OpenAI's own endpoint, so it is available only when this instance points at OpenAI itself.
    /// Redirected at an OpenAI-COMPATIBLE vendor (BaseUrl set), the chat calls still work and batching does not.
    /// </summary>
    public bool SupportsBatch => string.IsNullOrWhiteSpace(_config.BaseUrl);

    private ChatClient CreateChatClient(string model)
    {
        if (string.IsNullOrWhiteSpace(_config.BaseUrl))
        {
            return new ChatClient(model: model, apiKey: _config.ApiKey);
        }

        return new ChatClient(
            model: model,
            credential: new ApiKeyCredential(_config.ApiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri(_config.BaseUrl) });
    }

    // ── Shared plumbing so the SYNCHRONOUS and BATCH paths cannot drift ──────────────────────────────
    //
    // Each call is defined once as (prompt, schema, parser) and consumed two ways: the live path turns the
    // prompt into ChatMessages and calls the API; the OpenAI Batch path serialises the same prompt into a
    // chat-completions request body for a JSONL line. Both then run the SAME parser, so a batched result gets
    // the identical validation (vocabulary membership, count caps, era normalisation) as a live one. Anything
    // duplicated between the two would silently diverge the moment a prompt changed.

    /// <summary>The fixed assistant turn between the instructions and the payload.</summary>
    private const string Acknowledgement = "Understood. Send the information.";

    /// <summary>A call's two user turns: standing instructions, then the per-song payload.</summary>
    private sealed record Prompt(string Instructions, string Payload);

    private static List<ChatMessage> ToMessages(Prompt prompt) =>
    [
        new UserChatMessage(prompt.Instructions),
        new AssistantChatMessage(Acknowledgement),
        new UserChatMessage(prompt.Payload),
    ];

    private static ChatCompletionOptions ToOptions(string schemaName, string schemaJson) => new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            jsonSchemaFormatName: schemaName,
            jsonSchema: BinaryData.FromString(schemaJson),
            jsonSchemaIsStrict: true),
    };

    /// <summary>
    /// The chat-completions request body for a Batch API JSONL line - the wire equivalent of
    /// <see cref="ToMessages"/> + <see cref="ToOptions"/>. Hand-built rather than round-tripped through the SDK
    /// because the SDK does not expose the serialised request, and guessing at its internals would be the kind
    /// of thing that breaks silently on an upgrade.
    /// </summary>
    private static string ToBatchBody(Prompt prompt, string model, string schemaName, string schemaJson)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = prompt.Instructions },
                new JsonObject { ["role"] = "assistant", ["content"] = Acknowledgement },
                new JsonObject { ["role"] = "user", ["content"] = prompt.Payload },
            },
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = schemaName,
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(schemaJson),
                },
            },
        };
        return body.ToJsonString();
    }

    /// <summary>The model a call runs on: an explicit override, else the configured strong model.</summary>
    private string ModelFor(string? modelOverride) => modelOverride ?? _config.ResolvedStrongModelName;

    // ── Main genres ─────────────────────────────────────────────────────────────────────────────────

    private const string MainGenresSchemaName = "main_genres_response";
    private const string MainGenresSchemaJson = """
        {
            "type": "object",
            "properties": {
                "mainGenres": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            },
            "required": ["mainGenres"],
            "additionalProperties": false
        }
        """;

    private static Prompt MainGenresPrompt(GenerateMainGenresRequest request) => new(
        "You will be provided a song name, its artist(s), and list of main genres.\n" +
        "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
        "Based on the song info, determine between 1 and 3 main genres that best fit the song, only from the list provided.\n" +
        "Always give your best estimate. Never refuse, never ask for clarification, never say you cannot verify, and never mention browsing or the internet.",
        $"Song name: {request.SongName}\n" +
        $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
        $"Main Genres: {string.Join(", ", request.MainGenres)}" +
        (string.IsNullOrWhiteSpace(request.MainGenresContext) ? "" : $"\n\nAdditional context:\n{request.MainGenresContext}"));

    public string BuildMainGenresBatchBody(GenerateMainGenresRequest request, string? modelOverride = null) =>
        ToBatchBody(MainGenresPrompt(request), ModelFor(modelOverride), MainGenresSchemaName, MainGenresSchemaJson);

    /// <summary>
    /// Validates and normalises a main-genres completion. Shared by the live and batch paths so a batched
    /// result is held to the same standard: the model periodically ignores "from the list only" and the strict
    /// schema then ships the off-list value verbatim.
    /// </summary>
    public Result<GenerateMainGenresResponse> ParseMainGenres(string content, GenerateMainGenresRequest request)
    {
        try
        {
            var result = JsonConvert.DeserializeObject<GenerateMainGenresResponse>(content);

            if (result == null)
            {
                throw new Exception("Failed to parse MainGenresResponse from AI response.");
            }
            else if (result.MainGenres.Count > 3)
            {
                throw new Exception($"AI returned more than 3 main genres: {string.Join(", ", result.MainGenres)}. Please ensure the AI is configured correctly to return a maximum of 3 main genres.");
            }
            else if (result.MainGenres.Count == 0)
            {
                throw new Exception("AI returned no genres. Please ensure the AI is configured correctly to return at least one genre.");
            }
            else if (result.MainGenres.Any(x => !request.MainGenres.Contains(x, StringComparer.OrdinalIgnoreCase)))
            {
                throw new Exception(
                    $"AI returned main genres that were not in the provided list: {string.Join(", ", result.MainGenres.Where(x => !request.MainGenres.Contains(x, StringComparer.OrdinalIgnoreCase)))}. " +
                    $"Please ensure the AI is configured correctly to return only main genres from the provided list.");
            }
            return result;
        }
        catch (Exception e)
        {
            return Result.Error<GenerateMainGenresResponse>(e);
        }
    }

    public async Task<Result<GenerateMainGenresResponse>> GenerateMainGenresAsync(GenerateMainGenresRequest request, CancellationToken cancellationToken = default!)
    {
        try
        {
            var client = CreateChatClient(ModelFor(request.ModelOverride));
            var completion = (await client.CompleteChatAsync(
                ToMessages(MainGenresPrompt(request)),
                ToOptions(MainGenresSchemaName, MainGenresSchemaJson),
                cancellationToken)).Value;
            return ParseMainGenres(completion.Content[0].Text, request);
        }
        catch (Exception e)
        {
            return Result.Error<GenerateMainGenresResponse>(e);
        }
    }

    // ── Sub-genres ──────────────────────────────────────────────────────────────────────────────────

    private const string SubGenresSchemaName = "sub_genres_response";
    private const string SubGenresSchemaJson = """
        {
            "type": "object",
            "properties": {
                "subGenres": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            },
            "required": ["subGenres"],
            "additionalProperties": false
        }
        """;

    private static Prompt SubGenresPrompt(GenerateSubGenresRequest request) => new(
        "You will be provided a song name, its artist(s), and a list of sub-genres.\n" +
        "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
        "Choose the sub-genres from the list that best describe the song - only sub-genres from the list provided.\n" +
        "Always give your best estimate. Never refuse, never ask for clarification, never say you cannot verify, and never mention browsing or the internet.\n" +
        "Provide at least one if any reasonably fit; only return an empty list if none of the listed sub-genres apply.",
        $"Song name: {request.SongName}\n" +
        $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
        $"Sub-Genres: {string.Join(", ", request.SubGenres)}" +
        (string.IsNullOrWhiteSpace(request.SubGenresContext) ? "" : $"\n\nAdditional context:\n{request.SubGenresContext}"));

    public string BuildSubGenresBatchBody(GenerateSubGenresRequest request, string? modelOverride = null) =>
        ToBatchBody(SubGenresPrompt(request), ModelFor(modelOverride), SubGenresSchemaName, SubGenresSchemaJson);

    /// <summary>
    /// Validates and normalises a sub-genres completion. Shared by the live and batch paths.
    ///
    /// Post-parse guard, mirroring the main-genre membership check. The model occasionally ignores the
    /// "from the list only" instruction and returns a refusal/placeholder token ("clarification_requested")
    /// or a near-miss name; the strict JSON schema then ships that junk verbatim. Keep only values that are
    /// actually in the provided candidate list, dropping anything off-list rather than throwing - sub-genres
    /// are a secondary signal, and an empty list is a valid "none of these fit" outcome that must not nuke
    /// the otherwise-good main genres / era / language tags the orchestrator attaches alongside them.
    /// </summary>
    public Result<GenerateSubGenresResponse> ParseSubGenres(string content, GenerateSubGenresRequest request)
    {
        try
        {
            var result = JsonConvert.DeserializeObject<GenerateSubGenresResponse>(content);

            if (result == null)
            {
                throw new Exception("Failed to parse SubGenresResponse from AI response.");
            }

            var cleanSubGenres = result.SubGenres
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Where(s => request.SubGenres.Contains(s, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return result with { SubGenres = cleanSubGenres };
        }
        catch (Exception e)
        {
            return Result.Error<GenerateSubGenresResponse>(e);
        }
    }

    public async Task<Result<GenerateSubGenresResponse>> GenerateSubGenresAsync(GenerateSubGenresRequest request, CancellationToken cancellationToken = default!)
    {
        try
        {
            var client = CreateChatClient(ModelFor(request.ModelOverride));
            var completion = (await client.CompleteChatAsync(
                ToMessages(SubGenresPrompt(request)),
                ToOptions(SubGenresSchemaName, SubGenresSchemaJson),
                cancellationToken)).Value;
            return ParseSubGenres(completion.Content[0].Text, request);
        }
        catch (Exception e)
        {
            return Result.Error<GenerateSubGenresResponse>(e);
        }
    }

    // ── Other song details (era, languages, moods, energy, tempo, themes) ───────────────────────────

    private static Prompt OtherSongDetailsPrompt(GenerateOtherSongDetailsRequest request)
    {
        // Null candidate list = mood inference disabled (e.g. older callers that don't supply one). Energy is
        // independent, so it is still requested either way.
        var candidateMoods = request.CandidateMoods ?? [];
        var candidateThemes = request.CandidateThemes ?? [];
        return new Prompt(
                "You will be provided a song name, its artist(s), a list of candidate moods, a list of candidate themes, and optional context.\n" +
                "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
                "Return the prominent languages used in the lyrics, the song's original release time period, the song's mood(s), an energy level, the song's true tempo (with a recognition flag), and any matching theme(s).\n" +
                "Always give your single best estimate. Never refuse, never say you cannot verify, and never mention browsing, the internet, or needing more information.\n" +
                "If the song is instrumental (no lyrics), return exactly [\"Instrumental\"] for the languages.\n" +
                "If lyrics are included in the additional context, use them as the definitive source for the languages and a strong signal for the mood(s).\n" +
                "Each language must be a single bare language name such as \"Japanese\" or \"English\" - no sentences, no notes.\n" +
                "For the time period, you MUST ALWAYS return a valid era token and NEVER an empty string. If you are unsure of the exact era, give your best-guess decade from the genre/style/artist (most modern electronic, EDM, and video-game music is \"2010s\" or \"2020s\"). Return a decade if post-1900 (such as \"1930s\" or \"2010s\"), otherwise a century (such as \"1700s\" or \"1800s\") - a single token ending in \"0s\", no extra words, never blank.\n" +
                "For moods, choose between 1 and 3 that best fit the song, ONLY from the provided candidate mood list - copy the names exactly. If none fit well, return an empty list.\n" +
                "For trueBpm and bpmRecognized: a rough measured tempo may be provided, but automatic beat trackers are unreliable on dense electronic music and are frequently wrong (not just by a clean half/double). If you GENUINELY recognise this specific track and know its real production tempo, return that exact BPM as trueBpm and set bpmRecognized=true. Otherwise estimate the most likely tempo from the genre conventions and the measured hint, return it as trueBpm, and set bpmRecognized=false. trueBpm must be a whole number, normally 60-300. If no measured tempo is given and you don't recognise the track, you may still give your best genre-based estimate with bpmRecognized=false.\n" +
                "For energy, return a single integer from 1 to 10 describing the song's overall intensity/drive (1 = calm, sparse, gentle; 10 = intense, fast, hard-hitting). Weigh the true tempo heavily - but a fast tempo alone is not high energy if the arrangement is sparse or gentle.\n" +
                "For themes, return between 0 and 2 ONLY from the provided candidate theme list - copy the names exactly. Themes are origin/relationship labels (e.g. Anime, Vocaloid, Cover, Parody, Christmas), NOT sounds; pick one ONLY when you are confident the song genuinely is that (an anime tie-in, a Vocaloid production, a cover, a parody, etc.). Most songs match no theme - return an empty list whenever in doubt.\n" +
                "If you are unsure, pick the most likely option anyway.",
                $"Song name: {request.SongName}\n" +
                $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
                $"Candidate moods: {string.Join(", ", candidateMoods)}\n" +
                $"Candidate themes: {string.Join(", ", candidateThemes)}\n" +
                (request.MeasuredBpm is int measuredBpm ? $"Measured tempo (rough, often unreliable for electronic music): {measuredBpm} BPM\n" : "") +
                (string.IsNullOrWhiteSpace(request.OtherDetailsContext) ? "" : $"\nAdditional context:\n{request.OtherDetailsContext}"));
    }

    private const string OtherSongDetailsSchemaName = "other_song_details_response";
    private const string OtherSongDetailsSchemaJson = """
        {
            "type": "object",
            "properties": {
                "timePeriod": { "type": "string" },
                "languages": {
                    "type": "array",
                    "items": { "type": "string" }
                },
                "moods": {
                    "type": "array",
                    "items": { "type": "string" }
                },
                "energy": { "type": "integer" },
                "trueBpm": { "type": ["integer", "null"] },
                "bpmRecognized": { "type": "boolean" },
                "themes": {
                    "type": "array",
                    "items": { "type": "string" }
                }
            },
            "required": ["timePeriod", "languages", "moods", "energy", "trueBpm", "bpmRecognized", "themes"],
            "additionalProperties": false
        }
        """;

    public string BuildOtherSongDetailsBatchBody(GenerateOtherSongDetailsRequest request, string? modelOverride = null) =>
        ToBatchBody(OtherSongDetailsPrompt(request), ModelFor(modelOverride), OtherSongDetailsSchemaName, OtherSongDetailsSchemaJson);

    /// <summary>
    /// Validates and normalises an other-details completion. Shared by the live and batch paths.
    ///
    /// A Result.Error here means "regenerate": the live path retries immediately, and the batch path leaves the
    /// request queued for a later batch (bounded by AI:Batch MaxAttempts) rather than shipping a bad era. That
    /// matters most for TimePeriod, which is REQUIRED - an empty one becomes an empty-named tag on import.
    /// </summary>
    public Result<GenerateOtherSongDetailsResponse> ParseOtherSongDetails(string content, GenerateOtherSongDetailsRequest request)
    {
        var candidateMoods = request.CandidateMoods ?? [];
        var candidateThemes = request.CandidateThemes ?? [];

        try
        {
            var result = JsonConvert.DeserializeObject<GenerateOtherSongDetailsResponse>(content);

            if (result == null)
            {
                throw new Exception("Failed to parse OtherSongDetailsResponse from AI response.");
            }

            // Post-parse guard. The model occasionally ignores the "best guess, no browsing" instruction and
            // returns a refusal/disclaimer ("Unable to verify - I do not have browsing access ...") instead
            // of a value; the strict JSON schema then forces that prose into the string fields. Sanitize so
            // a refusal never reaches the hand-off contract.
            var rawLanguageCount = result.Languages.Count;

            var cleanTimePeriod = TimePeriodFormat.Normalize(result.TimePeriod);
            if (cleanTimePeriod is null)
            {
                throw new Exception($"AI returned no valid time period (raw: '{result.TimePeriod}').");
            }

            // Languages: drop anything that isn't a plausible bare language name. Real names are short and at
            // most a couple of words ("Japanese", "Mandarin Chinese"); refusal prose is long and many-worded.
            var cleanLanguages = result.Languages
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Where(l => l.Length <= 30 && l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
                .ToList();

            // Only treat a genuinely-empty model response as instrumental. If the model DID return languages
            // but they were all prose (filtered to empty), that was a refusal, not an instrumental track -
            // leave it blank instead of mislabeling it.
            if (cleanLanguages.Count == 0 && rawLanguageCount == 0)
            {
                cleanLanguages.Add("Instrumental");
            }

            // Moods: keep only values that are actually in the provided candidate list (mirrors the sub-genre
            // membership guard), trimmed and de-duplicated, capped at 3. An empty list is a valid "none fit".
            var cleanMoods = (result.Moods ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Where(m => candidateMoods.Contains(m, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            // Energy: accept only a sane 1-10 scalar; null out anything outside that range so a bad value
            // never reaches the hand-off contract.
            var cleanEnergy = result.Energy is >= 1 and <= 10 ? result.Energy : null;

            // True tempo: keep only a plausible musical value (coarse sanity gate against a wild number). The
            // caller (BpmResolver) decides whether to trust it over the measurement based on bpmRecognized.
            var cleanTrueBpm = result.TrueBpm is >= 40 and <= 300 ? result.TrueBpm : null;

            // Themes: keep only values actually in the provided candidate list (membership guard, like moods),
            // trimmed and de-duplicated, capped at 2. Empty is the common, valid "no theme".
            var cleanThemes = (result.Themes ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => candidateThemes.Contains(t, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

            return result with { TimePeriod = cleanTimePeriod, Languages = cleanLanguages, Moods = cleanMoods, Energy = cleanEnergy, Themes = cleanThemes, TrueBpm = cleanTrueBpm };
        }
        catch (Exception e)
        {
            return Result.Error<GenerateOtherSongDetailsResponse>(e);
        }
    }

    // TODO: potentially include metadata as an optional field in the database when importing, and include things like upload date, etc.
    public async Task<Result<GenerateOtherSongDetailsResponse>> GenerateOtherSongDetailsAsync(GenerateOtherSongDetailsRequest request, CancellationToken cancellationToken = default!)
    {
        var messages = ToMessages(OtherSongDetailsPrompt(request));
        var options = ToOptions(OtherSongDetailsSchemaName, OtherSongDetailsSchemaJson);

        // The time period is REQUIRED (an empty one becomes an empty-named tag on import), but the model
        // occasionally omits it. Regenerate up to a few times and only fail the whole call if it never produces
        // a valid decade/century. A Result.Error is not cached by the caller, so a later pass also retries.
        const int maxAttempts = 3;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var client = CreateChatClient(ModelFor(request.ModelOverride));
                var completion = (await client.CompleteChatAsync(messages, options, cancellationToken)).Value;
                var parsed = ParseOtherSongDetails(completion.Content[0].Text, request);

                if (parsed.IsOk)
                {
                    return parsed;
                }

                // Shared validation rejected it (most often a missing/unparseable era) - retry.
                lastError = parsed.GetError();
            }
            catch (Exception e)
            {
                lastError = e;
            }
        }

        return Result.Error<GenerateOtherSongDetailsResponse>(
            lastError ?? new Exception("Other-details generation failed to produce a valid time period."));
    }
}
