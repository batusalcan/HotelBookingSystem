using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAgentService.Providers;

/// <summary>
/// Calls the Google Gemini REST API (generativelanguage.googleapis.com).
/// To swap providers: implement IAiProvider and update DI registration in Program.cs — zero business logic changes required.
/// </summary>
public class GeminiAiProvider(HttpClient http, IConfiguration config, ILogger<GeminiAiProvider> logger)
    : IAiProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <precondition>prompt is non-empty; AI:ApiKey and AI:ModelName are configured</precondition>
    /// <postcondition>Returns the model's text response; throws HttpRequestException on API failure</postcondition>
    public async Task<string> GenerateAsync(string prompt)
    {
        var apiKey = config["AI:ApiKey"]
            ?? throw new InvalidOperationException("AI:ApiKey is not configured.");
        var model = config["AI:ModelName"] ?? "gemini-1.5-flash";

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = prompt } } }
            },
            generationConfig = new { responseMimeType = "application/json" }
        };

        var response = await http.PostAsJsonAsync(url, requestBody, JsonOpts);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(raw, JsonOpts)
            ?? throw new InvalidOperationException("Gemini returned an empty response.");

        var text = geminiResponse.Candidates?[0]?.Content?.Parts?[0]?.Text
            ?? throw new InvalidOperationException("Gemini response contained no text.");

        logger.LogDebug("Gemini response: {Text}", text);
        return text;
    }

    // ── Internal deserialization types ───────────────────────────────────────
    private record GeminiResponse(
        [property: JsonPropertyName("candidates")] GeminiCandidate[]? Candidates);

    private record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content);

    private record GeminiContent(
        [property: JsonPropertyName("parts")] GeminiPart[]? Parts);

    private record GeminiPart(
        [property: JsonPropertyName("text")] string? Text);
}
