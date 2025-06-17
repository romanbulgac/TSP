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
    private readonly List<Func<AntColonyResult, Task>> _acoResultHandlers = new();
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

            Console.WriteLine($"[SignalR] Attempting to connect to: {fullHubUrl}");
            Console.WriteLine($"[SignalR] Base address: {baseAddress}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(fullHubUrl.ToString())
                .WithAutomaticReconnect()
                .ConfigureLogging(builder => {
                    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
                })
                .Build();

            // Register handlers
            _hubConnection.On<GeneticAlgorithmResult>("ReceiveGAResult", async result =>
            {
                Console.WriteLine($"[SignalR] Received GA result: Generation {result.Generation}");
                foreach (var handler in _resultHandlers)
                {
                    await handler(result);
                }
            });

            _hubConnection.On<AntColonyResult>("ReceiveAcoResult", async result =>
            {
                Console.WriteLine($"[SignalR] Received ACO result: Iteration {result.Iteration}");
                foreach (var handler in _acoResultHandlers)
                {
                    await handler(result);
                }
            });

            _hubConnection.Closed += async (error) =>
            {
                Console.WriteLine($"[SignalR] Connection closed. Error: {error?.Message ?? "No error"}");
                if (ConnectionStateChanged != null)
                    await ConnectionStateChanged(HubConnectionState.Disconnected);
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                Console.WriteLine($"[SignalR] Reconnected with ID: {connectionId}");
                if (ConnectionStateChanged != null)
                    await ConnectionStateChanged(HubConnectionState.Connected);
            };

            _hubConnection.Reconnecting += async (error) =>
            {
                Console.WriteLine($"[SignalR] Reconnecting... Error: {error?.Message ?? "No error"}");
                if (ConnectionStateChanged != null)
                    await ConnectionStateChanged(HubConnectionState.Reconnecting);
            };

            Console.WriteLine("[SignalR] Starting connection...");
            await _hubConnection.StartAsync();
            Console.WriteLine($"[SignalR] Connected successfully with ID: {_hubConnection.ConnectionId}");

            if (ConnectionStateChanged != null)
                await ConnectionStateChanged(HubConnectionState.Connected);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SignalR] Connection failed: {ex.Message}");
            Console.WriteLine($"[SignalR] Stack trace: {ex.StackTrace}");
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
    /// Subscribes to ACO result updates
    /// </summary>
    /// <param name="handler">Handler for processing ACO results</param>
    public void SubscribeToAcoResults(Func<AntColonyResult, Task> handler)
    {
        _acoResultHandlers.Add(handler);
    }

    /// <summary>
    /// Unsubscribes from ACO result updates
    /// </summary>
    /// <param name="handler">Handler to remove</param>
    public void UnsubscribeFromAcoResults(Func<AntColonyResult, Task> handler)
    {
        _acoResultHandlers.Remove(handler);
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
