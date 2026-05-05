using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

// Real-time channel between backend and browser.
// Output streams line by line, AI feedback streams word by word.
public class ExecutionHub : Hub
{
    public async Task JoinSubmission(string submissionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, submissionId);
    }

    public async Task LeaveSubmission(string submissionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, submissionId);
    }
}
