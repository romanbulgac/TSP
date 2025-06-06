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
}
