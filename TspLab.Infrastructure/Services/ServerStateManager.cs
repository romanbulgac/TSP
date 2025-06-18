using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TspLab.Domain.Interfaces;
using TspLab.Domain.Models;

namespace TspLab.Infrastructure.Services;

/// <summary>
/// Server-side implementation for algorithm state persistence using file system
/// </summary>
public sealed class ServerStateManager : IAlgorithmStateManager
{
    private readonly ILogger<ServerStateManager> _logger;
    private readonly string _stateDirectory;
    private const string StateFileExtension = ".tsp_state.json";

    public ServerStateManager(ILogger<ServerStateManager> logger, IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Get state directory from configuration or use default
        _stateDirectory = configuration.GetValue<string>("StateManagement:Directory") 
                         ?? Path.Combine(Directory.GetCurrentDirectory(), "tsp_states");
        
        // Ensure directory exists
        Directory.CreateDirectory(_stateDirectory);
        
        _logger.LogInformation("ServerStateManager initialized with directory: {Directory}", _stateDirectory);
    }

    public async Task SaveStateAsync(GeneticAlgorithmState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"{state.SessionId}{StateFileExtension}";
            var filePath = Path.Combine(_stateDirectory, fileName);
            
            var json = JsonSerializer.Serialize(state, GetJsonSerializerOptions());
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            
            _logger.LogInformation("Saved state for session {SessionId} to {FilePath}", state.SessionId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state for session {SessionId}", state.SessionId);
            throw new InvalidOperationException($"Failed to save state: {ex.Message}", ex);
        }
    }

    public async Task<GeneticAlgorithmState?> LoadStateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"{sessionId}{StateFileExtension}";
            var filePath = Path.Combine(_stateDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("State file not found for session {SessionId}: {FilePath}", sessionId, filePath);
                return null;
            }
            
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var state = JsonSerializer.Deserialize<GeneticAlgorithmState>(json, GetJsonSerializerOptions());
            
            _logger.LogInformation("Loaded state for session {SessionId} from {FilePath}", sessionId, filePath);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load state for session {SessionId}", sessionId);
            throw new InvalidOperationException($"Failed to load state: {ex.Message}", ex);
        }
    }

    public async Task<List<GeneticAlgorithmStateSummary>> GetAvailableStatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stateFiles = Directory.GetFiles(_stateDirectory, $"*{StateFileExtension}");
            var summaries = new List<GeneticAlgorithmStateSummary>();
            
            foreach (var filePath in stateFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var state = JsonSerializer.Deserialize<GeneticAlgorithmState>(json, GetJsonSerializerOptions());
                    
                    if (state != null)
                    {
                        summaries.Add(new GeneticAlgorithmStateSummary
                        {
                            SessionId = state.SessionId,
                            CityCount = state.Cities.Length,
                            CurrentGeneration = state.CurrentGeneration,
                            MaxGenerations = state.Config.MaxGenerations,
                            BestDistance = state.BestTour?.Distance ?? 0,
                            SavedAt = File.GetLastWriteTime(filePath),
                            PausedAt = state.PausedAt,
                            Status = state.Status
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read state file {FilePath}", filePath);
                }
            }
            
            return summaries.OrderByDescending(s => s.SavedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available states");
            throw new InvalidOperationException($"Failed to get available states: {ex.Message}", ex);
        }
    }

    public async Task DeleteStateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"{sessionId}{StateFileExtension}";
            var filePath = Path.Combine(_stateDirectory, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted state for session {SessionId}", sessionId);
            }
            else
            {
                _logger.LogWarning("State file not found for deletion: {SessionId}", sessionId);
            }
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete state for session {SessionId}", sessionId);
            throw new InvalidOperationException($"Failed to delete state: {ex.Message}", ex);
        }
    }

    public async Task CreateCheckpointAsync(GeneticAlgorithmState state, CheckpointConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            if (state.CurrentGeneration % config.GenerationInterval == 0)
            {
                var checkpointId = $"{state.SessionId}_checkpoint_{state.CurrentGeneration}";
                var checkpointState = state.WithSessionId(checkpointId);
                
                await SaveStateAsync(checkpointState, cancellationToken);
                _logger.LogInformation("Created checkpoint for session {SessionId} at generation {Generation}", 
                    state.SessionId, state.CurrentGeneration);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkpoint for session {SessionId}", state.SessionId);
            throw new InvalidOperationException($"Failed to create checkpoint: {ex.Message}", ex);
        }
    }

    public async Task CleanupCheckpointsAsync(string sessionId, CheckpointConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var checkpointFiles = Directory.GetFiles(_stateDirectory, $"{sessionId}_checkpoint_*{StateFileExtension}");
            var filesToDelete = checkpointFiles
                .Select(f => new { Path = f, CreationTime = File.GetCreationTime(f) })
                .OrderByDescending(f => f.CreationTime)
                .Skip(config.MaxCheckpoints)
                .ToList();
            
            foreach (var file in filesToDelete)
            {
                File.Delete(file.Path);
                _logger.LogInformation("Deleted old checkpoint: {FilePath}", file.Path);
            }
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup checkpoints for session {SessionId}", sessionId);
            throw new InvalidOperationException($"Failed to cleanup checkpoints: {ex.Message}", ex);
        }
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }
}
