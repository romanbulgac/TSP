using TspLab.Domain.Models;

namespace TspLab.Domain.Interfaces;

/// <summary>
/// Interface for persisting and restoring genetic algorithm state
/// </summary>
public interface IAlgorithmStateManager
{
    /// <summary>
    /// Saves the current algorithm state
    /// </summary>
    /// <param name="state">The state to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveStateAsync(GeneticAlgorithmState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads an algorithm state by session ID
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The loaded state or null if not found</returns>
    Task<GeneticAlgorithmState?> LoadStateAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available saved states
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available states</returns>
    Task<List<GeneticAlgorithmStateSummary>> GetAvailableStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved state
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteStateAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an automatic checkpoint
    /// </summary>
    /// <param name="state">The state to checkpoint</param>
    /// <param name="config">Checkpoint configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateCheckpointAsync(GeneticAlgorithmState state, CheckpointConfig config, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old checkpoints based on configuration
    /// </summary>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="config">Checkpoint configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CleanupCheckpointsAsync(string sessionId, CheckpointConfig config, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary information about a saved algorithm state
/// </summary>
public sealed class GeneticAlgorithmStateSummary
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Number of cities in the problem
    /// </summary>
    public int CityCount { get; set; }

    /// <summary>
    /// Current generation
    /// </summary>
    public int CurrentGeneration { get; set; }

    /// <summary>
    /// Maximum generations configured
    /// </summary>
    public int MaxGenerations { get; set; }

    /// <summary>
    /// Best distance found so far
    /// </summary>
    public double BestDistance { get; set; }

    /// <summary>
    /// When the state was saved
    /// </summary>
    public DateTime SavedAt { get; set; }

    /// <summary>
    /// When the state was paused
    /// </summary>
    public DateTime PausedAt { get; set; }

    /// <summary>
    /// Algorithm status
    /// </summary>
    public AlgorithmStatus Status { get; set; }

    /// <summary>
    /// Elapsed time in milliseconds
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage => MaxGenerations > 0 ? 
        Math.Min(100.0, (double)CurrentGeneration / MaxGenerations * 100.0) : 0.0;
}
