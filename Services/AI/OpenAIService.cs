using Newtonsoft.Json;
using Nut.Results;
using OpenAI.Chat;
using Shuffull.Metadata.Configuration;
using Shuffull.Metadata.Models.AI;

namespace Shuffull.Metadata.Services.AI;

public class OpenAIService(OpenAIConfiguration config) : IAIService
{
    private readonly OpenAIConfiguration _config = config;

    public async Task<Result<GenerateMainGenresResponse>> GenerateMainGenresAsync(GenerateMainGenresRequest request, CancellationToken cancellationToken = default!)
    {
        var messages = new List<ChatMessage>()
        {
            new UserChatMessage(
                "You will be provided a song name, its artist(s), and list of main genres.\n" +
                "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
                "Based on the song info, determine between 1 and 3 main genres that best fit the song, only from the list provided.\n" +
                "Always give your best estimate. Never refuse, never ask for clarification, never say you cannot verify, and never mention browsing or the internet."),
            new AssistantChatMessage("Understood. Send the information."),
            new UserChatMessage(
                $"Song name: {request.SongName}\n" +
                $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
                $"Main Genres: {string.Join(", ", request.MainGenres)}" +
                (string.IsNullOrWhiteSpace(request.MainGenresContext) ? "" : $"\n\nAdditional context:\n{request.MainGenresContext}")),
        };
        var options = new ChatCompletionOptions()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat
            (
                jsonSchemaFormatName: "main_genres_response",
                jsonSchema: BinaryData.FromBytes("""
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
                 """u8.ToArray()),
                jsonSchemaIsStrict: true
            )
        };

