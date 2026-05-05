using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

// SignalR hub — the real-time channel between backend and browser
// When code runs, we push output line by line through here
// When AI reviews, we push feedback word by word through here
public class ExecutionHub : Hub
{
    public async Task JoinSubmission(string submissionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, submissionId);
    }
}
