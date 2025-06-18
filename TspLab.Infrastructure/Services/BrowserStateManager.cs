using System.Text.Json;
using Microsoft.JSInterop;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;

namespace TspLab.Infrastructure.Services;

/// <summary>
/// Browser localStorage implementation for algorithm state persistence
/// </summary>
public sealed class BrowserStateManager : IAlgorithmStateManager
{
    private readonly IJSRuntime _jsRuntime;
    private const string StateKeyPrefix = "tsp_ga_state_";
    private const string StateListKey = "tsp_ga_states";

    public BrowserStateManager(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public async Task SaveStateAsync(GeneticAlgorithmState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, GetJsonSerializerOptions());
            var key = StateKeyPrefix + state.SessionId;

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, json);

            // Update states list
            await UpdateStatesListAsync(state, cancellationToken);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException($"Failed to save state to browser storage: {ex.Message}", ex);
        }
    }

    public async Task<GeneticAlgorithmState?> LoadStateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = StateKeyPrefix + sessionId;
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);

            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<GeneticAlgorithmState>(json, GetJsonSerializerOptions());
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException($"Failed to load state from browser storage: {ex.Message}", ex);
        }
    }

    public async Task<List<GeneticAlgorithmStateSummary>> GetAvailableStatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StateListKey);
            
            if (string.IsNullOrEmpty(json))
                return new List<GeneticAlgorithmStateSummary>();

            var summaries = JsonSerializer.Deserialize<List<GeneticAlgorithmStateSummary>>(json, GetJsonSerializerOptions());
            return summaries ?? new List<GeneticAlgorithmStateSummary>();
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException($"Failed to get available states from browser storage: {ex.Message}", ex);
        }
    }

    public async Task DeleteStateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = StateKeyPrefix + sessionId;
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key);

            // Remove from states list
            await RemoveFromStatesListAsync(sessionId, cancellationToken);
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException($"Failed to delete state from browser storage: {ex.Message}", ex);
        }
    }

    public async Task CreateCheckpointAsync(GeneticAlgorithmState state, CheckpointConfig config, 
        CancellationToken cancellationToken = default)
    {
        if (!config.EnableAutoCheckpoint)
            return;

        // Create checkpoint with timestamp suffix
        var checkpointId = $"{state.SessionId}_checkpoint_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        var checkpointState = CloneState(state);
        checkpointState.SessionId = checkpointId;

        await SaveStateAsync(checkpointState, cancellationToken);

        // Cleanup old checkpoints if needed
        await CleanupCheckpointsAsync(state.SessionId, config, cancellationToken);
    }

    public async Task CleanupCheckpointsAsync(string sessionId, CheckpointConfig config, 
        CancellationToken cancellationToken = default)
    {
        var allStates = await GetAvailableStatesAsync(cancellationToken);
        var checkpoints = allStates
            .Where(s => s.SessionId.StartsWith($"{sessionId}_checkpoint_"))
            .OrderByDescending(s => s.SavedAt)
            .Skip(config.MaxCheckpoints)
            .ToList();

        foreach (var checkpoint in checkpoints)
        {
            await DeleteStateAsync(checkpoint.SessionId, cancellationToken);
        }
    }

    private async Task UpdateStatesListAsync(GeneticAlgorithmState state, CancellationToken cancellationToken)
    {
        var summaries = await GetAvailableStatesAsync(cancellationToken);

        // Remove existing entry for this session
        summaries.RemoveAll(s => s.SessionId == state.SessionId);

        // Add new entry
        summaries.Add(new GeneticAlgorithmStateSummary
        {
            SessionId = state.SessionId,
            CityCount = state.Cities.Length,
            CurrentGeneration = state.CurrentGeneration,
            MaxGenerations = state.Config.MaxGenerations,
            BestDistance = state.BestTour?.Distance ?? 0,
            SavedAt = DateTime.UtcNow,
            PausedAt = state.PausedAt,
            Status = state.Status,
            ElapsedMilliseconds = state.ElapsedMilliseconds
        });

        var json = JsonSerializer.Serialize(summaries, GetJsonSerializerOptions());
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StateListKey, json);
    }

    private async Task RemoveFromStatesListAsync(string sessionId, CancellationToken cancellationToken)
    {
        var summaries = await GetAvailableStatesAsync(cancellationToken);
        summaries.RemoveAll(s => s.SessionId == sessionId);

        var json = JsonSerializer.Serialize(summaries, GetJsonSerializerOptions());
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StateListKey, json);
    }

    private static GeneticAlgorithmState CloneState(GeneticAlgorithmState original)
    {
        var json = JsonSerializer.Serialize(original, GetJsonSerializerOptions());
        return JsonSerializer.Deserialize<GeneticAlgorithmState>(json, GetJsonSerializerOptions())!;
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }
}
