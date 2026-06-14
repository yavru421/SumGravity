using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SumGravity.Models;

namespace SumGravity.Services;

public class KoboldCppClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<KoboldCppClient> _logger;

    private const string DevSystemPrompt = """
        You are SumGravity, an expert C# and Blazor development assistant running locally on John's machine.
        John is a General Contractor who does not write code — you write everything for him.
        
        ## Search/Replace Protocol
        When modifying existing files, ALWAYS use the Search/Replace block format:
        
        ```
        <<<<<<< SEARCH
        :path: relative/path/to/file.cs
        [exact lines to find — must match verbatim]
        =======
        [replacement lines]
        >>>>>>> REPLACE
        ```
        
        Rules:
        - The SEARCH block must match the file content EXACTLY (whitespace, indentation).
        - Use one block per logical change. Do NOT rewrite entire files.
        - If creating a NEW file, use: `<<<<<<< NEW_FILE :path: relative/path/to/file.cs` then content then `>>>>>>> END_FILE`
        - Always be concise. Local 8B models have limited context — avoid verbosity.
        """;

    public KoboldCppClient(HttpClient http, IConfiguration config, ILogger<KoboldCppClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public int MaxTokens => _config.GetValue<int>("KoboldCpp:MaxTokens", 2048);
    public float Temperature => _config.GetValue<float>("KoboldCpp:Temperature", 0.3f);

    // ── Non-streaming completion ───────────────────────────────
    public async Task<string> CompleteAsync(
        List<ChatMessage> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var payload = BuildPayload(history, userMessage, stream: false);
        var response = await _http.PostAsJsonAsync("chat/completions", payload, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct);
        return json?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    // ── Streaming completion (SSE) ─────────────────────────────
    public async IAsyncEnumerable<string> StreamAsync(
        List<ChatMessage> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = BuildPayload(history, userMessage, stream: true);

        var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

        HttpResponseMessage? response = null;
        string? connectionError = null;
        try
        {
            response = await _http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KoboldCPP connection failed");
            connectionError = ex.Message;
        }

        if (connectionError is not null)
        {
            yield return $"\n\n⚠️ **KoboldCPP Error**: {connectionError}";
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") yield break;

            StreamChunk? chunk = null;
            try { chunk = JsonSerializer.Deserialize<StreamChunk>(data); }
            catch { continue; }

            var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
                yield return delta;
        }
    }

    // ── Connectivity check ─────────────────────────────────────
    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("models", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Payload builder ────────────────────────────────────────
    private object BuildPayload(List<ChatMessage> history, string userMessage, bool stream)
    {
        var messages = new List<object>
        {
            new { role = "system", content = DevSystemPrompt }
        };

        foreach (var msg in history.TakeLast(20)) // context window budget
        {
            messages.Add(new
            {
                role = msg.Role switch
                {
                    MessageRole.User => "user",
                    MessageRole.Assistant => "assistant",
                    _ => "system"
                },
                content = msg.Content
            });
        }

        messages.Add(new { role = "user", content = userMessage });

        return new
        {
            model = _config["KoboldCpp:Model"] ?? "auto",
            messages,
            max_tokens = MaxTokens,
            temperature = Temperature,
            stream
        };
    }

    // ── JSON response shapes ───────────────────────────────────
    private record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] List<Choice>? Choices);

    private record Choice(
        [property: JsonPropertyName("message")] MessageContent? Message);

    private record MessageContent(
        [property: JsonPropertyName("content")] string? Content);

    private record StreamChunk(
        [property: JsonPropertyName("choices")] List<StreamChoice>? Choices);

    private record StreamChoice(
        [property: JsonPropertyName("delta")] DeltaContent? Delta);

    private record DeltaContent(
        [property: JsonPropertyName("content")] string? Content);
}
