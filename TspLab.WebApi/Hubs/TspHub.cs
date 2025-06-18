using Microsoft.AspNetCore.SignalR;

namespace TspLab.WebApi.Hubs;

/// <summary>
/// SignalR hub for streaming TSP genetic algorithm results
/// </summary>
public sealed class TspHub : Hub
{
    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "TspSolvers");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "TspSolvers");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to join a specific group for targeted updates
    /// </summary>
    /// <param name="groupName">Name of the group to join</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Allows clients to leave a specific group
    /// </summary>
    /// <param name="groupName">Name of the group to leave</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Returns the current connection ID for testing purposes
    /// </summary>
    /// <returns>The connection ID</returns>
    public string GetConnectionId()
    {
        return Context.ConnectionId;
    }

    /// <summary>
    /// Requests pausing the current algorithm execution
    /// </summary>
    public async Task RequestPause()
    {
        await Clients.Caller.SendAsync("PauseRequested", Context.ConnectionId);
    }

    /// <summary>
    /// Requests resuming algorithm execution from a specific state
    /// </summary>
    /// <param name="sessionId">Session ID to resume from</param>
    public async Task RequestResume(string sessionId)
    {
        await Clients.Caller.SendAsync("ResumeRequested", sessionId, Context.ConnectionId);
    }

    /// <summary>
    /// Notifies clients about state save completion
    /// </summary>
    /// <param name="sessionId">The saved session ID</param>
    /// <param name="generation">Current generation when saved</param>
    public async Task NotifyStateSaved(string sessionId, int generation)
    {
        await Clients.All.SendAsync("StateSaved", sessionId, generation, Context.ConnectionId);
    }
}
