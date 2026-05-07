using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;

namespace backend.Services;

public class AIReviewService
{
    private readonly string _apiKey;
    private readonly IHubContext<ExecutionHub> _hub;
    private readonly IHttpClientFactory _httpClientFactory;

    public AIReviewService(IConfiguration config, IHubContext<ExecutionHub> hub, IHttpClientFactory httpClientFactory)
    {
        _apiKey = config["Groq:ApiKey"] ?? "";
        _hub = hub;
        _httpClientFactory = httpClientFactory;
    }

    public async Task ReviewAsync(string submissionId, string code, string language, string output)
    {
        var prompt = $"""
            You are a senior software engineer doing a code review. Be direct and specific.

            Language: {language}

            Code submitted:
            ```{language}
            {code}
            ```

            Execution output:
            {output}

            Review the code across these 5 areas. Reference actual line numbers where possible:

            **1. Correctness**
            Does it produce the right output? Does it handle edge cases (empty input, null, negative numbers, large values)?

            **2. Time & Space Complexity**
            What is the Big O complexity? Is there a more efficient approach?

            **3. Code Quality**
            Are variable/function names clear? Is the structure clean? Is anything repeated that should be a function?

            **4. Potential Bugs**
            What inputs would cause this to crash or return wrong results?

            **5. One Key Improvement**
            Give one specific, concrete change that would most improve this code. Show the improved version.
            """;

        await _hub.Clients.Group(submissionId).SendAsync("reviewStarted");

        var requestBody = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = 1024,
            stream = true
        };

        var json = JsonSerializer.Serialize(requestBody);

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            await _hub.Clients.Group(submissionId).SendAsync("reviewChunk", $"AI review unavailable ({response.StatusCode}): {err}");
            await _hub.Clients.Group(submissionId).SendAsync("reviewComplete");
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta")
                    .GetProperty("content")
                    .GetString();

                if (!string.IsNullOrEmpty(text))
                    await _hub.Clients.Group(submissionId).SendAsync("reviewChunk", text);
            }
            catch { /* skip malformed chunks */ }
        }

        await _hub.Clients.Group(submissionId).SendAsync("reviewComplete");
    }
}