        try
        {
            var client = new ChatClient(model: _config.ModelName, apiKey: _config.ApiKey);
            var completion = (await client.CompleteChatAsync(messages, options, cancellationToken)).Value;
            var resultStr = completion.Content[0].Text;
            var result = JsonConvert.DeserializeObject<GenerateMainGenresResponse>(resultStr);

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

    public async Task<Result<GenerateSubGenresResponse>> GenerateSubGenresAsync(GenerateSubGenresRequest request, CancellationToken cancellationToken = default!)
    {
        var messages = new List<ChatMessage>()
        {
            new UserChatMessage(
                "You will be provided a song name, its artist(s), and a list of sub-genres.\n" +
                "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
                "Choose the sub-genres from the list that best describe the song - only sub-genres from the list provided.\n" +
                "Always give your best estimate. Never refuse, never ask for clarification, never say you cannot verify, and never mention browsing or the internet.\n" +
                "Provide at least one if any reasonably fit; only return an empty list if none of the listed sub-genres apply."),
            new AssistantChatMessage("Understood. Send the information."),
            new UserChatMessage(
                $"Song name: {request.SongName}\n" +
                $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
                $"Sub-Genres: {string.Join(", ", request.SubGenres)}" +
                (string.IsNullOrWhiteSpace(request.SubGenresContext) ? "" : $"\n\nAdditional context:\n{request.SubGenresContext}")),
        };
        var options = new ChatCompletionOptions()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat
            (
                jsonSchemaFormatName: "sub_genres_response",
                jsonSchema: BinaryData.FromBytes("""
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
                 """u8.ToArray()),
                jsonSchemaIsStrict: true
            )
        };

        try
        {
            var client = new ChatClient(model: _config.ModelName, apiKey: _config.ApiKey);
            var completion = (await client.CompleteChatAsync(messages, options, cancellationToken)).Value;
            var resultStr = completion.Content[0].Text;
            var result = JsonConvert.DeserializeObject<GenerateSubGenresResponse>(resultStr);

            if (result == null)
            {
                throw new Exception("Failed to parse SubGenresResponse from AI response.");
            }

            // Post-parse guard, mirroring the main-genre membership check. The model occasionally ignores the
            // "from the list only" instruction and returns a refusal/placeholder token ("clarification_requested")
            // or a near-miss name; the strict JSON schema then ships that junk verbatim. Keep only values that are
            // actually in the provided candidate list, dropping anything off-list rather than throwing - sub-genres
            // are a secondary signal, and an empty list is a valid "none of these fit" outcome that must not nuke
            // the otherwise-good main genres / era / language tags the orchestrator attaches alongside them.
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

    // TODO: potentially include metadata as an optional field in the database when importing, and include things like upload date, etc.
    public async Task<Result<GenerateOtherSongDetailsResponse>> GenerateOtherSongDetailsAsync(GenerateOtherSongDetailsRequest request, CancellationToken cancellationToken = default!)
    {
        // Null candidate list = mood inference disabled (e.g. older callers that don't supply one). Energy is
        // independent, so it is still requested either way.
        var candidateMoods = request.CandidateMoods ?? [];
        var candidateThemes = request.CandidateThemes ?? [];
        var messages = new List<ChatMessage>()
        {
            new UserChatMessage(
                "You will be provided a song name, its artist(s), a list of candidate moods, a list of candidate themes, and optional context.\n" +
                "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
                "Return the prominent languages used in the lyrics, the song's original release time period, the song's mood(s), an energy level, a corrected tempo, and any matching theme(s).\n" +
                "Always give your single best estimate. Never refuse, never say you cannot verify, and never mention browsing, the internet, or needing more information.\n" +
                "If the song is instrumental (no lyrics), return exactly [\"Instrumental\"] for the languages.\n" +
                "If lyrics are included in the additional context, use them as the definitive source for the languages and a strong signal for the mood(s).\n" +
                "Each language must be a single bare language name such as \"Japanese\" or \"English\" - no sentences, no notes.\n" +
                "For the time period, return only a decade if post-1900 (such as \"1930s\" or \"2010s\"), otherwise only a century (such as \"1700s\" or \"1800s\") - a single token, no extra words.\n" +
                "For moods, choose between 1 and 3 that best fit the song, ONLY from the provided candidate mood list - copy the names exactly. If none fit well, return an empty list.\n" +
                "For correctedBpm: a measured tempo (BPM) may be provided. Automatic beat trackers commonly report HALF or DOUBLE the true tempo (and occasionally a 3:2 ratio), so it is often wrong by an octave - e.g. a drum & bass track measured at 87 is really 174, a dubstep track measured at 70 is really 140. Using the genre and your knowledge of the track, return the song's TRUE tempo as correctedBpm; it MUST be the measured value, or its half, double, 3:2 (x1.5), or 2:3 (/1.5), rounded to a whole number. If no measured tempo is provided, return null for correctedBpm.\n" +
                "For energy, return a single integer from 1 to 10 describing the song's overall intensity/drive (1 = calm, sparse, gentle; 10 = intense, fast, hard-hitting). Weigh the TRUE (corrected) tempo heavily - but a fast tempo alone is not high energy if the arrangement is sparse or gentle.\n" +
                "For themes, return between 0 and 2 ONLY from the provided candidate theme list - copy the names exactly. Themes are origin/relationship labels (e.g. Anime, Vocaloid, Cover, Parody, Christmas), NOT sounds; pick one ONLY when you are confident the song genuinely is that (an anime tie-in, a Vocaloid production, a cover, a parody, etc.). Most songs match no theme - return an empty list whenever in doubt.\n" +
                "If you are unsure, pick the most likely option anyway."),
            new AssistantChatMessage("Understood. Send the information."),
            new UserChatMessage(
                $"Song name: {request.SongName}\n" +
                $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
                $"Candidate moods: {string.Join(", ", candidateMoods)}\n" +
                $"Candidate themes: {string.Join(", ", candidateThemes)}\n" +
                (request.MeasuredBpm is int measuredBpm ? $"Measured tempo (may be an octave error): {measuredBpm} BPM\n" : "") +
                (string.IsNullOrWhiteSpace(request.OtherDetailsContext) ? "" : $"\nAdditional context:\n{request.OtherDetailsContext}")),
        };
        var options = new ChatCompletionOptions()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat
            (
                jsonSchemaFormatName: "other_song_details_response",
                jsonSchema: BinaryData.FromBytes("""
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
                            "correctedBpm": { "type": ["integer", "null"] },
                            "themes": {
                                "type": "array",
                                "items": { "type": "string" }
                            }
                        },
                        "required": ["timePeriod", "languages", "moods", "energy", "correctedBpm", "themes"],
                        "additionalProperties": false
                    }
                 """u8.ToArray()),
                jsonSchemaIsStrict: true
            )
        };

        try
        {
            var client = new ChatClient(model: _config.ModelName, apiKey: _config.ApiKey);
            var completion = (await client.CompleteChatAsync(messages, options, cancellationToken)).Value;
            var resultStr = completion.Content[0].Text;
            var result = JsonConvert.DeserializeObject<GenerateOtherSongDetailsResponse>(resultStr);

            if (result == null)
            {
                throw new Exception("Failed to parse OtherSongDetailsResponse from AI response.");
            }

            // Post-parse guard. The model occasionally ignores the "best guess, no browsing" instruction and
            // returns a refusal/disclaimer ("Unable to verify - I do not have browsing access ...") instead of
            // a value; the strict JSON schema then forces that prose into the string fields. Sanitize so a
            // refusal never reaches the hand-off contract.
            var rawLanguageCount = result.Languages.Count;

            // TimePeriod: keep only a decade/century token (ends in "0s"); salvage one embedded in prose
            // (e.g. "2020s (cannot verify ...)" -> "2020s"), otherwise blank it rather than ship a sentence.
            var periodMatch = System.Text.RegularExpressions.Regex.Match(result.TimePeriod ?? "", @"\b\d{3,4}0s\b");
            var cleanTimePeriod = periodMatch.Success ? periodMatch.Value : "";

            // Languages: drop anything that isn't a plausible bare language name. Real names are short and at
            // most a couple of words ("Japanese", "Mandarin Chinese"); refusal prose is long and many-worded.
            var cleanLanguages = result.Languages
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Where(l => l.Length <= 30 && l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
                .ToList();

            // Only treat a genuinely-empty model response as instrumental. If the model DID return languages
            // but they were all prose (filtered to empty), that was a refusal, not an instrumental track - leave
            // it blank instead of mislabeling it.
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

            // Energy: accept only a sane 1-10 scalar; null out anything outside that range so a bad value never
            // reaches the hand-off contract.
            var cleanEnergy = result.Energy is >= 1 and <= 10 ? result.Energy : null;

            // Corrected tempo: keep only a plausible musical value. The caller (BpmOctaveCorrection.Resolve)
            // still snaps this back to a real octave of the measured tempo before trusting it, so this is just a
            // coarse sanity gate against a wild number.
            var cleanCorrectedBpm = result.CorrectedBpm is >= 20 and <= 400 ? result.CorrectedBpm : null;

            // Themes: keep only values actually in the provided candidate list (membership guard, like moods),
            // trimmed and de-duplicated, capped at 2. Empty is the common, valid "no theme".
            var cleanThemes = (result.Themes ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Where(t => candidateThemes.Contains(t, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

            return result with { TimePeriod = cleanTimePeriod, Languages = cleanLanguages, Moods = cleanMoods, Energy = cleanEnergy, Themes = cleanThemes, CorrectedBpm = cleanCorrectedBpm };
        }
        catch (Exception e)
        {
            return Result.Error<GenerateOtherSongDetailsResponse>(e);
        }
    }
}
