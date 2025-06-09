using Microsoft.AspNetCore.SignalR.Client;
using TspLab.Domain.Models;

namespace TspLab.Web.Services;

/// <summary>
/// Service for managing SignalR connection to receive real-time GA results
/// </summary>
public sealed class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly List<Func<GeneticAlgorithmResult, Task>> _resultHandlers = new();
    private readonly HttpClient _httpClient;

    public SignalRService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Connection state of the SignalR hub
    /// </summary>
    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Connection ID for targeted updates
    /// </summary>
    public string? ConnectionId => _hubConnection?.ConnectionId;

    /// <summary>
    /// Event fired when connection state changes
    /// </summary>
    public event Func<HubConnectionState, Task>? ConnectionStateChanged;

    /// <summary>
    /// Starts the SignalR connection
    /// </summary>
    /// <param name="hubUrl">URL of the SignalR hub (relative path)</param>
    /// <returns>True if connection was successful</returns>
    public async Task<bool> StartConnectionAsync(string hubUrl = "/tspHub")
    {
        try
        {
            // Construct absolute URL using the HttpClient's base address
            var baseAddress = _httpClient.BaseAddress ?? throw new InvalidOperationException("HttpClient BaseAddress not set");
            var fullHubUrl = new Uri(baseAddress, hubUrl.TrimStart('/'));

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(fullHubUrl.ToString())
                .WithAutomaticReconnect()
                .Build();

            // Register handlers
            _hubConnection.On<GeneticAlgorithmResult>("ReceiveGAResult", async result =>
            {
                foreach (var handler in _resultHandlers)
                {
                    await handler(result);
                }
            });

            _hubConnection.Closed += async (error) =>
            {
                if (ConnectionStateChanged != null)
                    await ConnectionStateChanged(HubConnectionState.Disconnected);
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                if (ConnectionStateChanged != null)
                    await ConnectionStateChanged(HubConnectionState.Connected);
            };

            _hubConnection.Reconnecting += async (error) =>
            {
                if (ConnectionStateChanged != null)
                    await ConnectionStateChanged(HubConnectionState.Reconnecting);
            };

            await _hubConnection.StartAsync();

            if (ConnectionStateChanged != null)
                await ConnectionStateChanged(HubConnectionState.Connected);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops the SignalR connection
    /// </summary>
    public async Task StopConnectionAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            if (ConnectionStateChanged != null)
                await ConnectionStateChanged(HubConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Subscribes to GA result updates
    /// </summary>
    /// <param name="handler">Handler for processing results</param>
    public void SubscribeToResults(Func<GeneticAlgorithmResult, Task> handler)
    {
        _resultHandlers.Add(handler);
    }

    /// <summary>
    /// Unsubscribes from GA result updates
    /// </summary>
    /// <param name="handler">Handler to remove</param>
    public void UnsubscribeFromResults(Func<GeneticAlgorithmResult, Task> handler)
    {
        _resultHandlers.Remove(handler);
    }

    /// <summary>
    /// Joins a specific SignalR group
    /// </summary>
    /// <param name="groupName">Name of the group to join</param>
    public async Task JoinGroupAsync(string groupName)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("JoinGroup", groupName);
        }
    }

    /// <summary>
    /// Leaves a specific SignalR group
    /// </summary>
    /// <param name="groupName">Name of the group to leave</param>
    public async Task LeaveGroupAsync(string groupName)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("LeaveGroup", groupName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
