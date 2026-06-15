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
                "Based on the song info, determine between 1 and 3 main genres that best fit the song, only from the list provided.\n" +
                "Validate your work."),
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
        var messages = new List<ChatMessage>()
        {
            new UserChatMessage(
                "You will be provided a song name, its artist(s), and optional context.\n" +
                "You do NOT have internet access. Infer everything from your own knowledge and the provided context only.\n" +
                "Return the prominent languages used in the lyrics, and the song's original release time period.\n" +
                "Always give your single best estimate. Never refuse, never say you cannot verify, and never mention browsing, the internet, or needing more information.\n" +
                "If the song is instrumental (no lyrics), return exactly [\"Instrumental\"] for the languages.\n" +
                "Each language must be a single bare language name such as \"Japanese\" or \"English\" - no sentences, no notes.\n" +
                "For the time period, return only a decade if post-1900 (such as \"1930s\" or \"2010s\"), otherwise only a century (such as \"1700s\" or \"1800s\") - a single token, no extra words.\n" +
                "If you are unsure, pick the most likely option anyway."),
            new AssistantChatMessage("Understood. Send the information."),
            new UserChatMessage(
                $"Song name: {request.SongName}\n" +
                $"Artist(s): {string.Join(",", request.ArtistNames)}\n" +
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
                            }
                        },
                        "required": ["timePeriod", "languages"],
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

            return result with { TimePeriod = cleanTimePeriod, Languages = cleanLanguages };
        }
        catch (Exception e)
        {
            return Result.Error<GenerateOtherSongDetailsResponse>(e);
        }
    }
}
