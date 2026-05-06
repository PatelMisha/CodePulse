using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;

namespace backend.Services;

public class AIReviewService
{
    private readonly AnthropicClient _client;
    private readonly IHubContext<ExecutionHub> _hub;

    public AIReviewService(IConfiguration config, IHubContext<ExecutionHub> hub)
    {
        _client = new AnthropicClient(config["Anthropic:ApiKey"] ?? "");
        _hub = hub;
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

        var messageParams = new MessageParameters
        {
            Model = AnthropicModels.Claude45Sonnet,
            MaxTokens = 1024,
            Stream = true,
            Messages = [new Message(RoleType.User, prompt)]
        };

        await foreach (var streamEvent in _client.Messages.StreamClaudeMessageAsync(messageParams))
        {
            var text = streamEvent.Content?
                .OfType<TextContent>()
                .FirstOrDefault()?.Text;

            if (!string.IsNullOrEmpty(text))
                await _hub.Clients.Group(submissionId).SendAsync("reviewChunk", text);
        }

        await _hub.Clients.Group(submissionId).SendAsync("reviewComplete");
    }
}
