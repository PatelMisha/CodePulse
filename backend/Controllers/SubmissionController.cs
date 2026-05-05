using Microsoft.AspNetCore.Mvc;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionController : ControllerBase
{
    private readonly ExecutionService _execution;
    private readonly AIReviewService _aiReview;
    private readonly RateLimitService _rateLimit;

    public SubmissionController(ExecutionService execution, AIReviewService aiReview, RateLimitService rateLimit)
    {
        _execution = execution;
        _aiReview = aiReview;
        _rateLimit = rateLimit;
    }

    // POST /api/submission
    // 1. Check rate limit
    // 2. Run code in Docker container (streams output via SignalR)
    // 3. Send output to Claude AI (streams review via SignalR)
    [HttpPost]
    public async Task<ActionResult<SubmitResponse>> Submit([FromBody] SubmitRequest request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!await _rateLimit.IsAllowedAsync(clientIp))
        {
            var remaining = await _rateLimit.GetRemainingRunsAsync(clientIp);
            return StatusCode(429, new { message = "Rate limit exceeded. Max 10 runs per minute.", remaining });
        }

        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest(new { message = "Code cannot be empty." });

        var submissionId = Guid.NewGuid().ToString();

        // Run async so the HTTP response returns immediately with the submissionId.
        // The browser subscribes to SignalR using this ID to receive streamed results.
        _ = Task.Run(async () =>
        {
            var output = await _execution.ExecuteAsync(submissionId, request.Code, request.Language);
            await _aiReview.ReviewAsync(submissionId, request.Code, request.Language, output);
        });

        return Ok(new SubmitResponse { SubmissionId = Guid.Parse(submissionId) });
    }
}
